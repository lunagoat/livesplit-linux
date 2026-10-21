using System;
using System.IO;

namespace LiveSplit.Tests;

internal static class RepositoryFiles
{
    public static string Find(params string[] relativePath)
    {
        string directory = AppContext.BaseDirectory;
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory, "LiveSplit.slnx")))
            {
                return Path.Combine([directory, .. relativePath]);
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new FileNotFoundException("The repository root could not be located.");
    }
}
