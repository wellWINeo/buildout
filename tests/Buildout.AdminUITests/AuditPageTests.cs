using Bunit;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Buildout.AdminUI.Components.Pages;
using Buildout.AdminUI.Models;
using Buildout.AdminUI.Services;

namespace Buildout.AdminUITests;

public sealed class AuditPageTests : BunitContext
{
    public AuditPageTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void AuditPage_RendersDataGridWithEntries()
    {
        var entries = new List<AuditLogEntry>
        {
            new() { Id = Guid.NewGuid(), Actor = "admin@example.com", Action = "CreatePage", Resource = "page/home", Timestamp = DateTimeOffset.UtcNow }
        };
        Services.AddSingleton<IAuditLogService>(new StubAuditLogService(entries));

        var cut = Render<AuditPage>();

        cut.FindComponent<MudDataGrid<AuditLogEntry>>();
    }

    [Fact]
    public void AuditPage_RendersAllEntryRows()
    {
        var entries = new List<AuditLogEntry>
        {
            new() { Id = Guid.NewGuid(), Actor = "alice@example.com", Action = "CreatePage", Resource = "page/a", Timestamp = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Actor = "bob@example.com", Action = "RevokeKey", Resource = "key/b", Timestamp = DateTimeOffset.UtcNow.AddHours(-1) }
        };
        Services.AddSingleton<IAuditLogService>(new StubAuditLogService(entries));

        var cut = Render<AuditPage>();

        Assert.Contains("alice@example.com", cut.Markup);
        Assert.Contains("bob@example.com", cut.Markup);
    }

    [Fact]
    public void AuditPage_RendersEntriesNewestFirst()
    {
        var older = new AuditLogEntry { Id = Guid.NewGuid(), Actor = "old@example.com", Action = "OldAction", Resource = "x", Timestamp = DateTimeOffset.UtcNow.AddHours(-10) };
        var newer = new AuditLogEntry { Id = Guid.NewGuid(), Actor = "new@example.com", Action = "NewAction", Resource = "y", Timestamp = DateTimeOffset.UtcNow };
        Services.AddSingleton<IAuditLogService>(new StubAuditLogService([older, newer]));

        var cut = Render<AuditPage>();
        var markup = cut.Markup;

        var posOlder = markup.IndexOf("OldAction", StringComparison.Ordinal);
        var posNewer = markup.IndexOf("NewAction", StringComparison.Ordinal);
        Assert.True(posNewer < posOlder, "Newer entries should appear before older entries.");
    }

    [Fact]
    public void AuditPage_ShowsEmptyStateWhenNoEntries()
    {
        Services.AddSingleton<IAuditLogService>(new StubAuditLogService([]));
        var cut = Render<AuditPage>();
        Assert.Contains("No audit log entries found.", cut.Markup);
    }

    private sealed class StubAuditLogService(IReadOnlyList<AuditLogEntry> entries) : IAuditLogService
    {
        public IReadOnlyList<AuditLogEntry> GetAll() => entries;
    }
}
