using Buildout.Core.Audit;
using Buildout.Mcp.Audit.Migrations;
using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace Buildout.Mcp.Audit;

public static class AuditMcpServiceExtensions
{
    /// <summary>
    /// Registers audit trail services.  When both <paramref name="isHttpTransport"/> and
    /// <c>Audit:Enabled</c> are true, this registers:
    /// <list type="bullet">
    ///   <item><see cref="AdoNetAuditTrail"/> as <see cref="IAuditTrail"/></item>
    ///   <item><see cref="AuditTrailFilter"/> as <see cref="IConfigureOptions{McpServerOptions}"/>
    ///         so the filter is wired into the MCP pipeline after DI is built</item>
    ///   <item>FluentMigrator runner (migrations are executed by the caller after
    ///         <c>IHost.Build()</c> — see <c>Program.cs</c>)</item>
    /// </list>
    /// When either condition is false, <see cref="NullAuditTrail"/> is registered instead
    /// and no database or filter infrastructure is set up.
    /// </summary>
    public static IServiceCollection AddAuditTrail(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isHttpTransport)
    {
        services.AddOptions<AuditOptions>()
            .Bind(configuration.GetSection("Audit"))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AuditOptions>, AuditOptionsValidator>();
        services.AddLogging();

        var auditOptions = new AuditOptions();
        configuration.GetSection("Audit").Bind(auditOptions);

        if (!auditOptions.Enabled || !isHttpTransport)
        {
            services.AddSingleton<IAuditTrail, NullAuditTrail>();
            return services;
        }

        var connectionString = auditOptions.Provider == "sqlite"
            ? $"Data Source={auditOptions.SqlitePath};BusyTimeout=5000"
            : auditOptions.ConnectionString!;

        // FluentMigrator runner — migrations are run by the caller in Program.cs
        // after IHost.Build(), not here, to avoid the BuildServiceProvider anti-pattern.
        services.AddFluentMigratorCore()
            .ConfigureRunner(builder =>
            {
                if (auditOptions.Provider == "sqlite")
                {
                    builder
                        .AddSQLite()
                        .WithGlobalConnectionString(connectionString)
                        .ScanIn(typeof(Migration_001_CreateAuditEntries).Assembly)
                        .For.Migrations();
                }
                else
                {
                    builder
                        .AddPostgres()
                        .WithGlobalConnectionString(connectionString)
                        .ScanIn(typeof(Migration_001_CreateAuditEntries).Assembly)
                        .For.Migrations();
                }
            })
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        services.AddSingleton<IAuditTrail>(sp =>
            new AdoNetAuditTrail(
                connectionString,
                auditOptions.Provider!,
                sp.GetRequiredService<ILogger<AdoNetAuditTrail>>()));

        // IHttpContextAccessor supplies the per-request Mcp-Session-Id header value.
        services.AddHttpContextAccessor();

        // AuditTrailFilter implements IConfigureOptions<McpServerOptions>: it adds
        // itself to McpServerOptions.Filters.Request.CallToolFilters when options are
        // resolved (after DI is fully built), avoiding any intermediate container.
        services.AddSingleton<IConfigureOptions<McpServerOptions>, AuditTrailFilter>();

        return services;
    }
}
