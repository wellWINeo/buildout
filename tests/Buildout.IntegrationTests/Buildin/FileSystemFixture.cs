namespace Buildout.IntegrationTests.Buildin;

public sealed class FileSystemFixture
{
    /// <summary>
    /// Creates a temp directory on the real filesystem with automatic cleanup.
    /// </summary>
    public static TempDirectory CreateTempDirectory() => new();

    public static IReadOnlyDictionary<string, string> Manifest(string root)
        => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(path => Path.GetRelativePath(root, path), path => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);
}

public sealed class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"buildout-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(Path);
    }

    public void WriteJsonFile(string relativePath, string jsonContent)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        File.WriteAllText(fullPath, jsonContent);
    }

    public void WriteTextFile(string relativePath, string content)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        File.WriteAllText(fullPath, content);
    }

    public void DeleteFile(string relativePath)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
