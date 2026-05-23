using System.Reflection;
using System.Text;
using Buildout.Core.Buildin;
using Buildout.Core.Diagnostics;
using Buildout.Core.Markdown.Editing;
using Xunit;

namespace Buildout.UnitTests.Configuration;

public class DocumentationKeysTests
{
    [Fact]
    public void ConfigurationDocs_ContainExactKeySet_FromOptionsClasses()
    {
        var docsPath = FindDocsConfigurationMd();
        var expectedKeys = GetExpectedKeysFromOptionsClasses();
        var documentedKeys = ParseKeyTableFromDocs(docsPath);

        var missingInDocs = expectedKeys.Except(documentedKeys).OrderBy(k => k).ToArray();
        var extraInDocs = documentedKeys.Except(expectedKeys).OrderBy(k => k).ToArray();

        if (missingInDocs.Length == 0 && extraInDocs.Length == 0)
            return;

        var message = new StringBuilder();
        message.AppendLine("Configuration key mismatch between code and documentation:");

        if (missingInDocs.Length > 0)
        {
            message.AppendLine();
            message.AppendLine("Keys in code but missing from documentation:");
            foreach (var key in missingInDocs)
                message.AppendLine("  - " + key);
        }

        if (extraInDocs.Length > 0)
        {
            message.AppendLine();
            message.AppendLine("Keys in documentation but not in code:");
            foreach (var key in extraInDocs)
                message.AppendLine("  - " + key);
        }

        Assert.Fail(message.ToString());
    }

    private static string FindDocsConfigurationMd()
    {
        var baseDir = AppContext.BaseDirectory;
        var currentDir = new DirectoryInfo(baseDir);

        while (currentDir != null)
        {
            var docsPath = Path.Combine(currentDir.FullName, "docs", "configuration.md");
            if (File.Exists(docsPath))
                return docsPath;

            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException("Could not find docs/configuration.md starting from " + baseDir);
    }

    private static HashSet<string> GetExpectedKeysFromOptionsClasses()
    {
        var keys = new HashSet<string>();

        var buildinProps = typeof(BuildinClientOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in buildinProps)
        {
            if (prop.Name == "Http")
            {
                var httpProps = prop.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var httpProp in httpProps)
                {
                    var key = MapPropertyToConfigKey(httpProp.Name, "Http");
                    keys.Add(key);
                }
            }
            else
            {
                var key = MapPropertyToConfigKey(prop.Name, null);
                keys.Add(key);
            }
        }

        var telemetryProps = typeof(TelemetryOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in telemetryProps)
        {
            var key = MapPropertyToConfigKey(prop.Name, "Telemetry");
            keys.Add(key);
        }

        var limitationsProps = typeof(LimitationsOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in limitationsProps)
        {
            var key = MapPropertyToConfigKey(prop.Name, "Limitations");
            keys.Add(key);
        }

        return keys;
    }

    private static string MapPropertyToConfigKey(string propertyName, string? section)
    {
        switch (propertyName)
        {
            case "HttpTimeout":
                return "Http:Timeout";
            case "UnsafeAllowInsecure":
                return "Http:UnsafeAllowInsecure";
            default:
                return string.IsNullOrEmpty(section) ? propertyName : section + ":" + propertyName;
        }
    }

    private static HashSet<string> ParseKeyTableFromDocs(string docsPath)
    {
        var lines = File.ReadAllLines(docsPath);
        var keys = new HashSet<string>();
        var inTable = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (!inTable)
            {
                if (line.Contains("| Key |") && line.Contains("| Type |") && line.Contains("| Default |"))
                {
                    inTable = true;
                }
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                inTable = false;
                continue;
            }

            if (line.Length == 0 || line[0] != '|')
                continue;

            var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;

            var keyPart = parts[0].Trim();
            if (keyPart == "Key" || keyPart.Contains("---"))
                continue;

            var keyParts = keyPart.Split('`');
            var key = keyParts.Length > 1 ? keyParts[1].Trim() : keyPart;
            if (!string.IsNullOrWhiteSpace(key))
                keys.Add(key);
        }

        return keys;
    }
}