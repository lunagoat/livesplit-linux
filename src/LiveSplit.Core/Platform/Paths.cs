using System;
using System.IO;

namespace LiveSplit.Hosting;

public static class Paths
{
    public const string AppDirectoryName = "LiveSplit";
    public const string PortableMarkerFileName = "portable";
    public const string SettingsFileName = "settings.cfg";

    private static string configDirectoryOverride;
    private static string dataDirectoryOverride;
    private static string cacheDirectoryOverride;
    private static bool? portableOverride;

    public static string ExecutableDirectory
    {
        get
        {
            string dir = AppContext.BaseDirectory;
            return string.IsNullOrEmpty(dir) ? Directory.GetCurrentDirectory() : dir.TrimEnd(Path.DirectorySeparatorChar);
        }
    }

    public static bool IsPortable
    {
        get
        {
            if (portableOverride.HasValue)
            {
                return portableOverride.Value;
            }

            string dir = ExecutableDirectory;
            return File.Exists(Path.Combine(dir, PortableMarkerFileName))
                || File.Exists(Path.Combine(dir, SettingsFileName));
        }
    }

    public static string ConfigDirectory
    {
        get
        {
            if (configDirectoryOverride != null)
            {
                return configDirectoryOverride;
            }

            return IsPortable
                ? ExecutableDirectory
                : Path.Combine(XdgDirectory("XDG_CONFIG_HOME", ".config"), AppDirectoryName);
        }
    }

    public static string DataDirectory
    {
        get
        {
            if (dataDirectoryOverride != null)
            {
                return dataDirectoryOverride;
            }

            return IsPortable
                ? ExecutableDirectory
                : Path.Combine(XdgDirectory("XDG_DATA_HOME", ".local/share"), AppDirectoryName);
        }
    }

    public static string CacheDirectory
    {
        get
        {
            if (cacheDirectoryOverride != null)
            {
                return cacheDirectoryOverride;
            }

            return IsPortable
                ? Path.Combine(ExecutableDirectory, "cache")
                : Path.Combine(XdgDirectory("XDG_CACHE_HOME", ".cache"), AppDirectoryName);
        }
    }

    public static string LogDirectory
    {
        get
        {
            if (configDirectoryOverride != null)
            {
                return Path.Combine(configDirectoryOverride, "logs");
            }

            return IsPortable
                ? Path.Combine(ExecutableDirectory, "logs")
                : Path.Combine(XdgDirectory("XDG_STATE_HOME", ".local/state"), AppDirectoryName);
        }
    }

    public static string RuntimeDirectory
    {
        get
        {
            string dir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            return !string.IsNullOrEmpty(dir) && Directory.Exists(dir) ? dir : Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        }
    }

    public static string SettingsFile => Path.Combine(ConfigDirectory, SettingsFileName);

    public static string ComponentsDirectory => Path.Combine(ExecutableDirectory, "Components");

    public static string ResourcesDirectory => Path.Combine(ExecutableDirectory, "Resources");

    public static string CommandSocketPath => Path.Combine(RuntimeDirectory, "livesplit.sock");

    public static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    public static void SetDirectories(string configDirectory, string dataDirectory, string cacheDirectory = null)
    {
        configDirectoryOverride = configDirectory;
        dataDirectoryOverride = dataDirectory;
        cacheDirectoryOverride = cacheDirectory;
    }

    public static void SetPortable(bool? portable)
    {
        portableOverride = portable;
    }

    private static string XdgDirectory(string variable, string homeRelative)
    {
        string dir = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrEmpty(dir) && Path.IsPathRooted(dir))
        {
            return dir;
        }

        string home = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(home))
        {
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(home, homeRelative);
    }
}
