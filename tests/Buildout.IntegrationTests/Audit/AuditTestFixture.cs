using Testcontainers.PostgreSql;
using Xunit;

namespace Buildout.IntegrationTests.Audit;

public class AuditTestFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;

    public string SqliteTempPath { get; private set; } = string.Empty;

    /// <summary>
    /// PostgreSQL connection string from a Testcontainers-managed container.
    /// Null when Docker is not available or the container failed to start.
    /// Tests should skip if this property is null.
    /// </summary>
    public string? PostgresConnectionString { get; private set; }

    public async ValueTask InitializeAsync()
    {
        SqliteTempPath = Path.Combine(Path.GetTempPath(), $"buildout_audit_{Guid.NewGuid():N}.sqlite");

        try
        {
            _postgresContainer = new PostgreSqlBuilder()
                .WithDatabase("audit_test")
                .WithUsername("audit_user")
                .WithPassword("audit_pass")
                .Build();

            await _postgresContainer.StartAsync();
            PostgresConnectionString = _postgresContainer.GetConnectionString();
        }
        catch
        {
            // Docker not available — PostgreSQL tests will be skipped.
            _postgresContainer = null;
            PostgresConnectionString = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (File.Exists(SqliteTempPath))
        {
            try { File.Delete(SqliteTempPath); } catch { }
        }

        if (_postgresContainer is not null)
        {
            await _postgresContainer.StopAsync();
            await _postgresContainer.DisposeAsync();
        }
    }
}
