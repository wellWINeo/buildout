using Buildout.Core.Audit;
using System.Text.Json;
using Xunit;

namespace Buildout.UnitTests.Audit;

public class AuditEntryTests
{
    [Fact]
    public void Truncate_ReturnsEmptyBracesForNull()
    {
        var result = AuditEntry.Truncate(null, 100);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void Truncate_ReturnsEmptyBracesForEmptyString()
    {
        var result = AuditEntry.Truncate("", 100);
        Assert.Equal("{}", result);
    }

    [Fact]
    public void Truncate_ReturnsOriginalStringIfUnderLimit()
    {
        var input = "{\"test\":\"value\"}";
        var result = AuditEntry.Truncate(input, 100);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Truncate_AddsEllipsisWhenOverLimit()
    {
        var input = new string('a', 20);
        var result = AuditEntry.Truncate(input, 10);
        Assert.Equal(10, result.Length);
        Assert.EndsWith("...", result);
    }

    [Fact]
    public void Truncate_TruncatesExactlyToMaxLengthWithEllipsis()
    {
        var input = new string('a', 100);
        var maxLength = 20;
        var result = AuditEntry.Truncate(input, maxLength);
        Assert.Equal(maxLength, result.Length);
        Assert.StartsWith("aaaaaaa", result);
        Assert.EndsWith("...", result);
    }

    [Fact]
    public void SerializeParameters_TruncatesLongParameters()
    {
        var json = "{\"long\":\"" + new string('x', 15000) + "\"}";
        var result = AuditEntry.Truncate(json, 10000);

        Assert.Equal(10000, result.Length);
        Assert.EndsWith("...", result);
    }

    [Fact]
    public void SerializeParameters_PreservesShortParameters()
    {
        var json = "{\"test\":\"value\"}";
        var result = AuditEntry.Truncate(json, 100);

        Assert.Equal(json, result);
    }
}