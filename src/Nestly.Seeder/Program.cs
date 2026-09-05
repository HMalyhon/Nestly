using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Nestly.Search;

namespace Nestly.Seeder;

/// <summary>
/// Reads data/listings.csv.gz, cleans it, and fills the listings index:
/// <code>dotnet run --project src/Nestly.Seeder</code>
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var overrides = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (!TryParseArguments(args, overrides, out var exitCode))
        {
            return exitCode;
        }

        // Two deviations from the default host setup, both because this is a console tool
        // rather than a service. The content root is the binary's own directory, so
        // appsettings.json is found however the process was launched; and the host's args
        // parsing is skipped, because --count and --file are this tool's flags rather than
        // configuration keys, and they were translated above.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = [],
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Configuration.AddInMemoryCollection(overrides);

        builder.Services.AddOptions<SeederOptions>()
            .Bind(builder.Configuration.GetSection(SeederOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddNestlySearch(builder.Configuration);
        builder.Services.AddSingleton<SeedRunner>();

        // Ctrl+C cancels the run instead of killing it mid-bulk. The index is dropped and
        // rebuilt from scratch on every run, so an interrupted seed leaves nothing to clean up
        // beyond running it again.
        using var cancellation = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            using var host = builder.Build();

            return await host.Services.GetRequiredService<SeedRunner>()
                .RunAsync(cancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OptionsValidationException invalid)
        {
            // A settings problem is the operator's to fix, not a defect to debug, so it gets a
            // sentence rather than a stack trace.
            await Console.Error.WriteLineAsync(invalid.Message).ConfigureAwait(false);
            return 2;
        }
        catch (OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("cancelled").ConfigureAwait(false);
            return 130;
        }
    }

    private static bool TryParseArguments(string[] args, Dictionary<string, string?> overrides, out int exitCode)
    {
        exitCode = 0;
        var index = 0;

        while (index < args.Length)
        {
            var argument = args[index++];

            switch (argument)
            {
                case "--file" when index < args.Length:
                    overrides[$"{SeederOptions.SectionName}:{nameof(SeederOptions.DataPath)}"] = args[index++];
                    break;
                case "--count" when index < args.Length:
                    var count = args[index++];

                    // Checked here because the configuration binder throws before validation
                    // runs, and its exception is a stack trace rather than a sentence.
                    if (!int.TryParse(count, CultureInfo.InvariantCulture, out _))
                    {
                        Console.Error.WriteLine($"--count expects a number, got: {count}");
                        exitCode = 2;
                        return false;
                    }

                    overrides[$"{SeederOptions.SectionName}:{nameof(SeederOptions.Limit)}"] = count;
                    break;
                case "--dry-run":
                    overrides[$"{SeederOptions.SectionName}:{nameof(SeederOptions.DryRun)}"] = "true";
                    break;
                case "--help" or "-h":
                    PrintUsage();
                    return false;
                default:
                    Console.Error.WriteLine($"unrecognised argument: {argument}");
                    PrintUsage();
                    exitCode = 2;
                    return false;
            }
        }

        return true;
    }

    private static void PrintUsage() =>
        Console.Error.WriteLine("usage: dotnet run --project src/Nestly.Seeder [--file <path>] [--count <n>] [--dry-run]");
}
