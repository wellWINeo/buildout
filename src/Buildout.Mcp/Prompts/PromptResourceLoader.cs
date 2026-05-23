using System.Reflection;
using System.Text;

namespace Buildout.Mcp.Prompts;

public static class PromptResourceLoader
{
    private const string ResourcePrefix = "Buildout.Mcp.Prompts.";

    public static string Load(string name)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"{ResourcePrefix}{name}.md";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Embedded prompt resource not found: {resourceName}");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}