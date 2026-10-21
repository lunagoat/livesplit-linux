using System;
using System.Reflection;
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
}
