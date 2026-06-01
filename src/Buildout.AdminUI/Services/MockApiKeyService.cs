using Buildout.AdminUI.Models;

namespace Buildout.AdminUI.Services;

public sealed class MockApiKeyService : IApiKeyService
{
    private static readonly IReadOnlyList<ApiKey> _keys =
    [
        new ApiKey
        {
            Id = Guid.Parse("11111111-0000-0000-0000-000000000001"),
            Name = "CI Bot Key",
            Status = ApiKeyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-90),
            LastUsedAt = DateTimeOffset.UtcNow.AddHours(-2)
        },
        new ApiKey
        {
            Id = Guid.Parse("11111111-0000-0000-0000-000000000002"),
            Name = "Deploy Pipeline Key",
            Status = ApiKeyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-60),
            LastUsedAt = DateTimeOffset.UtcNow.AddDays(-1)
        },
        new ApiKey
        {
            Id = Guid.Parse("11111111-0000-0000-0000-000000000003"),
            Name = "Dev Laptop Key",
            Status = ApiKeyStatus.Revoked,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-120),
            LastUsedAt = DateTimeOffset.UtcNow.AddDays(-45)
        },
        new ApiKey
        {
            Id = Guid.Parse("11111111-0000-0000-0000-000000000004"),
            Name = "Staging Tester Key",
            Status = ApiKeyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            LastUsedAt = null
        },
        new ApiKey
        {
            Id = Guid.Parse("11111111-0000-0000-0000-000000000005"),
            Name = "Legacy Integration Key",
            Status = ApiKeyStatus.Revoked,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-365),
            LastUsedAt = DateTimeOffset.UtcNow.AddDays(-180)
        },
        new ApiKey
        {
            Id = Guid.Parse("11111111-0000-0000-0000-000000000006"),
            Name = "Monitoring Service Key",
            Status = ApiKeyStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-14),
            LastUsedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        }
    ];

    public IReadOnlyList<ApiKey> GetAll() => _keys;
}
