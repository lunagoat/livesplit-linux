using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace LiveSplit.Model.Input;

[Flags]
public enum ModifierKeys
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}

public sealed class HotkeyHook : IDisposable
{
    public static HotkeyHook Instance { get; set; }

    static HotkeyHook()
    {
        Instance = new HotkeyHook();
    }

    private HotkeyHook()
    {
    }

    public class KeyPressedEventArgs : EventArgs
    {
        public ModifierKeys Modifier { get; private set; }
        public Keys Key { get; private set; }

        internal KeyPressedEventArgs(ModifierKeys modifier, Keys key)
        {
            Modifier = modifier;
            Key = key;
        }
    }

    private readonly List<(ModifierKeys Modifier, Keys Key)> registered = [];
    private readonly object syncRoot = new();
    private IKeyboardBackend backend;

    public event EventHandler<KeyPressedEventArgs> KeyPressed;

    public IKeyboardBackend Backend
    {
        get => backend ??= KeyboardBackends.Shared;
        set
        {
            if (backend != null)
            {
                backend.KeyDown -= Backend_KeyDown;
            }

            backend = value;
            if (backend != null && registered.Count > 0)
            {
                backend.KeyDown += Backend_KeyDown;
            }
        }
    }

    public void RegisterHotKey(ModifierKeys modifier, Keys key)
    {
        lock (syncRoot)
        {
            if (registered.Count == 0)
            {
                Backend.KeyDown += Backend_KeyDown;
            }

            registered.Add((modifier, key));
        }
    }

    public void UnregisterAllHotkeys()
    {
        lock (syncRoot)
        {
            if (registered.Count > 0 && backend != null)
            {
                backend.KeyDown -= Backend_KeyDown;
            }

            registered.Clear();
        }
    }

    private void Backend_KeyDown(object sender, KeyEventArgs e)
    {
        ModifierKeys modifiers = ModifierKeys.None;
        if (e.Alt)
        {
            modifiers |= ModifierKeys.Alt;
        }

        if (e.Control)
        {
            modifiers |= ModifierKeys.Control;
        }

        if (e.Shift)
        {
            modifiers |= ModifierKeys.Shift;
        }

        bool match;
        lock (syncRoot)
        {
            match = registered.Contains((modifiers, e.KeyCode));
        }

        if (match)
        {
            KeyPressed?.Invoke(this, new KeyPressedEventArgs(modifiers, e.KeyCode));
        }
    }

    public void Dispose()
    {
        UnregisterAllHotkeys();
    }
}
