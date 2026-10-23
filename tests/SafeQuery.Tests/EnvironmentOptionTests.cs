using Xunit;

namespace SafeQuery.Tests;

public class EnvironmentOptionTests
{
    [Fact]
    public void NoFlagLeavesArgsAlone()
    {
        var (name, remaining) = EnvironmentOption.Extract(["select 1"]);

        Assert.Null(name);
        Assert.Equal(["select 1"], remaining);
    }

    [Fact]
    public void FlagIsRemovedFromArgs()
    {
        var (name, remaining) = EnvironmentOption.Extract(["--env", "prod", "-f", "q.sql"]);

        Assert.Equal("prod", name);
        Assert.Equal(["-f", "q.sql"], remaining);
    }

    [Fact]
    public void FlagAfterOtherArgsIsRemoved()
    {
        var (name, remaining) = EnvironmentOption.Extract(["--create-diagram", "--env", "dev"]);

        Assert.Equal("dev", name);
        Assert.Equal(["--create-diagram"], remaining);
    }

    [Theory]
    [InlineData("--env")]
    [InlineData("--env", "-f")]
    public void MissingNameThrows(params string[] args)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => EnvironmentOption.Extract(args));

        Assert.Equal("--env flag requires an environment name", ex.Message);
    }
}
