using LiveSplit.Model.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace LiveSplit.Tests.Input;

public class KeyboardHookMust
{
    private sealed class FakeBackend : IKeyboardBackend
    {
        public string Name => "fake";
        public bool IsAvailable => true;
        public string StatusMessage => null;
        public event KeyEventHandler KeyDown;
        public bool IsKeyDown(Keys key) => false;
        public void Poll() { }
        public void Dispose() { }
        public void Press(Keys key) => KeyDown?.Invoke(this, new KeyEventArgs(key));
    }

    [Fact]
    public void RaiseOnlyRegisteredKeys()
    {
        var backend = new FakeBackend();
        var hook = new KeyboardHook(backend);
        var pressed = new List<Keys>();
        hook.KeyPressed += (_, e) => pressed.Add(e.KeyData);
        hook.RegisterHotKey(Keys.NumPad1);
        hook.RegisterHotKey(Keys.A | Keys.Control);

        backend.Press(Keys.NumPad1);
        backend.Press(Keys.NumPad2);
        backend.Press(Keys.A);
        backend.Press(Keys.A | Keys.Control);
        backend.Press(Keys.A | Keys.Control | Keys.Shift);

        Assert.Equal([Keys.NumPad1, Keys.A | Keys.Control], pressed);
    }

    [Fact]
    public void StopRaisingAfterUnregisteringAllHotkeys()
    {
        var backend = new FakeBackend();
        var hook = new KeyboardHook(backend);
        int count = 0;
        hook.KeyPressed += (_, _) => count++;
        hook.RegisterHotKey(Keys.F1);

        backend.Press(Keys.F1);
        hook.UnregisterAllHotkeys();
        backend.Press(Keys.F1);

        Assert.Equal(1, count);
    }

    [Fact]
    public void StopListeningWhenDisposed()
    {
        var backend = new FakeBackend();
        var hook = new KeyboardHook(backend);
        int count = 0;
        hook.KeyPressed += (_, _) => count++;
        hook.RegisterHotKey(Keys.F1);

        hook.Dispose();
        backend.Press(Keys.F1);

        Assert.Equal(0, count);
    }

    [Fact]
    public void ReadKeyEventsFromEvdevDevices()
    {
        string directory = Path.Combine(Path.GetTempPath(), "livesplit-evdev-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string fifo = Path.Combine(directory, "event0");
        try
        {
            using (Process mkfifo = Process.Start("mkfifo", fifo))
            {
                mkfifo.WaitForExit();
                Assert.Equal(0, mkfifo.ExitCode);
            }

            using var writer = new FileStream(fifo, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 1);
            using var backend = new EvdevKeyboardBackend(() => [fifo]);
            Assert.True(backend.IsAvailable);

            var received = new List<Keys>();
            using var signal = new CountdownEvent(2);
            backend.KeyDown += (_, e) =>
            {
                lock (received)
                {
                    received.Add(e.KeyData);
                }

                signal.Signal();
            };

            WriteEvent(writer, 1, 29, 1);
            WriteEvent(writer, 0, 0, 0);
            WriteEvent(writer, 1, 30, 1);
            WriteEvent(writer, 1, 30, 0);
            writer.Flush();

            Assert.True(signal.Wait(TimeSpan.FromSeconds(5)));
            lock (received)
            {
                Assert.Equal([Keys.ControlKey, Keys.A | Keys.Control], received);
            }

            Assert.False(backend.IsKeyDown(Keys.A));
            Assert.True(backend.IsKeyDown(Keys.ControlKey));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ReportMissingPermissionsClearly()
    {
        string directory = Path.Combine(Path.GetTempPath(), "livesplit-evdev-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string device = Path.Combine(directory, "event0");
        File.WriteAllText(device, "");
        File.SetUnixFileMode(device, UnixFileMode.None);
        try
        {
            if (Environment.UserName == "root")
            {
                return;
            }

            using var backend = new EvdevKeyboardBackend(() => [device]);

            Assert.False(backend.IsAvailable);
            Assert.Contains("input", backend.StatusMessage);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static void WriteEvent(Stream stream, ushort type, ushort code, int value)
    {
        var buffer = new byte[24];
        BitConverter.TryWriteBytes(buffer.AsSpan(16), type);
        BitConverter.TryWriteBytes(buffer.AsSpan(18), code);
        BitConverter.TryWriteBytes(buffer.AsSpan(20), value);
        stream.Write(buffer);
    }
}
