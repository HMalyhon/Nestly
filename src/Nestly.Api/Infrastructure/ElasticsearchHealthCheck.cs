using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Nestly.Api.Infrastructure;

/// <summary>Reports the API unhealthy when Elasticsearch is unreachable.</summary>
// A ping rather than a query, so a slow cluster does not become a slow health endpoint.
internal sealed class ElasticsearchHealthCheck(ElasticsearchClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var ping = await client.PingAsync(cancellationToken).ConfigureAwait(false);

        if (ping.IsValidResponse)
        {
            return HealthCheckResult.Healthy("Elasticsearch is reachable.");
        }

        ping.TryGetOriginalException(out var cause);

        return HealthCheckResult.Unhealthy(ping.DebugInformation.Split('\n', 2)[0], cause);
    }
}
