using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace LiveSplit.Model.Input;

public class KeyboardHook : IDisposable
{
    private readonly object registeredKeysLock = new();
    private bool disposed;

    protected HashSet<Keys> RegisteredKeys { get; set; }
    protected IKeyboardBackend Backend { get; }
    public event KeyEventHandler KeyPressed;

    public KeyboardHook()
        : this(KeyboardBackends.Shared)
    {
    }

    public KeyboardHook(IKeyboardBackend backend)
    {
        RegisteredKeys = [];
        Backend = backend;
        Backend.KeyDown += Backend_KeyDown;
    }

    public bool IsAvailable => Backend.IsAvailable;

    public string StatusMessage => Backend.StatusMessage;

    public void RegisterHotKey(Keys key)
    {
        lock (registeredKeysLock)
        {
            RegisteredKeys.Add(key);
        }
    }

    public void UnregisterAllHotkeys()
    {
        lock (registeredKeysLock)
        {
            RegisteredKeys.Clear();
        }
    }

    public void Poll()
    {
        Backend.Poll();
    }

    protected bool IsKeyDown(Keys key)
    {
        return Backend.IsKeyDown(key);
    }

    private void Backend_KeyDown(object sender, KeyEventArgs e)
    {
        bool registered;
        lock (registeredKeysLock)
        {
            registered = RegisteredKeys.Contains(e.KeyData);
        }

        if (registered)
        {
            KeyPressed?.Invoke(this, e);
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            Backend.KeyDown -= Backend_KeyDown;
        }

        GC.SuppressFinalize(this);
    }
}
