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
    /// blobs (icons, bitmaps) that .NET can no longer deserialize; decode the images ourselves and
    /// return null for anything else that cannot be read, instead of failing the whole component.
    /// </summary>
    public static object GetResourceObject(ResourceManager manager, string name)
    {
        return GetResourceObject(manager, name, null);
    }

    public static object GetResourceObject(ResourceManager manager, string name, CultureInfo culture)
    {
        try
        {
            return culture == null ? manager.GetObject(name) : manager.GetObject(name, culture);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or IOException)
        {
            return ReadSerializedImage(manager, name);
        }
    }

    private static object ReadSerializedImage(ResourceManager manager, string name)
    {
        try
        {
            Assembly assembly = null;
            for (Type t = manager.GetType(); t != null && assembly == null; t = t.BaseType)
            {
                assembly = t.GetField("MainAssembly", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(manager) as Assembly;
            }

            using Stream stream = assembly?.GetManifestResourceStream(manager.BaseName + ".resources");
            return stream == null ? null : ReadSerializedImage(stream, name);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Reads a BinaryFormatter-serialized System.Drawing image out of a .resources stream.</summary>
    public static object ReadSerializedImage(Stream resources, string name)
    {
        try
        {
            using var reader = new ResourceReader(resources);
            reader.GetResourceData(name, out string type, out byte[] data);
            if (type == null || data == null || !(type.StartsWith("System.Drawing.Bitmap", StringComparison.Ordinal) || type.StartsWith("System.Drawing.Image", StringComparison.Ordinal)))
            {
                return null;
            }

            if (System.Drawing.LegacySerialization.TryDeserializeImage(data, out System.Drawing.Image image))
            {
                return image;
            }

            // The payload may carry a 7-bit encoded length prefix.
            int offset = 0;
            while (offset < data.Length && (data[offset] & 0x80) != 0)
            {
                offset++;
            }

            offset++;
            if (offset < data.Length && System.Drawing.LegacySerialization.TryDeserializeImage(data[offset..], out image))
            {
                return image;
            }

            return null;
        }
        catch (Exception)
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

    /// <summary>
    /// Replaces <c>isinst WindowsFormsSynchronizationContext</c>: legacy components insist that the
    /// UI thread has a WinForms synchronization context. Any context on the UI thread will do here.
    /// </summary>
    public static object AsUiSynchronizationContext(object value)
    {
        return value as System.Threading.SynchronizationContext;
    }

    /// <summary>Thread.Abort is not supported on .NET Core; the component's own cancellation flag has to do.</summary>
    public static void AbortThread(System.Threading.Thread thread)
    {
    }
}
