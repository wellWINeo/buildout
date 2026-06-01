using Buildout.AdminUI.Models;

namespace Buildout.AdminUI.Services;

public sealed class MockAuditLogService : IAuditLogService
{
    private static readonly IReadOnlyList<AuditLogEntry> _entries =
    [
        new AuditLogEntry
        {
            Id = Guid.Parse("22222222-0000-0000-0000-000000000001"),
            Actor = "admin@example.com",
            Action = "CreatePage",
            Resource = "page/home",
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10),
            Details = "Created homepage"
        },
        new AuditLogEntry
        {
            Id = Guid.Parse("22222222-0000-0000-0000-000000000002"),
            Actor = "ci-bot@example.com",
            Action = "RevokeKey",
            Resource = "key/legacy-integration",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-2),
            Details = null
        },
        new AuditLogEntry
        {
            Id = Guid.Parse("22222222-0000-0000-0000-000000000003"),
            Actor = "admin@example.com",
            Action = "UpdatePage",
            Resource = "page/about",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-5),
            Details = "Updated content blocks"
        },
        new AuditLogEntry
        {
            Id = Guid.Parse("22222222-0000-0000-0000-000000000004"),
            Actor = "deploy@example.com",
            Action = "CreateKey",
            Resource = "key/deploy-pipeline",
            Timestamp = DateTimeOffset.UtcNow.AddDays(-1),
            Details = null
        },
        new AuditLogEntry
        {
            Id = Guid.Parse("22222222-0000-0000-0000-000000000005"),
            Actor = "admin@example.com",
            Action = "DeletePage",
            Resource = "page/old-docs",
            Timestamp = DateTimeOffset.UtcNow.AddDays(-2),
            Details = "Removed outdated documentation"
        },
        new AuditLogEntry
        {
            Id = Guid.Parse("22222222-0000-0000-0000-000000000006"),
            Actor = "monitor@example.com",
            Action = "CreateKey",
            Resource = "key/monitoring-service",
            Timestamp = DateTimeOffset.UtcNow.AddDays(-5),
            Details = null
        },
        new AuditLogEntry
        {
            Id = Guid.Parse("22222222-0000-0000-0000-000000000007"),
            Actor = "admin@example.com",
            Action = "UpdatePage",
            Resource = "page/faq",
            Timestamp = DateTimeOffset.UtcNow.AddDays(-7),
            Details = null
        },
        new AuditLogEntry
        {
            Id = Guid.Parse("22222222-0000-0000-0000-000000000008"),
            Actor = "ci-bot@example.com",
            Action = "CreatePage",
            Resource = "page/release-notes",
            Timestamp = DateTimeOffset.UtcNow.AddDays(-10),
            Details = "Automated release notes page"
        },
        new AuditLogEntry
        {
            Id = Guid.Parse("22222222-0000-0000-0000-000000000009"),
            Actor = "admin@example.com",
            Action = "RevokeKey",
            Resource = "key/dev-laptop",
            Timestamp = DateTimeOffset.UtcNow.AddDays(-14),
            Details = "Security rotation"
        },
        new AuditLogEntry
        {
            Id = Guid.Parse("22222222-0000-0000-0000-000000000010"),
            Actor = "deploy@example.com",
            Action = "CreateKey",
            Resource = "key/staging-tester",
            Timestamp = DateTimeOffset.UtcNow.AddDays(-20),
            Details = null
        },
        new AuditLogEntry
        {
            Id = Guid.Parse("22222222-0000-0000-0000-000000000011"),
            Actor = "admin@example.com",
            Action = "CreatePage",
            Resource = "page/getting-started",
            Timestamp = DateTimeOffset.UtcNow.AddDays(-28),
            Details = "Initial setup documentation"
        }
    ];

    public IReadOnlyList<AuditLogEntry> GetAll() => _entries;
}
