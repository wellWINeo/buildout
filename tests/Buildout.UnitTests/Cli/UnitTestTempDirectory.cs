namespace Buildout.UnitTests.Cli;

/// <summary>Temp directory for CLI unit tests with CWD tracking.</summary>
internal sealed class UnitTestTempDirectory : IDisposable
{
    public string Path { get; }
    public string OriginalDir { get; }

    public UnitTestTempDirectory()
    {
        OriginalDir = Directory.GetCurrentDirectory();
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"buildout-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(Path);
        Directory.SetCurrentDirectory(Path);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(OriginalDir);
        try { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); }
        catch { /* best-effort */ }
    }
}
