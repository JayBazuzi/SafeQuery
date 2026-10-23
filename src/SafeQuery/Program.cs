using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DbToMermaid;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Naiad;

namespace SafeQuery;

public static class Program
{
    private const string DefaultDiagramPath = "./database-schema.md";

    private static readonly string[] ForbiddenKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "MERGE", "DROP", "ALTER", "CREATE", "TRUNCATE",
        "EXEC", "EXECUTE", "GRANT", "REVOKE", "DENY", "INTO", "sp_", "xp_"
    ];

    public static async Task<int> Main(string[] args)
    {
        var diagramIndex = Array.IndexOf(args, "--create-diagram");
        if (diagramIndex >= 0)
        {
            return await CreateDiagram(args, diagramIndex);
        }

        string? sql;
        try
        {
            sql = ResolveSql(args);
        }
        catch (Exception ex)
        {
            return await ReportFailure(ex.Message);
        }

        if (!IsReadOnlySelect(sql, out _))
        {
            return await ReportFailure("Read-Only Queries Only");
        }

        try
        {
            var connectionString = GetConnectionString();
            await RunQueryAsync(connectionString, sql!);
            return 0;
        }
        catch (Exception ex)
        {
            return await ReportFailure(ex.Message);
        }
    }

    private static async Task<int> ReportFailure(params string[] messages)
    {
        foreach (var message in messages)
        {
            await Console.Error.WriteLineAsync($"Exception: {message}");
        }

        return 1;
    }

    private static async Task<int> CreateDiagram(string[] args, int flagIndex)
    {
        try
        {
            var hasPath = flagIndex + 1 < args.Length && !args[flagIndex + 1].StartsWith('-');
            var path = hasPath ? args[flagIndex + 1] : DefaultDiagramPath;
            // SMO opens its own connection from connection.ConnectionString, which drops the password after Open unless this is set.
            var builder = new SqlConnectionStringBuilder(GetConnectionString()) { PersistSecurityInfo = true };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            var markdown = await SqlServerToMermaid.RenderMarkdown(connection);
            var parseable = MakeMermaidParseable(markdown);
            var narrow = BlankRelationshipLabels(parseable);
            await File.WriteAllTextAsync(path, narrow);
            Console.WriteLine($"Diagram written to {path}");

            var svgPath = Path.ChangeExtension(path, ".svg");
            await File.WriteAllTextAsync(svgPath, Mermaid.Render(ExtractMermaid(narrow)));
            Console.WriteLine($"SVG written to {svgPath}");
            return 0;
        }
        catch (Exception ex)
        {
            // SMO wraps the real cause (login, TLS, ...) in inner exceptions.
            return await ReportFailure([.. ExceptionChain(ex).Select(inner => inner.Message)]);
        }
    }

    private static IEnumerable<Exception> ExceptionChain(Exception ex)
    {
        for (var inner = ex; inner != null; inner = inner.InnerException)
        {
            yield return inner;
        }
    }

    // SqlServerToMermaid output that Mermaid cannot parse: commas inside types (e.g. "decimal(18,2)").
    private static string MakeMermaidParseable(string markdown)
    {
        return Regex.Replace(markdown, @"\((\d+),(\d+)\)", "($1-$2)");
    }

    // Relationship labels (long FK constraint names) make the diagram very wide, so they are blanked.
    private static string BlankRelationshipLabels(string markdown)
    {
        return Regex.Replace(markdown, @"(\|\|--o\{ ""[^""]+"") : ""[^""]*""", "$1 : \"\"");
    }

    private static string ExtractMermaid(string markdown)
    {
        var match = Regex.Match(markdown, @"```mermaid\s*\r?\n(.*?)```", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : markdown;
    }

    private static string? ResolveSql(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "-f")
            {
                if (i + 1 >= args.Length)
                {
                    throw new InvalidOperationException("-f flag requires a file path argument");
                }

                var path = args[i + 1];
                if (!File.Exists(path))
                {
                    throw new InvalidOperationException($"SQL file not found: {path}");
                }

                return File.ReadAllText(path);
            }
        }

        var positional = args.FirstOrDefault(a => a != "-f");
        if (!string.IsNullOrWhiteSpace(positional))
        {
            return positional;
        }

        if (Console.IsInputRedirected)
        {
            return Console.In.ReadToEnd();
        }

        throw new InvalidOperationException(
            "No SQL provided. Pass SQL as an argument, use -f <path>, or pipe SQL via stdin.");
    }

    private static bool IsReadOnlySelect(string? rawSql, out string reason)
    {
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(rawSql))
        {
            reason = "Empty query";
            return false;
        }

        var stripped = StripComments(rawSql).Trim();

        if (string.IsNullOrWhiteSpace(stripped))
        {
            reason = "Empty query after stripping comments";
            return false;
        }

        var semicolonIndex = stripped.IndexOf(';');
        if (semicolonIndex >= 0)
        {
            var remainder = stripped[(semicolonIndex + 1)..].Trim();
            if (remainder.Length > 0)
            {
                reason = "Multiple statements are not allowed";
                return false;
            }

            stripped = stripped[..semicolonIndex].TrimEnd();
        }

        if (!Regex.IsMatch(stripped, @"^\s*(SELECT|WITH)\b", RegexOptions.IgnoreCase))
        {
            reason = "Query must start with SELECT or WITH";
            return false;
        }

        foreach (var keyword in ForbiddenKeywords)
        {
            var pattern = $@"\b{Regex.Escape(keyword)}\b";
            if (Regex.IsMatch(stripped, pattern, RegexOptions.IgnoreCase))
            {
                reason = $"Forbidden keyword detected: {keyword}";
                return false;
            }
        }

        return true;
    }

    private static string StripComments(string sql)
    {
        var noBlockComments = Regex.Replace(sql, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        var noLineComments = Regex.Replace(noBlockComments, @"--[^\r\n]*", string.Empty);
        return noLineComments;
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".safequery", "settings.json");
    }

    // Order: user-level settings file, then the ConnectionStrings__DefaultConnection env var, then an interactive prompt (saved to the file).
    private static string GetConnectionString()
    {
        var settingsPath = GetSettingsPath();
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddJsonFile(settingsPath, optional: true, reloadOnChange: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        return PromptAndSaveConnectionString(settingsPath);
    }

    private static string PromptAndSaveConnectionString(string settingsPath)
    {
        if (Console.IsInputRedirected)
        {
            throw new InvalidOperationException(
                $"Connection string not configured. Run safequery once interactively (without piped input) to be prompted, " +
                $"create {settingsPath}, or set the ConnectionStrings__DefaultConnection environment variable.");
        }

        Console.Error.WriteLine($"No connection string found. It will be saved to {settingsPath}");
        Console.Error.Write("SQL Server connection string: ");
        var entered = Console.ReadLine();
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

    private static async Task RunQueryAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = 30
        };

        await using var reader = await command.ExecuteReaderAsync();

        var columnNames = new string[reader.FieldCount];
        for (var i = 0; i < reader.FieldCount; i++)
        {
            columnNames[i] = reader.GetName(i);
        }

        var output = new StringBuilder();
        output.Append("| ").Append(string.Join(" | ", columnNames)).Append(" |\n");
        output.Append("| ").Append(string.Join(" | ", columnNames.Select(_ => "---"))).Append(" |\n");

        var rowCount = 0;
        while (await reader.ReadAsync())
        {
            rowCount++;
            var values = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i)?.ToString() ?? string.Empty;
                values[i] = EscapeMarkdownCell(value);
            }

            output.Append("| ").Append(string.Join(" | ", values)).Append(" |\n");
        }

        Console.Write(output.ToString());

        if (rowCount == 0)
        {
            Console.WriteLine("_(0 rows)_");
        }
    }

    private static string EscapeMarkdownCell(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("|", "\\|")
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ");
    }
}
