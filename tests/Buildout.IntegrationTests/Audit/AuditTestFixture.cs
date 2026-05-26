using Xunit;

namespace Buildout.IntegrationTests.Audit;

public class AuditTestFixture : IAsyncLifetime
{
    public string SqliteTempPath { get; private set; } = string.Empty;
    public string? PostgresConnectionString { get; private set; }

    public async ValueTask InitializeAsync()
    {
        SqliteTempPath = Path.Combine(Path.GetTempPath(), $"buildout_audit_{Guid.NewGuid():N}.sqlite");
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (File.Exists(SqliteTempPath))
        {
            try
            {
                File.Delete(SqliteTempPath);
            }
            catch
            {
            }
        }

        await Task.CompletedTask;
    }
}