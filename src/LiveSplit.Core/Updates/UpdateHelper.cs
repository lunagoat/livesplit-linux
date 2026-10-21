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
            return Version.Parse($"{Git.LastTag}.{Git.CommitsSinceLastTag}");
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return null;
        }
    }

    private static string GetUserAgent()
    {
        string versionString = (Version != null) ? Version.ToString() : "Unknown";
        return $"LiveSplit/{versionString}";
    }
}
