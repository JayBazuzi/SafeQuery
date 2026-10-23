using DbToMermaid;
using Microsoft.Data.SqlClient;
using Naiad;

namespace SafeQuery;

public static class Program
{
    private const string DefaultDiagramPath = "./InternalDocuments/database-schema.md";

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
            sql = SqlInput.Resolve(args, Console.In, Console.IsInputRedirected);
        }
        catch (Exception ex)
        {
            return await ReportFailure(ex.Message);
        }

        if (!ReadOnlyGuard.IsReadOnlySelect(sql, out _))
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
            var parseable = DiagramText.MakeMermaidParseable(markdown);
            var narrow = DiagramText.BlankRelationshipLabels(parseable);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            await File.WriteAllTextAsync(path, narrow);
            Console.WriteLine($"Diagram written to {path}");

            var svgPath = Path.ChangeExtension(path, ".svg");
            await File.WriteAllTextAsync(svgPath, Mermaid.Render(DiagramText.ExtractMermaid(narrow)));
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

    private static async Task RunQueryAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = 30
        };

        await using var reader = await command.ExecuteReaderAsync();

        var columnNames = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        var rows = new List<string[]>();
        while (await reader.ReadAsync())
        {
            var values = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                values[i] = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i)?.ToString() ?? string.Empty;
            }

            rows.Add(values);
        }

        Console.Write(MarkdownTable.Render(columnNames, rows));
    }

    private static string GetConnectionString() =>
        ConnectionStringResolver.Resolve(
            ConnectionStringResolver.DefaultSettingsPath(), Directory.GetCurrentDirectory(), Console.In, Console.Error, Console.IsInputRedirected);
}
