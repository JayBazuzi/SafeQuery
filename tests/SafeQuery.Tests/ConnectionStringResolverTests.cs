using Xunit;

namespace SafeQuery.Tests;

public class ConnectionStringResolverTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("safequery-tests").FullName;
    private string SettingsPath => Path.Combine(_dir, "sub", "settings.json");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static KeyValuePair<string, string?>[] Env(string? value) =>
        value is null ? [] : [new("ConnectionStrings:DefaultConnection", value)];

    private void WriteSettings(string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, $$"""{ "ConnectionStrings": { "DefaultConnection": "{{value}}" } }""");
    }

    [Fact]
    public void UsesEnvironmentWhenNoFile() =>
        Assert.Equal("env", ConnectionStringResolver.Resolve(SettingsPath, TextReader.Null, TextWriter.Null, true, Env("env")));

    [Fact]
    public void UsesSettingsFileWhenNoEnvironment()
    {
        WriteSettings("file");

        Assert.Equal("file", ConnectionStringResolver.Resolve(SettingsPath, TextReader.Null, TextWriter.Null, true, Env(null)));
    }

    [Fact]
    public void SettingsFileWinsOverEnvironment()
    {
        WriteSettings("file");

        Assert.Equal("file", ConnectionStringResolver.Resolve(SettingsPath, TextReader.Null, TextWriter.Null, true, Env("env")));
    }

    [Fact]
    public void RedirectedInputWithNothingConfiguredThrows()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConnectionStringResolver.Resolve(SettingsPath, TextReader.Null, TextWriter.Null, true, Env(null)));

        Assert.StartsWith("Connection string not configured.", ex.Message);
        Assert.Contains(SettingsPath, ex.Message);
    }

    [Fact]
    public void PromptsAndSavesTheAnswer()
    {
        var prompt = new StringWriter();

        var result = ConnectionStringResolver.Resolve(
            SettingsPath, new StringReader("  Server=x;Database=y  \n"), prompt, false, Env(null));

        Assert.Equal("Server=x;Database=y", result);
        Assert.Contains("SQL Server connection string:", prompt.ToString());
        Assert.Equal(
            "Server=x;Database=y",
            ConnectionStringResolver.Resolve(SettingsPath, TextReader.Null, TextWriter.Null, true, Env(null)));
    }

    [Fact]
    public void SavedSettingsFileIsOwnerOnlyOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        ConnectionStringResolver.Resolve(SettingsPath, new StringReader("Server=x\n"), TextWriter.Null, false, Env(null));

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(SettingsPath));
    }

    [Fact]
    public void EmptyPromptAnswerThrows()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConnectionStringResolver.Resolve(SettingsPath, new StringReader("\n"), TextWriter.Null, false, Env(null)));

        Assert.Equal("No connection string entered.", ex.Message);
        Assert.False(File.Exists(SettingsPath));
    }
}
