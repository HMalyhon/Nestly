using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Nestly.Seeder;

/// <summary>
/// Reads data/listings.csv.gz, cleans it, and reports what came out:
/// <code>dotnet run --project src/Nestly.Seeder</code>
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
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

        builder.Services.AddSingleton<SeedRunner>();

        try
        {
            using var host = builder.Build();

            return host.Services.GetRequiredService<SeedRunner>().Run();
        }
        catch (OptionsValidationException invalid)
        {
            // A settings problem is the operator's to fix, not a defect to debug, so it gets a
            // sentence rather than a stack trace.
            Console.Error.WriteLine(invalid.Message);
            return 2;
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
                    overrides[$"{SeederOptions.SectionName}:{nameof(SeederOptions.Limit)}"] = args[index++];
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
        Console.Error.WriteLine("usage: dotnet run --project src/Nestly.Seeder [--file <path>] [--count <n>]");
}
