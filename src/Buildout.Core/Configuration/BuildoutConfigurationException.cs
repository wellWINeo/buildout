namespace Buildout.Core.Configuration;

public class BuildoutConfigurationException : Exception
{
    public string? Path { get; }

    public BuildoutConfigurationException(string message) : base(message)
    {
    }

    public BuildoutConfigurationException(string message, string? path) : base(message)
    {
        Path = path;
    }

    public BuildoutConfigurationException(string message, string? path, Exception? innerException) : base(message, innerException)
    {
        Path = path;
    }

    public BuildoutConfigurationException(string message, Exception? innerException) : base(message, innerException)
    {
    }
}