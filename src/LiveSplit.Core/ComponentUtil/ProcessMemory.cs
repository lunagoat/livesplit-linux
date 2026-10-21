using LiveSplit.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LiveSplit.ComponentUtil;

public sealed class MemoryMapEntry
{
    public ulong Start { get; init; }
    public ulong End { get; init; }
    public string Permissions { get; init; }
    public ulong Offset { get; init; }
    public string Path { get; init; }

    public ulong Size => End - Start;
    public bool Readable => Permissions.Length > 0 && Permissions[0] == 'r';
    public bool Writable => Permissions.Length > 1 && Permissions[1] == 'w';
    public bool Executable => Permissions.Length > 2 && Permissions[2] == 'x';
    public bool Private => Permissions.Length > 3 && Permissions[3] == 'p';
    public bool IsFileBacked => !string.IsNullOrEmpty(Path) && Path[0] == '/';
}

public static unsafe class ProcessMemory
{
    private static readonly object CacheLock = new();
    private static readonly Dictionary<int, (string Key, ProcessModuleWow64Safe[] Modules)> ModuleCache = [];
    private static readonly Dictionary<int, (string Key, bool Is64Bit)> BitnessCache = [];

    public static string ProcRoot { get; set; } = "/proc";

    public static IReadOnlyList<MemoryMapEntry> ReadMaps(int pid)
    {
        string path = System.IO.Path.Combine(ProcRoot, pid.ToString(CultureInfo.InvariantCulture), "maps");
        var entries = new List<MemoryMapEntry>();
        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return entries;
        }

        foreach (string line in lines)
        {
            MemoryMapEntry entry = ParseMapsLine(line);
            if (entry != null)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    public static MemoryMapEntry ParseMapsLine(string line)
    {
        int dash = line.IndexOf('-');
        int space1 = line.IndexOf(' ');
        if (dash <= 0 || space1 <= dash)
        {
            return null;
        }

        string[] parts = line.Split(' ', 6, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
        {
            return null;
        }

        if (!ulong.TryParse(line.AsSpan(0, dash), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong start)
            || !ulong.TryParse(line.AsSpan(dash + 1, space1 - dash - 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong end)
            || !ulong.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong offset))
        {
            return null;
        }

        string pathText = parts.Length >= 6 ? parts[5].Trim() : "";
        if (pathText.EndsWith(" (deleted)", StringComparison.Ordinal))
        {
            pathText = pathText[..^" (deleted)".Length];
        }

        return new MemoryMapEntry
        {
            Start = start,
            End = end,
            Permissions = parts[1],
            Offset = offset,
            Path = pathText,
        };
    }

    public static bool TryRead(int pid, ulong address, byte[] buffer, int count, out int bytesRead)
    {
        bytesRead = 0;
        if (count == 0)
        {
            return true;
        }

        fixed (byte* p = buffer)
        {
            var local = new LibC.IoVec { iov_base = p, iov_len = (nuint)count };
            var remote = new LibC.IoVec { iov_base = (void*)address, iov_len = (nuint)count };
            nint result = LibC.process_vm_readv(pid, &local, 1, &remote, 1, 0);
            if (result > 0)
            {
                bytesRead = (int)result;
                return true;
            }
        }

        return TryReadProcMem(pid, address, buffer, count, out bytesRead);
    }

    public static bool TryWrite(int pid, ulong address, byte[] buffer, int count, out int bytesWritten)
    {
        bytesWritten = 0;
        if (count == 0)
        {
            return true;
        }

        if (TryWriteProcMem(pid, address, buffer, count, out bytesWritten))
        {
            return true;
        }

        fixed (byte* p = buffer)
        {
            var local = new LibC.IoVec { iov_base = p, iov_len = (nuint)count };
            var remote = new LibC.IoVec { iov_base = (void*)address, iov_len = (nuint)count };
            nint result = LibC.process_vm_writev(pid, &local, 1, &remote, 1, 0);
            if (result > 0)
            {
                bytesWritten = (int)result;
                return true;
            }
        }

        return false;
    }

    private static bool TryReadProcMem(int pid, ulong address, byte[] buffer, int count, out int bytesRead)
    {
        bytesRead = 0;
        try
        {
            using var stream = new FileStream(
                System.IO.Path.Combine(ProcRoot, pid.ToString(CultureInfo.InvariantCulture), "mem"),
                FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1);
            stream.Seek((long)address, SeekOrigin.Begin);
            int total = 0;
            while (total < count)
            {
                int read = stream.Read(buffer, total, count - total);
                if (read <= 0)
                {
                    break;
                }

                total += read;
            }

            bytesRead = total;
            return total > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryWriteProcMem(int pid, ulong address, byte[] buffer, int count, out int bytesWritten)
    {
        bytesWritten = 0;
        try
        {
            using var stream = new FileStream(
                System.IO.Path.Combine(ProcRoot, pid.ToString(CultureInfo.InvariantCulture), "mem"),
                FileMode.Open, FileAccess.Write, FileShare.ReadWrite, 1);
            stream.Seek((long)address, SeekOrigin.Begin);
            stream.Write(buffer, 0, count);
            stream.Flush();
            bytesWritten = count;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    public static ProcessModuleWow64Safe[] GetModules(int pid)
    {
        IReadOnlyList<MemoryMapEntry> maps = ReadMaps(pid);
        string key = string.Join('|', maps.Where(x => x.IsFileBacked).Select(x => x.Start.ToString("x", CultureInfo.InvariantCulture) + x.Path));

        lock (CacheLock)
        {
            if (ModuleCache.TryGetValue(pid, out (string Key, ProcessModuleWow64Safe[] Modules) cached) && cached.Key == key)
            {
                return cached.Modules;
            }
        }

        ProcessModuleWow64Safe[] modules = BuildModules(pid, maps);

        lock (CacheLock)
        {
            if (ModuleCache.Count > 100)
            {
                ModuleCache.Clear();
            }

            ModuleCache[pid] = (key, modules);
        }

        return modules;
    }

    public static ProcessModuleWow64Safe[] BuildModules(int pid, IReadOnlyList<MemoryMapEntry> maps)
    {
        var modules = new List<ProcessModuleWow64Safe>();
        var byPath = new Dictionary<string, ProcessModuleWow64Safe>();
        string exePath = TryReadExePath(pid);

        foreach (MemoryMapEntry entry in maps)
        {
            if (!entry.IsFileBacked || entry.Path.StartsWith("/memfd:", StringComparison.Ordinal)
                || (!LooksLikeModule(entry.Path) && entry.Path != exePath))
            {
                continue;
            }

            if (byPath.TryGetValue(entry.Path, out ProcessModuleWow64Safe existing))
            {
                long end = (long)entry.End;
                int size = (int)(end - (long)existing.BaseAddress);
                if (size > existing.ModuleMemorySize)
                {
                    existing.ModuleMemorySize = size;
                }

                continue;
            }

            if (entry.Offset != 0)
            {
                continue;
            }

            var module = new ProcessModuleWow64Safe
            {
                BaseAddress = (IntPtr)(long)entry.Start,
                EntryPointAddress = IntPtr.Zero,
                FileName = entry.Path,
                ModuleMemorySize = (int)Math.Min(entry.Size, int.MaxValue),
                ModuleName = System.IO.Path.GetFileName(entry.Path),
            };

            byPath[entry.Path] = module;
            modules.Add(module);
        }

        foreach (ProcessModuleWow64Safe module in modules)
        {
            if (TryReadPeInfo(pid, (ulong)(long)module.BaseAddress, out int sizeOfImage, out int entryPointRva, out _))
            {
                module.ModuleMemorySize = sizeOfImage;
                module.EntryPointAddress = module.BaseAddress + entryPointRva;
            }
        }

        ProcessModuleWow64Safe main = null;
        if (exePath != null && !IsWinePreloader(exePath))
        {
            main = modules.FirstOrDefault(x => x.FileName == exePath);
        }

        main ??= FindWineMainModule(pid, modules);

        if (main != null && modules.IndexOf(main) > 0)
        {
            modules.Remove(main);
            modules.Insert(0, main);
        }

        return [.. modules];
    }

    public static bool Is64Bit(int pid)
    {
        string exe = TryReadExePath(pid);
        IReadOnlyList<MemoryMapEntry> maps = ReadMaps(pid);
        string key = exe + "|" + maps.Count;

        lock (CacheLock)
        {
            if (BitnessCache.TryGetValue(pid, out (string Key, bool Is64Bit) cached) && cached.Key == key)
            {
                return cached.Is64Bit;
            }
        }

        bool is64 = DetectIs64Bit(pid, exe);

        lock (CacheLock)
        {
            BitnessCache[pid] = (key, is64);
        }

        return is64;
    }

    private static bool DetectIs64Bit(int pid, string exePath)
    {
        if (exePath != null && IsWinePreloader(exePath))
        {
            ProcessModuleWow64Safe main = FindWineMainModule(pid, BuildModulesWithoutReorder(pid));
            if (main != null
                && TryReadPeInfo(pid, (ulong)(long)main.BaseAddress, out _, out _, out bool isPe32Plus))
            {
                return isPe32Plus;
            }
        }

        if (exePath != null)
        {
            try
            {
                using FileStream stream = File.OpenRead(exePath);
                var header = new byte[5];
                if (stream.Read(header, 0, 5) == 5 && header[0] == 0x7F && header[1] == (byte)'E' && header[2] == (byte)'L' && header[3] == (byte)'F')
                {
                    return header[4] == 2;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return Environment.Is64BitOperatingSystem;
    }

    private static List<ProcessModuleWow64Safe> BuildModulesWithoutReorder(int pid)
    {
        return [.. GetModules(pid)];
    }

    private static ProcessModuleWow64Safe FindWineMainModule(int pid, IEnumerable<ProcessModuleWow64Safe> modules)
    {
        List<ProcessModuleWow64Safe> exes = [.. modules.Where(x => x.ModuleName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))];
        if (exes.Count == 0)
        {
            return null;
        }

        string comm = TryReadComm(pid);
        if (comm != null)
        {
            ProcessModuleWow64Safe byComm = exes.FirstOrDefault(x => string.Equals(x.ModuleName, comm, StringComparison.OrdinalIgnoreCase)
                || string.Equals(System.IO.Path.GetFileNameWithoutExtension(x.ModuleName), System.IO.Path.GetFileNameWithoutExtension(comm), StringComparison.OrdinalIgnoreCase));
            if (byComm != null)
            {
                return byComm;
            }
        }

        foreach (string arg in ReadCommandLine(pid))
        {
            string name = ProcessUtil.GetBaseName(arg);
            ProcessModuleWow64Safe byArg = exes.FirstOrDefault(x => string.Equals(x.ModuleName, name, StringComparison.OrdinalIgnoreCase));
            if (byArg != null)
            {
                return byArg;
            }
        }

        return exes[0];
    }

    private static bool LooksLikeModule(string path)
    {
        string name = System.IO.Path.GetFileName(path);
        return name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".so", StringComparison.Ordinal)
            || name.Contains(".so.", StringComparison.Ordinal)
            || name.EndsWith(".drv", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".sys", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".ocx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWinePreloader(string exePath)
    {
        string name = System.IO.Path.GetFileName(exePath);
        return name.StartsWith("wine", StringComparison.OrdinalIgnoreCase) && name.Contains("preloader", StringComparison.OrdinalIgnoreCase)
            || name is "wine" or "wine64" or "wineserver";
    }

    public static bool TryReadPeInfo(int pid, ulong baseAddress, out int sizeOfImage, out int entryPointRva, out bool isPe32Plus)
    {
        sizeOfImage = 0;
        entryPointRva = 0;
        isPe32Plus = false;

        var dos = new byte[0x40];
        if (!TryRead(pid, baseAddress, dos, dos.Length, out int read) || read != dos.Length || dos[0] != 'M' || dos[1] != 'Z')
        {
            return false;
        }

        int peOffset = BitConverter.ToInt32(dos, 0x3C);
        if (peOffset <= 0 || peOffset > 0x1000)
        {
            return false;
        }

        var pe = new byte[0x60];
        if (!TryRead(pid, baseAddress + (ulong)peOffset, pe, pe.Length, out read) || read != pe.Length
            || pe[0] != 'P' || pe[1] != 'E' || pe[2] != 0 || pe[3] != 0)
        {
            return false;
        }

        ushort magic = BitConverter.ToUInt16(pe, 24);
        if (magic is not (0x10B or 0x20B))
        {
            return false;
        }

        isPe32Plus = magic == 0x20B;
        entryPointRva = BitConverter.ToInt32(pe, 24 + 16);
        sizeOfImage = BitConverter.ToInt32(pe, 24 + 56);
        return sizeOfImage > 0;
    }

    public static string TryReadExePath(int pid)
    {
        try
        {
            FileSystemInfo link = new FileInfo(System.IO.Path.Combine(ProcRoot, pid.ToString(CultureInfo.InvariantCulture), "exe")).ResolveLinkTarget(false);
            string target = link?.FullName;
            if (target != null && target.EndsWith(" (deleted)", StringComparison.Ordinal))
            {
                target = target[..^" (deleted)".Length];
            }

            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static string TryReadComm(int pid)
    {
        try
        {
            return File.ReadAllText(System.IO.Path.Combine(ProcRoot, pid.ToString(CultureInfo.InvariantCulture), "comm")).TrimEnd('\n');
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static IReadOnlyList<string> ReadCommandLine(int pid)
    {
        try
        {
            byte[] data = File.ReadAllBytes(System.IO.Path.Combine(ProcRoot, pid.ToString(CultureInfo.InvariantCulture), "cmdline"));
            return System.Text.Encoding.UTF8.GetString(data).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static MemoryBasicInformation ToBasicInformation(MemoryMapEntry entry)
    {
        MemPageProtect protect = entry.Executable
            ? entry.Writable ? MemPageProtect.PAGE_EXECUTE_READWRITE : entry.Readable ? MemPageProtect.PAGE_EXECUTE_READ : MemPageProtect.PAGE_EXECUTE
            : entry.Writable ? MemPageProtect.PAGE_READWRITE : entry.Readable ? MemPageProtect.PAGE_READONLY : MemPageProtect.PAGE_NOACCESS;

        MemPageType type = !entry.IsFileBacked
            ? MemPageType.MEM_PRIVATE
            : LooksLikeModule(entry.Path) ? MemPageType.MEM_IMAGE : MemPageType.MEM_MAPPED;

        return new MemoryBasicInformation
        {
            BaseAddress = (IntPtr)(long)entry.Start,
            AllocationBase = (IntPtr)(long)entry.Start,
            AllocationProtect = protect,
            RegionSize = (UIntPtr)entry.Size,
            State = MemPageState.MEM_COMMIT,
            Protect = entry.Readable || entry.Writable || entry.Executable ? protect : MemPageProtect.PAGE_NOACCESS | MemPageProtect.PAGE_GUARD,
            Type = type,
        };
    }

    public static void Signal(int pid, int signal)
    {
        LibC.kill(pid, signal);
    }
}

public static class ProcessUtil
{
    public static string GetBaseName(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        int index = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        return index >= 0 ? path[(index + 1)..] : path;
    }

    public static bool MatchesName(int pid, string name)
    {
        string wanted = StripExe(name);
        string comm = ProcessMemory.TryReadComm(pid);
        if (comm != null && string.Equals(StripExe(comm), wanted, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        IReadOnlyList<string> args = ProcessMemory.ReadCommandLine(pid);
        return args.Count > 0
            && string.Equals(StripExe(GetBaseName(args[0])), wanted, StringComparison.OrdinalIgnoreCase);
    }

    public static Process[] GetProcessesByName(string name)
    {
        var result = new List<Process>();
        foreach (string dir in Directory.EnumerateDirectories(ProcessMemory.ProcRoot))
        {
            if (!int.TryParse(System.IO.Path.GetFileName(dir), NumberStyles.None, CultureInfo.InvariantCulture, out int pid))
            {
                continue;
            }

            if (!MatchesName(pid, name))
            {
                continue;
            }

            try
            {
                result.Add(Process.GetProcessById(pid));
            }
            catch (ArgumentException)
            {
            }
        }

        return [.. result];
    }

    private static string StripExe(string name)
    {
        return name != null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }
}
