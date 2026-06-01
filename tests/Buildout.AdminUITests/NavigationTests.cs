using Bunit;
using Xunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Buildout.AdminUI.Components.Layout;

namespace Buildout.AdminUITests;

public sealed class NavigationTests : BunitContext
{
    public NavigationTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void MainLayout_RendersTwoTabsWithCorrectLabels()
    {
        var cut = Render<MainLayout>(p => p
            .Add(l => l.Body, b => b.AddMarkupContent(0, "<p>content</p>")));

        var tabs = cut.FindComponents<MudTabPanel>();
        Assert.Equal(2, tabs.Count);
        Assert.Contains(tabs, t => t.Instance.Text == "Keys Management");
        Assert.Contains(tabs, t => t.Instance.Text == "Audit Logs");
    }

    [Fact]
    public void NavigatingToKeys_ActivatesFirstTab()
    {
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("http://localhost/keys");

        var cut = Render<MainLayout>(p => p
            .Add(l => l.Body, b => b.AddMarkupContent(0, "<p>content</p>")));

        var mudTabs = cut.FindComponent<MudTabs>();
        Assert.Equal(0, mudTabs.Instance.ActivePanelIndex);
    }

    [Fact]
    public void NavigatingToAudit_ActivatesSecondTab()
    {
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("http://localhost/audit");

        var cut = Render<MainLayout>(p => p
            .Add(l => l.Body, b => b.AddMarkupContent(0, "<p>content</p>")));

        var mudTabs = cut.FindComponent<MudTabs>();
        Assert.Equal(1, mudTabs.Instance.ActivePanelIndex);
    }
}
