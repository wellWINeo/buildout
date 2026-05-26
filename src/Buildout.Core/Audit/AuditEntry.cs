using System.Text.Json;

namespace Buildout.Core.Audit;

public enum AuditOutcome
{
    Success = 0,
    Failure = 1
}

public record AuditEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string ToolName { get; init; }
    public string? SessionId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public required string Parameters { get; init; }
    public AuditOutcome Outcome { get; init; }
    public TimeSpan Duration { get; init; }
    public string? ErrorDetails { get; init; }

    public static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "{}";
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, maxLength - 3), "...");
    }

    public static string SerializeParameters(Dictionary<string, JsonElement> parameters)
    {
        var json = JsonSerializer.Serialize(parameters);
        return Truncate(json, 10000);
    }
}