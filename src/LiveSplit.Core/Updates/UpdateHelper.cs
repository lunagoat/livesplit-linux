using LiveSplit.Options;
using System;
using System.Collections.Generic;

namespace LiveSplit.Updates;

public static class UpdateHelper
{
    public static readonly Version Version = GetVersionFromGit();
    public static string UserAgent => GetUserAgent();
    public static readonly List<Type> AlreadyChecked = [];

    private static Version GetVersionFromGit()
    {
        try
        {
            return Version.TryParse($"{Git.LastTag}.{Git.CommitsSinceLastTag}", out Version version)
                ? version
                : new Version(1, 0, 0, 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return new Version(1, 0, 0, 0);
        }
    }

    private static string GetUserAgent()
    {
        string versionString = (Version != null) ? Version.ToString() : "Unknown";
        return $"LiveSplit/{versionString}";
    }
}
