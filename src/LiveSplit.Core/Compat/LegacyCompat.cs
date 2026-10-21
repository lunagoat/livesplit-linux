using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Resources;
using System.Security.Principal;
using System.Runtime.CompilerServices;
using LiveSplit.Model;

namespace LiveSplit.Compat;

/// <summary>Runtime helpers that rewritten legacy components are redirected to.</summary>
public static class LegacyCompat
{
    private static readonly ConditionalWeakTable<LiveSplitState, object> Forms = new();

    /// <summary>
    /// Stands in for <c>LiveSplitState.Form</c>: returns a (headless) WinForms Form from the stub
    /// assembly, or null when the stub is not installed.
    /// </summary>
    public static object GetForm(LiveSplitState state)
    {
        if (state == null)
        {
            return null;
        }

        if (Forms.TryGetValue(state, out object form))
        {
            return form;
        }

        try
        {
            Assembly stub = Assembly.Load(LegacyComponentLoader.WinFormsStubAssembly);
            Type type = stub.GetType("System.Windows.Forms.Form");
            form = type == null ? null : Activator.CreateInstance(type);
        }
        catch
        {
            form = null;
        }

        if (form != null)
        {
            Forms.Add(state, form);
        }

        return form;
    }

    /// <summary>
    /// ResourceManager.GetObject for legacy assemblies. Their .resources hold BinaryFormatter
    /// blobs (icons, bitmaps) that .NET can no longer read; return null for those instead of
    /// failing the whole component.
    /// </summary>
    public static object GetResourceObject(ResourceManager manager, string name)
    {
        try
        {
            return manager.GetObject(name);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    public static object GetResourceObject(ResourceManager manager, string name, CultureInfo culture)
    {
        try
        {
            return manager.GetObject(name, culture);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// The NamedPipeClientStream overload taking PipeAccessRights is Windows-only; map the
    /// rights to a PipeDirection and use the portable constructor.
    /// </summary>
    public static NamedPipeClientStream CreatePipeClient(string serverName, string pipeName, PipeAccessRights rights, PipeOptions options, TokenImpersonationLevel impersonation, HandleInheritability inheritability)
    {
        bool read = (rights & PipeAccessRights.ReadData) != 0;
        bool write = (rights & PipeAccessRights.WriteData) != 0;
        PipeDirection direction = read && !write ? PipeDirection.In : write && !read ? PipeDirection.Out : PipeDirection.InOut;
        return new NamedPipeClientStream(serverName, pipeName, direction, options, impersonation, inheritability);
    }

    /// <summary>
    /// Process.Handle is a pidfd-style descriptor on Linux; legacy code passes it straight to
    /// ReadProcessMemory/VirtualQueryEx, which libwin32shim expects to be the pid.
    /// </summary>
    public static IntPtr GetProcessHandle(System.Diagnostics.Process process)
    {
        return process == null ? IntPtr.Zero : (IntPtr)process.Id;
    }
}
