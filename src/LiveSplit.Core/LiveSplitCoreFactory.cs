using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LiveSplit;

public class LiveSplitCoreFactory
{
    private const string LibraryName = "livesplit_core";
    private const string FileName = "liblivesplit_core.so";
    private static readonly object SyncRoot = new();
    private static IntPtr handle;
    private static bool resolverRegistered;

    public static void LoadLiveSplitCore()
    {
        lock (SyncRoot)
        {
            if (!resolverRegistered)
            {
                NativeLibrary.SetDllImportResolver(typeof(LiveSplitCoreFactory).Assembly, ResolveLibrary);
                resolverRegistered = true;
            }

            if (handle == IntPtr.Zero)
            {
                handle = FindAndLoad(typeof(LiveSplitCoreFactory).Assembly, null);
            }

            if (handle == IntPtr.Zero)
            {
                throw new DllNotFoundException("Unable to load the native livesplit-core library: " + FileName);
            }
        }
    }

    public static string[] GetSearchPaths()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string overridePath = Environment.GetEnvironmentVariable("LIVESPLIT_CORE_PATH");

        var paths = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(overridePath))
        {
            paths.Add(Directory.Exists(overridePath) ? Path.Combine(overridePath, FileName) : overridePath);
        }

        paths.Add(Path.Combine(baseDirectory, FileName));
        paths.Add(Path.Combine(baseDirectory, "native", FileName));
        paths.Add(Path.Combine(baseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native", FileName));
        return [.. paths];
    }

    private static IntPtr ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != LibraryName)
        {
            return IntPtr.Zero;
        }

        lock (SyncRoot)
        {
            if (handle == IntPtr.Zero)
            {
                handle = FindAndLoad(assembly, searchPath);
            }

            return handle;
        }
    }

    private static IntPtr FindAndLoad(Assembly assembly, DllImportSearchPath? searchPath)
    {
        foreach (string path in GetSearchPaths())
        {
            if (File.Exists(path) && NativeLibrary.TryLoad(path, out IntPtr loaded))
            {
                return loaded;
            }
        }

        return NativeLibrary.TryLoad(FileName, assembly, searchPath, out IntPtr fallback) ? fallback : IntPtr.Zero;
    }
}
