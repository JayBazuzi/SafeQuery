using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace SafeQuery;

internal static class ConnectionStringResolver
{
    private const string RepositoriesKey = "Repositories";
    private const string ExampleConnectionString =
        "Server=localhost;Database=MyDb;User Id=readonly_user;Password=your_password;TrustServerCertificate=True";

    internal static string DefaultSettingsPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".safequery", "settings.json");

    // Order: the settings-file entry for the nearest directory (from workingDirectory up to its git root),
    // then the ConnectionStrings__DefaultConnection env var, then an interactive prompt (saved under the git root).
    // Outside a git repo only workingDirectory itself is used.
    // environment: null reads the real process environment.
    internal static string Resolve(
        string settingsPath,
        string workingDirectory,
        TextReader input,
        TextWriter prompt,
        bool inputRedirected,
        IEnumerable<KeyValuePair<string, string?>>? environment = null)
    {
        var directories = DirectoriesToCheck(workingDirectory);

        var saved = ReadRepositories(settingsPath);
        foreach (var directory in directories)
        {
            if (saved.TryGetValue(directory, out var found) && !string.IsNullOrWhiteSpace(found))
            {
                return found;
            }
        }

        var builder = new ConfigurationBuilder();
        if (environment is null)
        {
            builder.AddEnvironmentVariables();
        }
        else
        {
            builder.AddInMemoryCollection(environment);
        }

        var connectionString = builder.Build().GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        return PromptAndSave(settingsPath, directories[^1], input, prompt, inputRedirected);
    }

    // Directories from the working directory up to the git root, nearest first. The last item is where a new entry is saved.
    private static List<string> DirectoriesToCheck(string workingDirectory)
    {
        var start = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workingDirectory));
        var directories = new List<string>();
        for (var current = start; current is not null; current = Path.GetDirectoryName(current))
        {
            directories.Add(current);
            if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, ".git")))
            {
                return directories;
            }
        }

        return [start];
    }

    private static Dictionary<string, string> ReadRepositories(string settingsPath)
    {
        var comparer = OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        var result = new Dictionary<string, string>(comparer);
        if (File.Exists(settingsPath)
            && JsonNode.Parse(File.ReadAllText(settingsPath)) is JsonObject root
            && root[RepositoriesKey] is JsonObject repositories)
        {
            foreach (var (path, value) in repositories)
            {
                if (value?.GetValue<string>() is { } connection)
                {
                    result[Path.TrimEndingDirectorySeparator(path)] = connection;
                }
            }
        }

        return result;
    }

    private static string PromptAndSave(string settingsPath, string saveDirectory, TextReader input, TextWriter prompt, bool inputRedirected)
    {
        if (inputRedirected)
        {
            throw new InvalidOperationException(
                $"Connection string not configured for {saveDirectory}. Run safequery once interactively (without piped input) to be prompted, " +
                $"add it to {settingsPath}, or set the ConnectionStrings__DefaultConnection environment variable.");
        }

        prompt.WriteLine($"No connection string found for {saveDirectory}. It will be saved to {settingsPath}");
        prompt.WriteLine($"Example: {ExampleConnectionString}");
        prompt.Write("SQL Server connection string: ");
        var entered = input.ReadLine();
        if (string.IsNullOrWhiteSpace(entered))
        {
            throw new InvalidOperationException("No connection string entered.");
        }

        entered = entered.Trim();
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        var root = File.Exists(settingsPath) ? JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject ?? new JsonObject() : new JsonObject();
        var repositories = root[RepositoriesKey] as JsonObject ?? new JsonObject();
        repositories[saveDirectory] = entered;
        root[RepositoriesKey] = repositories;
        File.WriteAllText(settingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(settingsPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return entered;
    }
}
