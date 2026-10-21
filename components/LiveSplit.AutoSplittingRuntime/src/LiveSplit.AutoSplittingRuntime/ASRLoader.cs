using System;
using System.IO;
using System.Linq;
using LiveSplit.ComponentUtil;
using System.Runtime.InteropServices;
namespace LiveSplit.AutoSplittingRuntime;

public static class ASRLoader
{
    private static readonly object Sync = new();
    private static IntPtr handle;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint ProcessResolver(IntPtr name, UIntPtr length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void InstallProcessResolver(ProcessResolver resolver);
    private static readonly ProcessResolver ResolveProcess = (name, length) =>
    {
        try
        {
            string text = Marshal.PtrToStringUTF8(name, checked((int)length.ToUInt64()));
            var processes = ProcessUtil.GetProcessesByName(text);
            try { return (uint)(processes.OrderByDescending(p => p.StartTime).FirstOrDefault(p => !p.HasExited)?.Id ?? 0); }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        catch { return 0; } // Exceptions must not cross the native callback boundary.
    };
    public static bool HasCoreProcessResolver { get; private set; }

    static ASRLoader() => NativeLibrary.SetDllImportResolver(typeof(ASRLoader).Assembly,
        (name, assembly, searchPath) => name == "asr_capi" ? Load() : IntPtr.Zero);
    public static void LoadASR() => Load();
    private static IntPtr Load()
    {
        lock (Sync)
        {
            if (handle != IntPtr.Zero) return handle;
            string name = OperatingSystem.IsWindows() ? "asr_capi.dll" : OperatingSystem.IsMacOS() ? "libasr_capi.dylib" : "libasr_capi.so";
            string configured = Environment.GetEnvironmentVariable("LIVESPLIT_ASR_PATH");
            string[] paths = [configured == null ? "" : Directory.Exists(configured) ? Path.Combine(configured, name) : configured,
                Path.Combine(AppContext.BaseDirectory, name),
                Path.Combine(AppContext.BaseDirectory, "native", name),
                Path.Combine(AppContext.BaseDirectory, "../../../../linux/native", name),
                "/home/luna/Projects/livesplit-linux/src/linux/native/" + name];
            foreach (string path in paths)
                if (!string.IsNullOrEmpty(path) && NativeLibrary.TryLoad(path, out handle))
                {
                    if (NativeLibrary.TryGetExport(handle, "set_process_resolver", out var install))
                    {
                        Marshal.GetDelegateForFunctionPointer<InstallProcessResolver>(install)(ResolveProcess);
                        HasCoreProcessResolver = true;
                    }
                    return handle;
                }
            throw new DllNotFoundException("Unable to load ASR. Put " + name + " beside LiveSplit or in native/, or set LIVESPLIT_ASR_PATH. Tried: " + string.Join(", ", paths));
        }
    }
}
