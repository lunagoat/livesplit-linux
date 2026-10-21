using LiveSplit.Options;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace LiveSplit.Video;

public interface IVideoPlayer : IDisposable
{
    void Start(string path, TimeSpan startOffset);
    void SetPaused(bool paused);
    void Seek(TimeSpan position);
    void Stop();
}

/// <summary>Owns a muted external mpv window and a private JSON IPC session.</summary>
public sealed class MpvVideoPlayer : IVideoPlayer
{
    private readonly Func<ProcessStartInfo, Process> start;
    private readonly string executable;
    private readonly Func<string, CancellationToken, Task<Stream>> connect;
    private readonly object gate = new();
    private Process process;
    private CancellationTokenSource cancellation;
    private Channel<object[]> commands;
    private string socketPath;
    private string socketDirectory;
    private bool disposed;
    public Task Completion { get; private set; } = Task.CompletedTask;

    public MpvVideoPlayer(string executable = "mpv", Func<ProcessStartInfo, Process> start = null, Func<string, CancellationToken, Task<Stream>> connect = null)
    {
        this.executable = executable;
        this.start = start ?? Process.Start;
        this.connect = connect ?? ConnectAsync;
    }

    public void Start(string path, TimeSpan startOffset)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Stop();
            // A private directory prevents another local user replacing the IPC endpoint.
            socketDirectory = Path.Combine(Path.GetTempPath(), "lsv-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(socketDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            socketPath = Path.Combine(socketDirectory, "mpv.sock");
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
            foreach (string arg in new[] { "--no-config", "--no-terminal", "--mute=yes", "--keep-open=no", "--input-ipc-server=" + socketPath,
                "--start=" + Math.Max(0, startOffset.TotalSeconds).ToString("0.######", CultureInfo.InvariantCulture), "--", Path.GetFullPath(path) })
                info.ArgumentList.Add(arg);
            try
            {
                process = start(info) ?? throw new InvalidOperationException("mpv did not start.");
                cancellation = new CancellationTokenSource();
                commands = Channel.CreateUnbounded<object[]>(new UnboundedChannelOptions { SingleReader = true });
                string sessionPath = socketPath;
                ChannelReader<object[]> sessionCommands = commands.Reader;
                CancellationToken sessionToken = cancellation.Token;
                Completion = Task.Run(() => RunIpcAsync(sessionPath, sessionCommands, sessionToken));
            }
            catch { Stop(); throw; }
        }
    }

    private static async Task<Stream> ConnectAsync(string path, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(path), timeout.Token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException)
            {
                socket.Dispose();
                await Task.Delay(25, timeout.Token);
            }
            catch { socket.Dispose(); throw; }
        }
    }

    private async Task RunIpcAsync(string path, ChannelReader<object[]> reader, CancellationToken token)
    {
        try
        {
            using Stream stream = await connect(path, token);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            // Drain mpv replies/events so its output cannot fill the socket and block commands.
            Task drain = DrainAsync(stream, token);
            try
            {
                await foreach (object[] command in reader.ReadAllAsync(token))
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new { command }).AsMemory(), token);
            }
            finally
            {
                stream.Dispose();
                await drain;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { Log.Error(ex); }
    }

    private static async Task DrainAsync(Stream stream, CancellationToken token)
    {
        try
        {
            byte[] buffer = new byte[4096];
            while (await stream.ReadAsync(buffer, token) != 0) { }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException or SocketException) { }
    }

    public void SetPaused(bool paused) { lock (gate) commands?.Writer.TryWrite(["set_property", "pause", paused]); }
    public void Seek(TimeSpan position) { lock (gate) commands?.Writer.TryWrite(["seek", Math.Max(0, position.TotalSeconds), "absolute+exact"]); }

    public void Stop()
    {
        lock (gate)
        {
            cancellation?.Cancel();
            commands?.Writer.TryComplete();
            commands = null;
            cancellation?.Dispose();
            cancellation = null;
            try
            {
                if (process != null && !process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { }
            finally
            {
                Process stoppedProcess = process;
                process = null;
                string directory = socketDirectory;
                socketDirectory = null;
                socketPath = null;
                Completion = CleanupAsync(directory, stoppedProcess, Completion);
            }

        }
    }

    private static async Task CleanupAsync(string directory, Process stoppedProcess, Task completion)
    {
        await completion.ConfigureAwait(false);
        if (stoppedProcess != null)
        {
            try { await stoppedProcess.WaitForExitAsync().ConfigureAwait(false); }
            finally { stoppedProcess.Dispose(); }
        }
        if (directory == null) return;
        try { Directory.Delete(directory, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    public void Dispose() { lock (gate) { Stop(); disposed = true; } }
}
