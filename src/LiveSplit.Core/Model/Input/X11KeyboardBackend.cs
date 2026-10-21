using LiveSplit.Options;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace LiveSplit.Model.Input;

public sealed unsafe class X11KeyboardBackend : IKeyboardBackend
{
    private const int KeymapBytes = 32;
    private const int X11KeycodeOffset = 8;
    private const int PollIntervalMs = 8;

    private readonly KeyEventTranslator translator = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly Thread thread;
    private IntPtr display;
    private bool disposed;

    public string Name => "x11";

    public bool IsAvailable => display != IntPtr.Zero;

    public string StatusMessage { get; private set; }

    public event KeyEventHandler KeyDown;

    public X11KeyboardBackend()
    {
        try
        {
            display = XOpenDisplay(null);
            if (display == IntPtr.Zero)
            {
                StatusMessage = "Unable to open the X11 display.";
                return;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            StatusMessage = "libX11 is not available: " + ex.Message;
            return;
        }

        thread = new Thread(Run) { Name = "Hotkey X11 reader", IsBackground = true };
        thread.Start();
    }

    public bool IsKeyDown(Keys key)
    {
        return translator.IsKeyDown(key);
    }

    public void Poll()
    {
    }

    private void Run()
    {
        byte* current = stackalloc byte[KeymapBytes];
        var previous = new byte[KeymapBytes];

        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                XQueryKeymap(display, current);

                for (int i = 0; i < KeymapBytes; i++)
                {
                    byte changed = (byte)(current[i] ^ previous[i]);
                    if (changed == 0)
                    {
                        continue;
                    }

                    for (int bit = 0; bit < 8; bit++)
                    {
                        if ((changed & (1 << bit)) == 0)
                        {
                            continue;
                        }

                        int keycode = (i * 8) + bit - X11KeycodeOffset;
                        bool down = (current[i] & (1 << bit)) != 0;
                        if (keycode >= 0 && translator.TryProcess(keycode, down ? 1 : 0, out Keys key))
                        {
                            KeyDown?.Invoke(this, new KeyEventArgs(key));
                        }
                    }

                    previous[i] = current[i];
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            cancellation.Token.WaitHandle.WaitOne(PollIntervalMs);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        cancellation.Cancel();
        if (thread != null && thread.IsAlive && Thread.CurrentThread != thread)
        {
            thread.Join(1000);
        }

        if (display != IntPtr.Zero)
        {
            XCloseDisplay(display);
            display = IntPtr.Zero;
        }

        cancellation.Dispose();
    }

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(string name);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XQueryKeymap(IntPtr display, byte* keys);
}
