using Buildout.Core.Configuration;
using Xunit;

namespace Buildout.UnitTests.Configuration;

public class ConfigFlagParserTests
{
    [Theory]
    [InlineData(new[] { "--config", "path.json" }, "path.json", new string[0])]
    [InlineData(new[] { "--config=path.json" }, "path.json", new string[0])]
    [InlineData(new[] { "-c", "path.json" }, "path.json", new string[0])]
    [InlineData(new[] { "-c=path.json" }, "path.json", new string[0])]
    public void Extract_RecognizesAllFlagForms(string[] args, string expectedPath, string[] expectedResidual)
    {
        var (configPath, residual) = ConfigFlagParser.Extract(args);

        Assert.Equal(expectedPath, configPath);
        Assert.Equal(expectedResidual, residual);
    }

    [Fact]
    public void Extract_LastOccurrenceWinsOnDuplicates()
    {
        var args = new[] { "--config", "first.json", "--config", "second.json" };

        var (configPath, residual) = ConfigFlagParser.Extract(args);

        Assert.Equal("second.json", configPath);
        Assert.Empty(residual);
    }

    [Theory]
    [InlineData(new[] { "--config", "path.json", "arg1", "arg2" }, new[] { "arg1", "arg2" })]
    [InlineData(new[] { "arg1", "--config", "path.json", "arg2" }, new[] { "arg1", "arg2" })]
    [InlineData(new[] { "arg1", "arg2", "--config=path.json" }, new[] { "arg1", "arg2" })]
    public void Extract_PreservesResidualArgsOrder(string[] args, string[] expectedResidual)
    {
        var (_, residual) = ConfigFlagParser.Extract(args);

        Assert.Equal(expectedResidual, residual);
    }

    [Theory]
    [InlineData(new[] { "--other-flag", "value" }, new[] { "--other-flag", "value" })]
    [InlineData(new[] { "-x", "value" }, new[] { "-x", "value" })]
    [InlineData(new[] { "--other-flag", "value", "arg1", "arg2" }, new[] { "--other-flag", "value", "arg1", "arg2" })]
    public void Extract_NonConfigFlagsUntouched(string[] args, string[] expectedResidual)
    {
        var (configPath, residual) = ConfigFlagParser.Extract(args);

        Assert.Null(configPath);
        Assert.Equal(expectedResidual, residual);
    }

    [Fact]
    public void Extract_EmptyArgsReturnsNullPathAndReferenceEqualResidual()
    {
        var args = Array.Empty<string>();

        var (configPath, residual) = ConfigFlagParser.Extract(args);

        Assert.Null(configPath);
        Assert.Same(args, residual);
    }

    [Fact]
    public void Extract_ConfigFlagWithEmptyValue()
    {
        var args = new[] { "--config", "" };

        var (configPath, residual) = ConfigFlagParser.Extract(args);

        Assert.Equal("", configPath);
        Assert.Empty(residual);
    }

    [Fact]
    public void Extract_MixedFlagsAndArgs()
    {
        var args = new[] { "-c", "config.json", "--verbose", "input.txt", "--dry-run" };

        var (configPath, residual) = ConfigFlagParser.Extract(args);

        Assert.Equal("config.json", configPath);
        Assert.Equal(new[] { "--verbose", "input.txt", "--dry-run" }, residual);
    }
}