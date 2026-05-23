namespace Buildout.Core.Configuration;

internal record BuildoutConfigurationOptions
{
    public string DefaultFilePath { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "buildout",
        "config.json");
    public string Prefix { get; init; } = "Buildout__";
}
