using System;
using System.Windows.Forms;

namespace LiveSplit.Model.Input;

public interface IKeyboardBackend : IDisposable
{
    string Name { get; }

    bool IsAvailable { get; }

    string StatusMessage { get; }

    event KeyEventHandler KeyDown;

    bool IsKeyDown(Keys key);

    void Poll();
}

public sealed class NullKeyboardBackend : IKeyboardBackend
{
    public NullKeyboardBackend(string statusMessage = "No global keyboard backend is available.")
    {
        StatusMessage = statusMessage;
    }

    public string Name => "none";

    public bool IsAvailable => false;

    public string StatusMessage { get; }

    public event KeyEventHandler KeyDown
    {
        add { }
        remove { }
    }

    public bool IsKeyDown(Keys key)
    {
        return false;
    }

    public void Poll()
    {
    }

    public void Dispose()
    {
    }
}
