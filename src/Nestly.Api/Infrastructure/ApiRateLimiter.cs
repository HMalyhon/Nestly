using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using IPNetwork = System.Net.IPNetwork;

namespace Nestly.Api.Infrastructure;

/// <summary>Bounds what one caller can ask of the embedder and the cluster.</summary>
// The 200 ms debounce in the browser is the client being polite, not a limit: nothing stops a
// loop of searches, and each one is an ONNX forward pass plus seven aggregations that escape the
// query by design.
internal static class ApiRateLimiter
{
    /// <summary>Sustained rate and burst for the aggregating endpoints, search and map.</summary>
    // Pacing a person is the client's job, and its debounces cap a browser at about ten requests
    // a second across the two. These are six times that sustained and thirty times it in a burst:
    // high enough that no client of this API is ever throttled, low enough to stop a loop.
    private const int SearchPerSecond = 60;
    private const int SearchBurst = 300;

    /// <summary>The same, for a suggest of one prefix query and a detail of one document read.</summary>
    private const int ReadPerSecond = 120;
    private const int ReadBurst = 600;

    private static readonly TimeSpan Replenishment = TimeSpan.FromSeconds(1);

    // RFC 1918, which is what a container network hands the proxy -- these are constants of the
    // internet rather than addresses of this deployment.
#pragma warning disable S1313 // Do not hardcode IP addresses
    private static readonly IPNetwork[] PrivateNetworks =
    [
        new(IPAddress.Parse("10.0.0.0"), 8),
        new(IPAddress.Parse("172.16.0.0"), 12),
        new(IPAddress.Parse("192.168.0.0"), 16),
    ];
#pragma warning restore S1313

    // What actually protects the box, and the reason the limits above can be loose. Measured, both
    // endpoints reach their ceiling at twice the core count -- search 50 requests a second, map
    // 144 -- so gating here costs neither of them throughput and keeps latency off the floor.
    private static readonly int WorkPermits = Environment.ProcessorCount * 2;

    public static IServiceCollection AddApiRateLimiter(this IServiceCollection services)
    {
        // Behind nginx every request arrives from the proxy, which would make one shared bucket of
        // a per-caller limit. Only private peers are trusted, so a direct caller cannot forge one.
        services.Configure<ForwardedHeadersOptions>(forwarded =>
        {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            forwarded.ForwardLimit = 1;

            foreach (var network in PrivateNetworks)
            {
                forwarded.KnownIPNetworks.Add(network);
            }
        });

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = RejectAsync;

            // Chained, so an aggregating request passes the caller's budget and then the work gate.
            limiter.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(ByCaller),
                PartitionedRateLimiter.Create<HttpContext, string>(ByWork));
        });

        return services;
    }

    /// <summary>Which budget a path draws on, and none for anything outside the API.</summary>
    // /health is deliberately unmetered: Compose gates two services on it.
    internal static (string Group, int Burst, int PerSecond) BudgetFor(PathString path)
    {
        if (IsAggregating(path))
        {
            return ("search", SearchBurst, SearchPerSecond);
        }

        return path.StartsWithSegments("/api") ? ("read", ReadBurst, ReadPerSecond) : (string.Empty, 0, 0);
    }

    /// <summary>The two endpoints that make Elasticsearch aggregate rather than read.</summary>
    private static bool IsAggregating(PathString path) =>
        path.StartsWithSegments("/api/listings/search") || path.StartsWithSegments("/api/listings/map");

    private static RateLimitPartition<string> ByCaller(HttpContext context)
    {
        var (group, burst, perSecond) = BudgetFor(context.Request.Path);

        if (burst == 0)
        {
            return RateLimitPartition.GetNoLimiter<string>(string.Empty);
        }

        var caller = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // A bucket rather than a window: typing arrives in bursts, and a fixed window both cuts
        // one in half at its edge and lets twice its budget through across that edge.
        return RateLimitPartition.GetTokenBucketLimiter(
            $"{group}:{caller}",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = burst,
                TokensPerPeriod = perSecond,
                ReplenishmentPeriod = Replenishment,

                // Queueing belongs to the gate below, which knows what the box can take.
                QueueLimit = 0,
            });
    }

    // One queue for everybody, because what it protects is the box's cores rather than a caller's
    // share of them.
    private static RateLimitPartition<string> ByWork(HttpContext context) =>
        IsAggregating(context.Request.Path)
            ? RateLimitPartition.GetConcurrencyLimiter(
                "aggregating",
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = WorkPermits,
                    QueueLimit = WorkPermits * 2,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                })
            : RateLimitPartition.GetNoLimiter<string>(string.Empty);

    private static async ValueTask RejectAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        // The window knows when it resets; the inference queue only knows it is full, and a client
        // told nothing at all retries immediately.
        var after = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var window)
            ? (int)Math.Ceiling(window.TotalSeconds)
            : 1;

        context.HttpContext.Response.Headers.RetryAfter = after.ToString(CultureInfo.InvariantCulture);

        // The same ProblemDetails shape as every other refusal, so one client path reads them all.
        var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();

        await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails =
            {
                Title = "Too many requests.",
                Detail = "This client has run too many searches. Try again shortly.",
                Status = StatusCodes.Status429TooManyRequests,
            },
        }).ConfigureAwait(false);
    }
}
