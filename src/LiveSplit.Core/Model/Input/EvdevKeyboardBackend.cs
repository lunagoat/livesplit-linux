using LiveSplit.Options;
using LiveSplit.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace LiveSplit.Model.Input;

public sealed unsafe class EvdevKeyboardBackend : IKeyboardBackend
{
    private const int EvKey = 1;
    private const int KeyA = 30;
    private const int KeyF1 = 59;
    private const int KeyKp1 = 79;
    private const int EventSize64 = 24;
    private const int EventSize32 = 16;
    private const int RescanIntervalMs = 2000;
    private const int PollTimeoutMs = 250;

    private readonly Func<IEnumerable<string>> deviceProvider;
    private readonly KeyEventTranslator translator = new();
    private readonly Dictionary<string, int> openDevices = [];
    private readonly object devicesLock = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly Thread thread;
    private readonly int eventSize = IntPtr.Size == 8 ? EventSize64 : EventSize32;
    private long lastScanTicks;
    private bool permissionDenied;
    private bool disposed;

    public string Name => "evdev";

    public bool IsAvailable
    {
        get
        {
            lock (devicesLock)
            {
                return openDevices.Count > 0;
            }
        }
    }

    public string StatusMessage { get; private set; }

    public int DeviceCount
    {
        get
        {
            lock (devicesLock)
            {
                return openDevices.Count;
            }
        }
    }

    public event KeyEventHandler KeyDown;

    public EvdevKeyboardBackend()
        : this(() => FindKeyboardDevices("/sys/class/input", "/dev/input"))
    {
    }

    public EvdevKeyboardBackend(Func<IEnumerable<string>> deviceProvider)
    {
        this.deviceProvider = deviceProvider;
        Rescan();

        thread = new Thread(Run) { Name = "Hotkey evdev reader", IsBackground = true };
        thread.Start();
    }

    public static IEnumerable<string> FindKeyboardDevices(string sysClassInput, string devInput)
    {
        if (!Directory.Exists(sysClassInput))
        {
            yield break;
        }

        foreach (string dir in Directory.EnumerateDirectories(sysClassInput, "event*").OrderBy(x => x, StringComparer.Ordinal))
        {
            string ev;
            string key;
            try
            {
                ev = File.ReadAllText(Path.Combine(dir, "device", "capabilities", "ev"));
                key = File.ReadAllText(Path.Combine(dir, "device", "capabilities", "key"));
            }
            catch
            {
                continue;
            }

            if (IsKeyboard(ev, key))
            {
                yield return Path.Combine(devInput, Path.GetFileName(dir));
            }
        }
    }

    public static bool IsKeyboard(string evCapabilities, string keyCapabilities)
    {
        if (!HasBit(evCapabilities, EvKey))
        {
            return false;
        }

        return HasBit(keyCapabilities, KeyA) || HasBit(keyCapabilities, KeyF1) || HasBit(keyCapabilities, KeyKp1);
    }

    public static bool HasBit(string capabilities, int bit)
    {
        string[] words = capabilities.Split([' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);
        int wordIndexFromEnd = bit / 64;
        if (wordIndexFromEnd >= words.Length)
        {
            return false;
        }

        string word = words[words.Length - 1 - wordIndexFromEnd];
        return ulong.TryParse(word, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value)
            && ((value >> (bit % 64)) & 1UL) != 0;
    }

    public bool IsKeyDown(Keys key)
    {
        return translator.IsKeyDown(key);
    }

    public void Poll()
    {
    }

    private void Rescan()
    {
        lastScanTicks = Stopwatch.GetTimestamp();

        List<string> wanted;
        try
        {
            wanted = [.. deviceProvider()];
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return;
        }

        lock (devicesLock)
        {
            foreach (string path in openDevices.Keys.Where(x => !wanted.Contains(x)).ToList())
            {
                LibC.close(openDevices[path]);
                openDevices.Remove(path);
            }

            int denied = 0;
            foreach (string path in wanted)
            {
                if (openDevices.ContainsKey(path))
                {
                    continue;
                }

                int fd = LibC.open(path, LibC.O_RDONLY | LibC.O_NONBLOCK | LibC.O_CLOEXEC);
                if (fd >= 0)
                {
                    openDevices[path] = fd;
                }
                else if (LibC.LastError is LibC.EACCES or LibC.EPERM)
                {
                    denied++;
                }
            }

            permissionDenied = denied > 0 && openDevices.Count == 0;
            StatusMessage = openDevices.Count > 0
                ? null
                : permissionDenied
                    ? "Permission denied reading /dev/input/event*. Add your user to the 'input' group (sudo usermod -aG input $USER) and log in again to enable global hotkeys."
                    : "No keyboard input devices were found under /dev/input.";
        }
    }

    private void Run()
    {
        var buffer = new byte[eventSize * 64];

        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                if (Stopwatch.GetElapsedTime(lastScanTicks).TotalMilliseconds >= RescanIntervalMs)
                {
                    Rescan();
                }

                int[] fds;
                lock (devicesLock)
                {
                    fds = [.. openDevices.Values];
                }

                if (fds.Length == 0)
                {
                    cancellation.Token.WaitHandle.WaitOne(RescanIntervalMs / 4);
                    continue;
                }

                var pollFds = new LibC.PollFd[fds.Length];
                for (int i = 0; i < fds.Length; i++)
                {
                    pollFds[i].fd = fds[i];
                    pollFds[i].events = LibC.POLLIN;
                }

                int ready;
                fixed (LibC.PollFd* p = pollFds)
                {
                    ready = LibC.poll(p, (nuint)pollFds.Length, PollTimeoutMs);
                }

                if (ready <= 0)
                {
                    continue;
                }

                for (int i = 0; i < pollFds.Length; i++)
                {
                    short revents = pollFds[i].revents;
                    if ((revents & LibC.POLLIN) != 0)
                    {
                        ReadDevice(pollFds[i].fd, buffer);
                    }
                    else if ((revents & (LibC.POLLERR | LibC.POLLHUP | LibC.POLLNVAL)) != 0)
                    {
                        CloseDevice(pollFds[i].fd);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                cancellation.Token.WaitHandle.WaitOne(500);
            }
        }
    }

    private void ReadDevice(int fd, byte[] buffer)
    {
        nint count;
        fixed (byte* p = buffer)
        {
            count = LibC.read(fd, p, (nuint)buffer.Length);
        }

        if (count < 0)
        {
            int error = LibC.LastError;
            if (error is not (LibC.EAGAIN or LibC.EINTR))
            {
                CloseDevice(fd);
            }

            return;
        }

        if (count == 0)
        {
            CloseDevice(fd);
            return;
        }

        for (int offset = 0; offset + eventSize <= count; offset += eventSize)
        {
            int headerSize = eventSize - 8;
            ushort type = BitConverter.ToUInt16(buffer, offset + headerSize);
            ushort code = BitConverter.ToUInt16(buffer, offset + headerSize + 2);
            int value = BitConverter.ToInt32(buffer, offset + headerSize + 4);

            if (type == EvKey && translator.TryProcess(code, value, out Keys key))
            {
                try
                {
                    KeyDown?.Invoke(this, new KeyEventArgs(key));
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }
        }
    }

    private void CloseDevice(int fd)
    {
        lock (devicesLock)
        {
            foreach (KeyValuePair<string, int> pair in openDevices.Where(x => x.Value == fd).ToList())
            {
                openDevices.Remove(pair.Key);
                LibC.close(fd);
            }
        }

        translator.Reset();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        cancellation.Cancel();
        if (thread.IsAlive && Thread.CurrentThread != thread)
        {
            thread.Join(1000);
        }

        lock (devicesLock)
        {
            foreach (int fd in openDevices.Values)
            {
                LibC.close(fd);
            }

            openDevices.Clear();
        }

        cancellation.Dispose();
    }
}
