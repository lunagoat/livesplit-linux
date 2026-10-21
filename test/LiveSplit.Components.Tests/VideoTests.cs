using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.Video;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Xunit;

namespace LiveSplit.Components.Tests;

public class VideoTests
{
    [Fact]
    public void RoundtripSettingsAndExposeModeDependentSize()
    {
        var settings = new VideoSettings { VideoPath = @"C:\Videos\PB & WR.mkv", Offset = TimeSpan.FromSeconds(12.25), Height = 222, Width = 333 };
        var loaded = new VideoSettings();
        XmlNode xml = settings.GetSettings(new XmlDocument());
        loaded.SetSettings(xml);
        Assert.Equal(xml.OuterXml, loaded.GetSettings(new XmlDocument()).OuterXml);
        Assert.Equal(settings.GetSettingsHashCode(), loaded.GetSettingsHashCode());
        Assert.Equal((200f, 200f, TimeSpan.Zero), (new VideoSettings().Width, new VideoSettings().Height, new VideoSettings().Offset));
        loaded.Mode = LayoutMode.Vertical;
        Assert.Equal((100, 300, 222f), (loaded.SizeMinimum, loaded.SizeMaximum, loaded.SizeValue));
        var changes = new List<string>();
        loaded.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        loaded.Mode = LayoutMode.Horizontal;
        Assert.Contains(nameof(loaded.SizeMaximum), changes);
        Assert.Contains(nameof(loaded.SizeLabel), changes);
        Assert.Contains(nameof(loaded.SizeValue), changes);
        loaded.SizeValue = 345;
        Assert.Equal(345, loaded.Width);
        Assert.Equal(222, loaded.Height);
        loaded.OffsetString = "invalid";
        Assert.Equal(settings.Offset, loaded.Offset);
        Assert.Equal(settings.VideoPath, loaded.VideoPath);
        Assert.StartsWith("file:///", loaded.MRL);
    }

    [Fact]
    public void TimerStartsWithOffsetPausesSeeksResetsAndDetaches()
    {
        using var dir = new TemporaryMediaDirectory();
        string path = Path.Combine(dir.Path, "PB Video.mkv");
        File.WriteAllText(path, "fake video");
        var state = TestSupport.CreateState();
        var timer = new TimerModel { CurrentState = state };
        var fake = new FakeVideoPlayer();
        var component = new VideoComponent(state, fake);
        component.Settings.VideoPath = "Z:" + path.Replace('/', '\\').Replace("PB Video", "pb video");
        component.Settings.Offset = TimeSpan.FromSeconds(30);
        timer.Start();
        Assert.Equal(path, fake.Path);
        Assert.InRange(fake.Offset.TotalSeconds, 30, 31);
        timer.Pause();
        Assert.True(fake.Paused);
        timer.Pause();
        Assert.False(fake.Paused);
        component.Synchronize(TimeSpan.FromSeconds(5));
        Assert.InRange(fake.Position.TotalSeconds, 35, 36);
        timer.Reset(false);
        Assert.Equal(1, fake.Stops);
        component.Settings.Offset = TimeSpan.FromSeconds(-60);
        timer.Start();
        Assert.Equal(TimeSpan.Zero, fake.Offset);
        component.Activated = false;
        Assert.Equal(2, fake.Stops);
        component.Dispose();
        Assert.True(fake.Disposed);
        timer.Reset(false);
        Assert.Equal(2, fake.Stops);
    }

    [Fact]
    public async Task MpvReceivesStartupOptionsAndJsonCommandsAndIsKillable()
    {
        using var dir = new TemporaryMediaDirectory();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var ipc = new FakeIpcStream();
        Process observer = null;
        string socketPath = null;
        ProcessStartInfo captured = null;
        using var player = new MpvVideoPlayer(start: info =>
        {
            captured = info;
            socketPath = info.ArgumentList.Single(x => x.StartsWith("--input-ipc-server=")).Split('=', 2)[1];
            var process = Process.Start(new ProcessStartInfo("/bin/sleep") { ArgumentList = { "60" }, UseShellExecute = false });
            observer = Process.GetProcessById(process.Id);
            return process;
        }, connect: (_, _) => Task.FromResult<Stream>(ipc));
        player.Start(Path.Combine(dir.Path, "PB ; $(false).mp4"), TimeSpan.FromSeconds(12.5));
        Assert.Contains("--start=12.5", captured.ArgumentList);
        Assert.Contains("--mute=yes", captured.ArgumentList);
        Assert.Contains("--", captured.ArgumentList);
        Assert.False(captured.UseShellExecute);
        Assert.False(observer.HasExited);
        player.SetPaused(true);
        player.SetPaused(false);
        player.Seek(TimeSpan.FromSeconds(42.25));
        foreach (string expected in new[] { "[\"set_property\",\"pause\",true]", "[\"set_property\",\"pause\",false]", "[\"seek\",42.25,\"absolute+exact\"]" })
        {
            string line = await ipc.Lines.Reader.ReadAsync(timeout.Token);
            using var json = JsonDocument.Parse(line);
            Assert.Equal(expected, JsonSerializer.Serialize(json.RootElement.GetProperty("command"), new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        }
        player.Stop();
        await player.Completion.WaitAsync(timeout.Token);
        await observer.WaitForExitAsync(timeout.Token);
        observer.Dispose();
        Assert.False(File.Exists(socketPath));
    }

    [Fact]
    public async Task StopCancelsIpcStartupAndDisposalRejectsNewPlayback()
    {
        Process observer = null;
        var player = new MpvVideoPlayer(start: _ =>
        {
            var process = Process.Start(new ProcessStartInfo("/bin/sleep") { ArgumentList = { "60" } });
            observer = Process.GetProcessById(process.Id);
            return process;
        }, connect: async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Stream.Null; });
        player.Start("/tmp/fake.mp4", TimeSpan.Zero);
        player.Dispose();
        await player.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        await observer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        observer.Dispose();
        Assert.Throws<ObjectDisposedException>(() => player.Start("/tmp/fake.mp4", TimeSpan.Zero));
    }

    private sealed class FakeIpcStream : Stream
    {
        public Channel<string> Lines { get; } = Channel.CreateUnbounded<string>();
        private readonly StringBuilder pending = new();
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            byte[] bytes = buffer.ToArray();
            Write(bytes, 0, bytes.Length);
            return ValueTask.CompletedTask;
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            pending.Append(Encoding.UTF8.GetString(buffer, offset, count));
            string text = pending.ToString();
            int newline;
            while ((newline = text.IndexOf('\n')) >= 0)
            {
                Lines.Writer.TryWrite(text[..newline].TrimEnd('\r'));
                text = text[(newline + 1)..];
            }
            pending.Clear().Append(text);
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private sealed class FakeVideoPlayer : IVideoPlayer
    {
        public string Path;
        public TimeSpan Offset, Position;
        public bool Paused, Disposed;
        public int Stops;
        public void Start(string path, TimeSpan startOffset) { Path = path; Offset = startOffset; }
        public void SetPaused(bool paused) => Paused = paused;
        public void Seek(TimeSpan position) => Position = position;
        public void Stop() => Stops++;
        public void Dispose() => Disposed = true;
    }
}
