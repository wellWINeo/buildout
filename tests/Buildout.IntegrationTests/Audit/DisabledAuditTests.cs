using Buildout.Core.Audit;
using Buildout.Mcp.Audit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Buildout.IntegrationTests.Audit;

public class DisabledAuditTests
{
    [Fact]
    public void AddAuditTrail_WhenDisabledWithStdioTransport_RegistersNullAuditTrail()
    {
        var configuration = BuildConfiguration(enabled: false);
        var services = new ServiceCollection();

        services.AddAuditTrail(configuration, isHttpTransport: false);

        using var sp = services.BuildServiceProvider();
        Assert.IsType<NullAuditTrail>(sp.GetRequiredService<IAuditTrail>());
    }

    [Fact]
    public void AddAuditTrail_WhenDisabledWithHttpTransport_RegistersNullAuditTrail()
    {
        var configuration = BuildConfiguration(enabled: false);
        var services = new ServiceCollection();

        services.AddAuditTrail(configuration, isHttpTransport: true);

        using var sp = services.BuildServiceProvider();
        Assert.IsType<NullAuditTrail>(sp.GetRequiredService<IAuditTrail>());
    }

    [Fact]
    public void AddAuditTrail_WhenEnabledWithStdioTransport_RegistersNullAuditTrail()
    {
        // Audit is explicitly restricted to HTTP transport: even if Enabled=true,
        // stdio transport must yield NullAuditTrail.
        var configuration = BuildConfiguration(enabled: true, provider: "sqlite", sqlitePath: "/tmp/x.db");
        var services = new ServiceCollection();

        services.AddAuditTrail(configuration, isHttpTransport: false);

        using var sp = services.BuildServiceProvider();
        Assert.IsType<NullAuditTrail>(sp.GetRequiredService<IAuditTrail>());
    }

    private static IConfiguration BuildConfiguration(
        bool enabled,
        string? provider = null,
        string? sqlitePath = null)
    {
        var values = new Dictionary<string, string?> { ["Audit:Enabled"] = enabled.ToString(System.Globalization.CultureInfo.InvariantCulture).ToLowerInvariant() };
        if (provider is not null) values["Audit:Provider"] = provider;
        if (sqlitePath is not null) values["Audit:SqlitePath"] = sqlitePath;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
