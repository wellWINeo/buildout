namespace Buildout.Core.Audit;

public class AuditOptions
{
    public bool Enabled { get; set; }
    public string? Provider { get; set; }
    public string? SqlitePath { get; set; }
    public string? ConnectionString { get; set; }
    public int MaxParameterLength { get; set; } = 10000;
}