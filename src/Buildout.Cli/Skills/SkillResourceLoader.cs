using System.Reflection;
using System.Text;

namespace Buildout.Cli.Skills;

public static class SkillResourceLoader
{
    private const string ResourcePrefix = "Buildout.Cli.Skills.";

    public static IEnumerable<(string FileName, string Content)> LoadAll()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();

        foreach (var resourceName in resourceNames)
        {
            if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var fileName = resourceName.Substring(ResourcePrefix.Length);
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                continue;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var content = reader.ReadToEnd();

            yield return (fileName, content);
        }
    }
}