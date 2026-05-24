using Buildout.Core.Buildin;
using Buildout.Core.Buildin.Errors;
using Buildout.Core.Buildin.Models;
using Buildout.Core.Caching;
using Buildout.Core.Markdown.Authoring;
using Buildout.Core.PageLifecycle;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

using PageModel = Buildout.Core.Buildin.Models.Page;

namespace Buildout.UnitTests.PageLifecycle;

[Collection("MetricsTests")]
public sealed class PageLifecycleTests
{
    private readonly IBuildinClient _client = Substitute.For<IBuildinClient>();
    private readonly TestLogger _logger = new();
    private readonly IPageReadCache _cache = Substitute.For<IPageReadCache>();
    private readonly Buildout.Core.PageLifecycle.PageLifecycle _sut;

    private const string PageId = "test-page-id";

    public PageLifecycleTests()
    {
        _sut = new Buildout.Core.PageLifecycle.PageLifecycle(_client, _cache, _logger);
    }

    private static PageModel ActivePage() => new() { Id = PageId, Archived = false };

    private static PageModel ArchivedPage() => new() { Id = PageId, Archived = true };

    [Fact]
    public async Task HappyPath_StateChange()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>()).Returns(ActivePage());
        _client.UpdatePageAsync(PageId, Arg.Any<UpdatePageRequest>(), Arg.Any<CancellationToken>())
            .Returns(ArchivedPage());

        var outcome = await _sut.DeleteAsync(PageId);

        await _client.Received(1).GetPageAsync(PageId, Arg.Any<CancellationToken>());
        await _client.Received(1).UpdatePageAsync(PageId, Arg.Any<UpdatePageRequest>(), Arg.Any<CancellationToken>());
        Assert.True(outcome.Archived);
        Assert.True(outcome.Changed);
        Assert.Null(outcome.FailureClass);
    }

    [Fact]
    public async Task NoOp_AlreadyArchived()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>()).Returns(ArchivedPage());

        var outcome = await _sut.DeleteAsync(PageId);

        await _client.Received(1).GetPageAsync(PageId, Arg.Any<CancellationToken>());
        await _client.DidNotReceive().UpdatePageAsync(Arg.Any<string>(), Arg.Any<UpdatePageRequest>(), Arg.Any<CancellationToken>());
        Assert.True(outcome.Archived);
        Assert.False(outcome.Changed);
        Assert.Null(outcome.FailureClass);
    }

    [Fact]
    public async Task PatchBody_OnlyArchivedFieldSet()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>()).Returns(ActivePage());
        _client.UpdatePageAsync(PageId, Arg.Any<UpdatePageRequest>(), Arg.Any<CancellationToken>())
            .Returns(ArchivedPage());

        await _sut.DeleteAsync(PageId);

        await _client.Received(1).UpdatePageAsync(
            PageId,
            Arg.Is<UpdatePageRequest>(req =>
                req.Archived == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FailureClass_NotFound_OnGet()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BuildinApiException(new ApiError(404, "not_found", "Page not found", null)));

        var outcome = await _sut.DeleteAsync(PageId);

        Assert.Equal(FailureClass.NotFound, outcome.FailureClass);
    }

    [Fact]
    public async Task FailureClass_Auth_OnGet()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BuildinApiException(new ApiError(401, "unauthorized", "Unauthorized", null)));

        var outcome = await _sut.DeleteAsync(PageId);

        Assert.Equal(FailureClass.Auth, outcome.FailureClass);
    }

    [Fact]
    public async Task FailureClass_Transport_OnGet()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BuildinApiException(new TransportError(new HttpRequestException("Connection refused"))));

        var outcome = await _sut.DeleteAsync(PageId);

        Assert.Equal(FailureClass.Transport, outcome.FailureClass);
    }

    [Fact]
    public async Task FailureClass_Unexpected_5xx_OnGet()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BuildinApiException(new ApiError(500, "internal_error", "Server error", null)));

        var outcome = await _sut.DeleteAsync(PageId);

        Assert.Equal(FailureClass.Unexpected, outcome.FailureClass);
    }

    [Fact]
    public async Task FailureClass_UnknownError_OnGet()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BuildinApiException(new UnknownError(502, "bad gateway")));

        var outcome = await _sut.DeleteAsync(PageId);

        Assert.Equal(FailureClass.Unexpected, outcome.FailureClass);
    }

    [Fact]
    public async Task FailureClass_NotFound_OnPatch()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>()).Returns(ActivePage());
        _client.UpdatePageAsync(PageId, Arg.Any<UpdatePageRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BuildinApiException(new ApiError(404, "not_found", "Page not found", null)));

        var outcome = await _sut.DeleteAsync(PageId);

        Assert.Equal(FailureClass.NotFound, outcome.FailureClass);
    }

    [Fact]
    public async Task FailureClass_Auth_OnPatch()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>()).Returns(ActivePage());
        _client.UpdatePageAsync(PageId, Arg.Any<UpdatePageRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BuildinApiException(new ApiError(401, "unauthorized", "Unauthorized", null)));

        var outcome = await _sut.DeleteAsync(PageId);

        Assert.Equal(FailureClass.Auth, outcome.FailureClass);
    }

    [Fact]
#pragma warning disable CA1873
    public async Task OperationRecorder_DeleteOperationName()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>()).Returns(ActivePage());
        _client.UpdatePageAsync(PageId, Arg.Any<UpdatePageRequest>(), Arg.Any<CancellationToken>())
            .Returns(ArchivedPage());

        await _sut.DeleteAsync(PageId);

        var completedEntry = _logger.Entries.FirstOrDefault(e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("page_delete") &&
            e.Message.Contains("completed"));

        Assert.NotNull(completedEntry);
    }
#pragma warning restore CA1873

    [Fact]
    public async Task Restore_HappyPath_StateChange()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>()).Returns(ArchivedPage());
        _client.UpdatePageAsync(PageId, Arg.Any<UpdatePageRequest>(), Arg.Any<CancellationToken>())
            .Returns(ActivePage());

        var outcome = await _sut.RestoreAsync(PageId);

        await _client.Received(1).GetPageAsync(PageId, Arg.Any<CancellationToken>());
        await _client.Received(1).UpdatePageAsync(PageId, Arg.Any<UpdatePageRequest>(), Arg.Any<CancellationToken>());
        Assert.False(outcome.Archived);
        Assert.True(outcome.Changed);
        Assert.Null(outcome.FailureClass);
    }

    [Fact]
    public async Task Restore_NoOp_AlreadyActive()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>()).Returns(ActivePage());

        var outcome = await _sut.RestoreAsync(PageId);

        await _client.Received(1).GetPageAsync(PageId, Arg.Any<CancellationToken>());
        await _client.DidNotReceive().UpdatePageAsync(Arg.Any<string>(), Arg.Any<UpdatePageRequest>(), Arg.Any<CancellationToken>());
        Assert.False(outcome.Archived);
        Assert.False(outcome.Changed);
        Assert.Null(outcome.FailureClass);
    }

    [Fact]
    public async Task Restore_PatchBody_ArchivedFalse()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>()).Returns(ArchivedPage());
        _client.UpdatePageAsync(PageId, Arg.Any<UpdatePageRequest>(), Arg.Any<CancellationToken>())
            .Returns(ActivePage());

        await _sut.RestoreAsync(PageId);

        await _client.Received(1).UpdatePageAsync(
            PageId,
            Arg.Is<UpdatePageRequest>(req => req.Archived == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Restore_FailureClass_NotFound_OnGet()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new BuildinApiException(new ApiError(404, "not_found", "Page not found", null)));

        var outcome = await _sut.RestoreAsync(PageId);

        Assert.Equal(FailureClass.NotFound, outcome.FailureClass);
    }

    [Fact]
    public async Task Restore_FailureClass_Auth_OnPatch()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>()).Returns(ArchivedPage());
        _client.UpdatePageAsync(PageId, Arg.Any<UpdatePageRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BuildinApiException(new ApiError(401, "unauthorized", "Unauthorized", null)));

        var outcome = await _sut.RestoreAsync(PageId);

        Assert.Equal(FailureClass.Auth, outcome.FailureClass);
    }

    [Fact]
    public async Task Restore_OperationRecorder_RestoreOperationName()
    {
        _client.GetPageAsync(PageId, Arg.Any<CancellationToken>()).Returns(ArchivedPage());
        _client.UpdatePageAsync(PageId, Arg.Any<UpdatePageRequest>(), Arg.Any<CancellationToken>())
            .Returns(ActivePage());

        await _sut.RestoreAsync(PageId);

        var completedEntry = _logger.Entries.FirstOrDefault(e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("page_restore") &&
            e.Message.Contains("completed"));

        Assert.NotNull(completedEntry);
    }

    private sealed class TestLogger : ILogger<Buildout.Core.PageLifecycle.PageLifecycle>
    {
        public List<LogEntry> Entries { get; } = [];

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            Entries.Add(new LogEntry(logLevel, message));
        }

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }

    private sealed record LogEntry(LogLevel Level, string Message);
}
