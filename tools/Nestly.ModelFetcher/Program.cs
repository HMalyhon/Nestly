using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace Nestly.ModelFetcher;

/// <summary>
/// Fetches the sentence-embedding model:
/// <code>dotnet run --project tools/Nestly.ModelFetcher</code>
/// </summary>
/// <remarks>
/// The model is 90 MB, so it is downloaded rather than committed -- a repository that carries its
/// own binary dependencies is a repository nobody wants to clone. Every file is checked against a
/// recorded SHA-256, which makes this reproducible rather than merely convenient: the vectors in
/// the index are only comparable to a query vector if both came from the same weights.
/// </remarks>
internal static class Program
{
    private static async Task<int> Main()
    {
        var options = Load();

        try
        {
            Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        }
        catch (ValidationException invalid)
        {
            await Console.Error.WriteLineAsync($"appsettings.json is not usable: {invalid.Message}").ConfigureAwait(false);
            return 2;
        }

        var directory = Path.GetFullPath(Path.Combine(FindRepositoryRoot(), options.Directory));

        System.IO.Directory.CreateDirectory(directory);

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        foreach (var file in options.Files)
        {
            var path = Path.Combine(directory, file.Name);

            if (await MatchesAsync(path, file.Sha256).ConfigureAwait(false))
            {
                Console.WriteLine($"ok       {file.Name} (already present, digest matches)");
                continue;
            }

            Console.WriteLine($"fetching {file.Name} from {file.Url}");

            if (!await TryDownloadAsync(client, file, path).ConfigureAwait(false))
            {
                return 1;
            }
        }

        Console.WriteLine($"model ready in {directory}");

        return 0;
    }

    private static async Task<bool> TryDownloadAsync(HttpClient client, ModelFile file, string path)
    {
        // To a temporary name first: a half-written model that happens to be the right length is
        // harder to notice than a missing one.
        var partial = path + ".partial";

        try
        {
            using var response = await client.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            await using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            await using (var destination = File.Create(partial))
            {
                await source.CopyToAsync(destination).ConfigureAwait(false);
            }
        }
        catch (HttpRequestException failure)
        {
            await Console.Error.WriteLineAsync($"could not download {file.Name}: {failure.Message}").ConfigureAwait(false);
            File.Delete(partial);

            return false;
        }

        if (!await MatchesAsync(partial, file.Sha256).ConfigureAwait(false))
        {
            await Console.Error.WriteLineAsync(
                $"{file.Name} downloaded but its SHA-256 does not match appsettings.json -- refusing to install it").ConfigureAwait(false);
            File.Delete(partial);

            return false;
        }

        File.Move(partial, path, overwrite: true);

        var size = new FileInfo(path).Length;

        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"ok       {file.Name} ({size / 1_000_000d:F1} MB, digest verified)"));

        return true;
    }

    private static async Task<bool> MatchesAsync(string path, string expected)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        await using var stream = File.OpenRead(path);

        var hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);

        return Convert.ToHexStringLower(hash).Equals(expected, StringComparison.Ordinal);
    }

    private static ModelOptions Load()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        return configuration.GetSection(ModelOptions.SectionName).Get<ModelOptions>() ?? new ModelOptions();
    }

    /// <summary>
    /// Walks up from the binary to the directory holding the solution, so the tool works the same
    /// whether it is launched from the repository root, from its own project folder, or by an IDE
    /// with a working directory of its own choosing.
    /// </summary>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Nestly.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return System.IO.Directory.GetCurrentDirectory();
    }
}
