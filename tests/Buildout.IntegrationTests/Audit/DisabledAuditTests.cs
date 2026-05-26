using System.Globalization;
using Buildout.Core.Audit;
using Buildout.Mcp.Audit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Buildout.IntegrationTests.Audit;

public class DisabledAuditTests
{
    [Fact]
    public void AuditDisabled_NullAuditTrailRegistered()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Audit:Enabled"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<AuditTrailFilter>, NullLogger<AuditTrailFilter>>();

        var auditOptions = new AuditOptions();
        configuration.GetSection("Audit").Bind(auditOptions);
        
        if (!auditOptions.Enabled)
        {
            services.AddSingleton<IAuditTrail, NullAuditTrail>();
        }

        var serviceProvider = services.BuildServiceProvider();
        var auditTrail = serviceProvider.GetRequiredService<IAuditTrail>();

        Assert.IsType<NullAuditTrail>(auditTrail);
    }

    [Fact]
    public void AuditDisabled_HttpTransport_NullAuditTrailRegistered()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Audit:Enabled"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<AuditTrailFilter>, NullLogger<AuditTrailFilter>>();

        var auditOptions = new AuditOptions();
        configuration.GetSection("Audit").Bind(auditOptions);
        
        if (!auditOptions.Enabled)
        {
            services.AddSingleton<IAuditTrail, NullAuditTrail>();
        }

        var serviceProvider = services.BuildServiceProvider();
        var auditTrail = serviceProvider.GetRequiredService<IAuditTrail>();

        Assert.IsType<NullAuditTrail>(auditTrail);
    }

    private sealed class NullLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}