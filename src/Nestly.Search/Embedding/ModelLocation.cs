namespace Nestly.Search.Embedding;

/// <summary>Finds the model directory from wherever the process happens to have been launched.</summary>
internal static class ModelLocation
{
    private const string SolutionFile = "Nestly.slnx";

    /// <summary>
    /// Resolves a relative directory against the repository root when there is one, and against
    /// the application directory otherwise.
    /// </summary>
    // The two cases are a developer running `dotnet run` from anywhere in the tree, and a
    // container that has no repository but does have the model copied in beside the binaries.
    public static string Resolve(string directory)
    {
        if (Path.IsPathRooted(directory))
        {
            return directory;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, SolutionFile)))
            {
                return Path.Combine(current.FullName, directory);
            }

            current = current.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, directory);
    }
}
