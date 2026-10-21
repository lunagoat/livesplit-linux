using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace LiveSplit.UI.Components;

public interface IAudioPlayer : IDisposable
{
    void Play(string path, float volume);
    void Stop();
}

/// <summary>Plays one sound at a time without waiting for playback to finish.</summary>
public sealed class LinuxAudioPlayer : IAudioPlayer
{
    private readonly string searchPath;
    private readonly Func<ProcessStartInfo, Process> start;
    private Process process;
    private readonly object gate = new();
    private bool disposed;

    public LinuxAudioPlayer(string searchPath = null, Func<ProcessStartInfo, Process> start = null)
    {
        this.searchPath = searchPath ?? Environment.GetEnvironmentVariable("PATH") ?? "";
        this.start = start ?? Process.Start;
    }

    public static string FindExecutable(string name, string searchPath)
    {
        foreach (string directory in searchPath.Split(Path.PathSeparator))
        {
            string path = Path.GetFullPath(Path.Combine(string.IsNullOrEmpty(directory) ? "." : directory, name));
            if (!File.Exists(path)) continue;
            try
            {
                if (OperatingSystem.IsWindows() || (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0)
                    return path;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return null;
    }

    public ProcessStartInfo CreateStartInfo(string path, float volume)
    {
        volume = float.IsFinite(volume) ? Math.Clamp(volume, 0, 1) : 0;
        foreach (string name in new[] { "pw-play", "paplay", "mpv", "ffplay" })
        {
            string executable = FindExecutable(name, searchPath);
            if (executable == null) continue;
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
            string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
            string[] args = name switch
            {
                "pw-play" => ["--volume=" + Number(volume), "--", path],
                "paplay" => ["--volume=" + Math.Round(volume * 65536).ToString(CultureInfo.InvariantCulture), "--", path],
                "mpv" => ["--no-config", "--no-video", "--no-terminal", "--volume=" + Number(volume * 100), "--", path],
                _ => ["-nodisp", "-autoexit", "-loglevel", "quiet", "-volume", Number(Math.Round(volume * 100)), "-i", path],
            };
            foreach (string arg in args) info.ArgumentList.Add(arg);
            return info;
        }
        throw new NotSupportedException("Sound playback requires pw-play, paplay, mpv, or ffplay on PATH.");
    }

    public void Play(string path, float volume)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Stop();
            process = start(CreateStartInfo(Path.GetFullPath(path), volume));
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            if (process == null) return;
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            finally { process.Dispose(); process = null; }
        }
    }

    public void Dispose()
    {
        lock (gate) { Stop(); disposed = true; }
    }
}
