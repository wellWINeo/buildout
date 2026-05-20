using System.Text.Json;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.PageLifecycle;
using Xunit;

namespace Buildout.UnitTests.PageLifecycle;

public sealed class PageLifecycleOutcomeTests
{
    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new PageLifecycleOutcome { PageId = "p1", Archived = true, Changed = true };
        var b = new PageLifecycleOutcome { PageId = "p1", Archived = true, Changed = true };
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void RecordEquality_DifferentValues_NotEqual()
    {
        var a = new PageLifecycleOutcome { PageId = "p1", Archived = true, Changed = true };
        var b = new PageLifecycleOutcome { PageId = "p1", Archived = false, Changed = true };
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void JsonRoundTrip_PreservesNonNullFields()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var original = new PageLifecycleOutcome { PageId = "p1", Archived = true, Changed = true };
        var json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<PageLifecycleOutcome>(json, options);
        Assert.NotNull(deserialized);
        Assert.Equal(original.PageId, deserialized.PageId);
        Assert.Equal(original.Archived, deserialized.Archived);
        Assert.Equal(original.Changed, deserialized.Changed);
    }

    [Fact]
    public void StateInvariant_FailureClassNull_ArchivedNotNull()
    {
        var outcome = new PageLifecycleOutcome { PageId = "p1", Archived = true, Changed = true, FailureClass = null };
        Assert.Null(outcome.FailureClass);
        Assert.NotNull(outcome.Archived);
    }

    [Fact]
    public void StateInvariant_ChangedTrue_FailureClassNull()
    {
        var outcome = new PageLifecycleOutcome { PageId = "p1", Archived = true, Changed = true };
        Assert.True(outcome.Changed);
        Assert.Null(outcome.FailureClass);
    }

    [Fact]
    public void StateInvariant_FailureClassSet_UnderlyingExceptionSet()
    {
        var ex = new InvalidOperationException("test");
        var outcome = new PageLifecycleOutcome { PageId = "p1", Changed = false, FailureClass = FailureClass.NotFound, UnderlyingException = ex };
        Assert.NotNull(outcome.FailureClass);
        Assert.NotNull(outcome.UnderlyingException);
    }
}
