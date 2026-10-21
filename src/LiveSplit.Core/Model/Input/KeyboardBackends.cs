using LiveSplit.Options;
using System;

namespace LiveSplit.Model.Input;

public static class KeyboardBackends
{
    private static readonly object SyncRoot = new();
    private static IKeyboardBackend shared;

    public static IKeyboardBackend Shared
    {
        get
        {
            lock (SyncRoot)
            {
                return shared ??= Create();
            }
        }
        set
        {
            lock (SyncRoot)
            {
                shared = value;
            }
        }
    }

    public static IKeyboardBackend Create()
    {
        string preferred = Environment.GetEnvironmentVariable("LIVESPLIT_HOTKEY_BACKEND")?.Trim().ToLowerInvariant();

        if (preferred == "none")
        {
            return new NullKeyboardBackend("Global hotkeys are disabled by LIVESPLIT_HOTKEY_BACKEND=none.");
        }

        var failures = new System.Collections.Generic.List<string>();

        if (preferred is null or "" or "evdev")
        {
            try
            {
                var evdev = new EvdevKeyboardBackend();
                if (evdev.IsAvailable)
                {
                    return evdev;
                }

                failures.Add(evdev.StatusMessage);
                evdev.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                failures.Add("evdev: " + ex.Message);
            }
        }

        if (preferred is null or "" or "x11")
        {
            try
            {
                if (string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "x11", StringComparison.OrdinalIgnoreCase)
                    || preferred == "x11")
                {
                    var x11 = new X11KeyboardBackend();
                    if (x11.IsAvailable)
                    {
                        return x11;
                    }

                    failures.Add(x11.StatusMessage);
                    x11.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                failures.Add("x11: " + ex.Message);
            }
        }

        string message = string.Join(" ", failures);
        if (message.Length > 0)
        {
            Log.Error(message);
        }

        return new NullKeyboardBackend(message.Length > 0 ? message : "No global keyboard backend is available.");
    }
}
