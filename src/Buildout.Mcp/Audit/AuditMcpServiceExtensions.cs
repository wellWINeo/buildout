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
            new Linq2DbAuditTrail(connectionString, auditOptions.Provider!, sp.GetRequiredService<ILogger<Linq2DbAuditTrail>>()));

        var finalServiceProvider = services.BuildServiceProvider();
        using (var scope = finalServiceProvider.CreateScope())
        {
            var migrationRunner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
            migrationRunner.MigrateUp();
        }
        
        return services;
    }
}