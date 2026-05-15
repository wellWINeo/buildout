using System.Diagnostics.Metrics;
using Buildout.Core.Diagnostics;
using Xunit;

namespace Buildout.UnitTests.Diagnostics;

public sealed class BuildoutMeterTests
{
    [Fact]
    public void Meter_HasCorrectNameAndVersion()
    {
        Assert.Equal("Buildout", BuildoutMeter.Meter.Name);
        Assert.Equal("1.0.0", BuildoutMeter.Meter.Version);
    }

    [Theory]
    [InlineData(nameof(BuildoutMeter.OperationsTotal), "buildout.operations.total", "{operation}")]
    [InlineData(nameof(BuildoutMeter.ApiCallsTotal), "buildout.api.calls.total", "{call}")]
    [InlineData(nameof(BuildoutMeter.BlocksProcessedTotal), "buildout.blocks.processed.total", "{block}")]
    [InlineData(nameof(BuildoutMeter.SearchResultsTotal), "buildout.search.results.total", "{result}")]
    [InlineData(nameof(BuildoutMeter.PagesCreatedTotal), "buildout.pages.created.total", "{page}")]
    [InlineData(nameof(BuildoutMeter.DatabaseViewRendersTotal), "buildout.database.view.renders.total", "{render}")]
    [InlineData(nameof(BuildoutMeter.McpToolInvocationsTotal), "buildout.mcp.tool.invocations.total", "{invocation}")]
    [InlineData(nameof(BuildoutMeter.McpResourceReadsTotal), "buildout.mcp.resource.reads.total", "{read}")]
    public void Counter_Instruments_HaveCorrectNameAndUnit(string propertyName, string expectedName, string expectedUnit)
    {
        var instrument = GetInstrument(propertyName);
        Assert.NotNull(instrument);
        Assert.Equal(expectedName, instrument.Name);
        Assert.Equal(expectedUnit, instrument.Unit);
    }

    [Theory]
    [InlineData(nameof(BuildoutMeter.OperationDuration), "buildout.operation.duration", "s")]
    [InlineData(nameof(BuildoutMeter.ApiCallDuration), "buildout.api.call.duration", "s")]
    [InlineData(nameof(BuildoutMeter.McpToolDuration), "buildout.mcp.tool.duration", "s")]
    public void Histogram_Instruments_HaveCorrectNameAndUnit(string propertyName, string expectedName, string expectedUnit)
    {
        var instrument = GetInstrument(propertyName);
        Assert.NotNull(instrument);
        Assert.Equal(expectedName, instrument.Name);
        Assert.Equal(expectedUnit, instrument.Unit);
    }

    [Fact]
    public void AllInstruments_BelongToBuildoutMeter()
    {
        var instruments = new Instrument[]
        {
            BuildoutMeter.OperationsTotal,
            BuildoutMeter.OperationDuration,
            BuildoutMeter.ApiCallsTotal,
            BuildoutMeter.ApiCallDuration,
            BuildoutMeter.BlocksProcessedTotal,
            BuildoutMeter.SearchResultsTotal,
            BuildoutMeter.PagesCreatedTotal,
            BuildoutMeter.DatabaseViewRendersTotal,
            BuildoutMeter.McpToolInvocationsTotal,
            BuildoutMeter.McpToolDuration,
            BuildoutMeter.McpResourceReadsTotal,
        };

        Assert.All(instruments, instrument =>
        {
            Assert.NotNull(instrument);
            Assert.Same(BuildoutMeter.Meter, instrument.Meter);
        });
    }

    private static Instrument GetInstrument(string fieldName)
    {
        var field = typeof(BuildoutMeter).GetField(fieldName);
        Assert.NotNull(field);
        return (Instrument)field!.GetValue(null)!;
    }
}
