using Bunit;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Buildout.AdminUI.Components.Pages;
using Buildout.AdminUI.Models;
using Buildout.AdminUI.Services;

namespace Buildout.AdminUITests;

public sealed class KeysPageTests : BunitContext
{
    public KeysPageTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void KeysPage_RendersDataGridWithKeys()
    {
        var keys = new List<ApiKey>
        {
            new() { Id = Guid.NewGuid(), Name = "CI Bot Key", Status = ApiKeyStatus.Active, CreatedAt = DateTimeOffset.UtcNow.AddDays(-5) }
        };
        Services.AddSingleton<IApiKeyService>(new StubApiKeyService(keys));

        var cut = Render<KeysPage>();

        cut.FindComponent<MudDataGrid<ApiKey>>();
    }

    [Fact]
    public void KeysPage_RendersAllKeyRows()
    {
        var keys = new List<ApiKey>
        {
            new() { Id = Guid.NewGuid(), Name = "Key A", Status = ApiKeyStatus.Active, CreatedAt = DateTimeOffset.UtcNow.AddDays(-5) },
            new() { Id = Guid.NewGuid(), Name = "Key B", Status = ApiKeyStatus.Revoked, CreatedAt = DateTimeOffset.UtcNow.AddDays(-10) }
        };
        Services.AddSingleton<IApiKeyService>(new StubApiKeyService(keys));

        var cut = Render<KeysPage>();

        Assert.Contains("Key A", cut.Markup);
        Assert.Contains("Key B", cut.Markup);
    }

    [Fact]
    public void KeysPage_DisplaysStatusAsText()
    {
        var keys = new List<ApiKey>
        {
            new() { Id = Guid.NewGuid(), Name = "Active Key", Status = ApiKeyStatus.Active, CreatedAt = DateTimeOffset.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Revoked Key", Status = ApiKeyStatus.Revoked, CreatedAt = DateTimeOffset.UtcNow }
        };
        Services.AddSingleton<IApiKeyService>(new StubApiKeyService(keys));

        var cut = Render<KeysPage>();

        Assert.Contains("Active", cut.Markup);
        Assert.Contains("Revoked", cut.Markup);
    }

    [Fact]
    public void KeysPage_ShowsEmptyStateWhenNoKeys()
    {
        Services.AddSingleton<IApiKeyService>(new StubApiKeyService([]));
        var cut = Render<KeysPage>();
        Assert.Contains("No API keys found.", cut.Markup);
    }

    private sealed class StubApiKeyService(IReadOnlyList<ApiKey> keys) : IApiKeyService
    {
        public IReadOnlyList<ApiKey> GetAll() => keys;
    }
}
