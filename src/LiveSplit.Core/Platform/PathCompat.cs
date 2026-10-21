using System;
using System.IO;

namespace LiveSplit.Hosting;

public static class PathCompat
{
    public static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        if (Path.DirectorySeparatorChar == '\\')
        {
            return path;
        }

        bool windowsStyle = path.Contains('\\') || (path.Length >= 2 && path[1] == ':' && char.IsLetter(path[0]));
        if (!windowsStyle)
        {
            return path;
        }

        string converted = path.Replace('\\', '/');

        if (converted.Length >= 2 && converted[1] == ':' && char.IsLetter(converted[0]))
        {
            string rest = converted[2..];
            if (!rest.StartsWith('/'))
            {
                rest = "/" + rest;
            }

            string wineMapped = MapWineDrive(char.ToLowerInvariant(converted[0]), rest);
            return wineMapped ?? rest;
        }

        return converted;
    }

    public static string Resolve(string path, string baseDirectory = null)
    {
        string normalized = Normalize(path);
        if (string.IsNullOrEmpty(normalized))
        {
            return normalized;
        }

        string candidate = !Path.IsPathRooted(normalized) && baseDirectory != null
            ? Path.Combine(baseDirectory, normalized)
            : normalized;

        return ResolveCaseInsensitive(candidate) ?? candidate;
    }

    public static string ResolveCaseInsensitive(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (File.Exists(path) || Directory.Exists(path))
        {
            return path;
        }

        string full = Path.GetFullPath(path);
        string root = Path.GetPathRoot(full);
        string current = root;
        string[] parts = full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        foreach (string part in parts)
        {
            string next = Path.Combine(current, part);
            if (File.Exists(next) || Directory.Exists(next))
            {
                current = next;
                continue;
            }

            string match = null;
            try
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(current))
                {
                    if (string.Equals(Path.GetFileName(entry), part, StringComparison.OrdinalIgnoreCase))
                    {
                        match = entry;
                        break;
                    }
                }
            }
            catch
            {
            }

            if (match == null)
            {
                return null;
            }

            current = match;
        }

        return current;
    }

    private static string MapWineDrive(char drive, string rest)
    {
        if (drive == 'z')
        {
            return rest;
        }

        string prefix = Environment.GetEnvironmentVariable("WINEPREFIX");
        if (string.IsNullOrEmpty(prefix))
        {
            string home = Environment.GetEnvironmentVariable("HOME");
            if (string.IsNullOrEmpty(home))
            {
                return null;
            }

            prefix = Path.Combine(home, ".wine");
        }

        string link = Path.Combine(prefix, "dosdevices", drive + ":");
        try
        {
            if (Directory.Exists(link))
            {
                var info = new DirectoryInfo(link);
                string target = info.ResolveLinkTarget(true)?.FullName ?? link;
                return Path.Combine(target, rest.TrimStart('/'));
            }
        }
        catch
        {
        }

        return null;
    }
}
