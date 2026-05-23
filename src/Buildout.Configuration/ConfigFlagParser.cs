namespace Buildout.Configuration;

public static class ConfigFlagParser
{
    public static (string? ConfigPath, string[] Residual) Extract(string[] args)
    {
        if (args.Length == 0)
        {
            return (null, args);
        }

        var residualList = new List<string>();
        string? configPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (TryParseConfigFlag(arg, out var extractedPath, out var skipNext))
            {
                configPath = extractedPath;
                if (skipNext)
                {
                    if (i + 1 < args.Length)
                    {
                        configPath = args[i + 1];
                    }
                    i++;
                }
            }
            else
            {
                residualList.Add(arg);
            }
        }

        return (configPath, residualList.ToArray());
    }

    private static bool TryParseConfigFlag(string arg, out string? configPath, out bool skipNext)
    {
        configPath = null;
        skipNext = false;

        if (arg == "--config" || arg == "-c")
        {
            configPath = null;
            skipNext = true;
            return true;
        }

        if (arg.StartsWith("--config=", StringComparison.Ordinal))
        {
            configPath = arg["--config=".Length..];
            skipNext = false;
            return true;
        }

        if (arg.StartsWith("-c=", StringComparison.Ordinal))
        {
            configPath = arg["-c=".Length..];
            skipNext = false;
            return true;
        }

        return false;
    }
}
