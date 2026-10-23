using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SafeQuery;

internal static class ConnectionStringResolver
{
    internal static string DefaultSettingsPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".safequery", "settings.json");

    // Order: user-level settings file, then the ConnectionStrings__DefaultConnection env var, then an interactive prompt (saved to the file).
    // environment: null reads the real process environment.
    internal static string Resolve(
        string settingsPath,
        TextReader input,
        TextWriter prompt,
        bool inputRedirected,
        IEnumerable<KeyValuePair<string, string?>>? environment = null)
    {
        var builder = new ConfigurationBuilder();
        if (environment is null)
        {
            builder.AddEnvironmentVariables();
        }
        else
        {
            builder.AddInMemoryCollection(environment);
        }

        var configuration = builder
            .AddJsonFile(settingsPath, optional: true, reloadOnChange: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        return PromptAndSave(settingsPath, input, prompt, inputRedirected);
    }

    private static string PromptAndSave(string settingsPath, TextReader input, TextWriter prompt, bool inputRedirected)
    {
        if (inputRedirected)
        {
            throw new InvalidOperationException(
                $"Connection string not configured. Run safequery once interactively (without piped input) to be prompted, " +
                $"create {settingsPath}, or set the ConnectionStrings__DefaultConnection environment variable.");
        }

        prompt.WriteLine($"No connection string found. It will be saved to {settingsPath}");
        prompt.Write("SQL Server connection string: ");
        var entered = input.ReadLine();
        if (string.IsNullOrWhiteSpace(entered))
        {
            throw new InvalidOperationException("No connection string entered.");
        }

        entered = entered.Trim();
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        var json = JsonSerializer.Serialize(
            new { ConnectionStrings = new { DefaultConnection = entered } },
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsPath, json);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(settingsPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return entered;
    }
}
