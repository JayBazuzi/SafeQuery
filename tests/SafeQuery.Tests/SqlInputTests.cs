using Xunit;

namespace SafeQuery.Tests;

public class SqlInputTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("safequery-tests").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void PositionalArgumentIsTheSql() =>
        Assert.Equal("select 1", SqlInput.Resolve(["select 1"], TextReader.Null, false));

    [Fact]
    public void FileFlagReadsTheFile()
    {
        var path = Path.Combine(_dir, "q.sql");
        File.WriteAllText(path, "select 2");

        Assert.Equal("select 2", SqlInput.Resolve(["-f", path], TextReader.Null, false));
    }

    [Fact]
    public void FileFlagWinsOverPositionalArgument()
    {
        var path = Path.Combine(_dir, "q.sql");
        File.WriteAllText(path, "select 2");

        Assert.Equal("select 2", SqlInput.Resolve(["select 1", "-f", path], TextReader.Null, false));
    }

    [Fact]
    public void FileFlagWithoutPathThrows()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SqlInput.Resolve(["-f"], TextReader.Null, false));

        Assert.Equal("-f flag requires a file path argument", ex.Message);
    }

    [Fact]
    public void MissingFileThrows()
    {
        var path = Path.Combine(_dir, "nope.sql");

        var ex = Assert.Throws<InvalidOperationException>(() => SqlInput.Resolve(["-f", path], TextReader.Null, false));

        Assert.Equal($"SQL file not found: {path}", ex.Message);
    }

    [Fact]
    public void ReadsStdinWhenRedirectedAndNoArguments() =>
        Assert.Equal("select 3", SqlInput.Resolve([], new StringReader("select 3"), true));

    [Fact]
    public void ArgumentWinsOverStdin() =>
        Assert.Equal("select 1", SqlInput.Resolve(["select 1"], new StringReader("select 3"), true));

    [Fact]
    public void NoInputAtAllThrows()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SqlInput.Resolve([], TextReader.Null, false));

        Assert.StartsWith("No SQL provided.", ex.Message);
    }
}
