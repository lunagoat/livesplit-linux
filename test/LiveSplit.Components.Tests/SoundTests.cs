using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Xunit;

namespace LiveSplit.Components.Tests;

public class SoundTests
{
    [Fact]
    public void PreserveSettingsAndLegacyPaths()
    {
        var settings = new SoundSettings { GeneralVolume = 43, OutputDevice = 7, IsClearAddDragDrop = false };
        foreach (EventType type in Enum.GetValues<EventType>())
        {
            settings.SoundDataDictionary[type].FilePaths = [@"C:\Music\a & b.wav", "/tmp/second.mp3"];
            settings.SoundDataDictionary[type].Volume = (int)type + 30;
        }
        var loaded = new SoundSettings();
        XmlNode xml = settings.GetSettings(new XmlDocument());
        loaded.SetSettings(xml);
        Assert.Equal(xml.OuterXml, loaded.GetSettings(new XmlDocument()).OuterXml);
        Assert.Equal(settings.GetSettingsHashCode(), loaded.GetSettingsHashCode());
        Assert.False(loaded.IsOutputDeviceEnabled);
        var doc = new XmlDocument();
        doc.LoadXml("<Settings><Split>C:\\old.wav</Split><SplitVolume>75</SplitVolume></Settings>");
        loaded.SetSettings(doc.DocumentElement);
        Assert.Equal([@"C:\old.wav"], loaded.SoundDataDictionary[EventType.Split].FilePaths);
        Assert.True(loaded.IsClearAddDragDrop);
        Assert.Equal(100, loaded.GeneralVolume);
        Assert.All(new SoundSettings().SoundDataDictionary.Values, data => Assert.Equal(100, data.Volume));
    }

    [Fact]
    public void FileAndVolumeRowsNotifyAndApplyDropPolicy()
    {
        var data = new SoundData([], 100);
        var files = new SoundFileSettings(EventType.Split, data);
        var volume = new SoundVolumeSettings(EventType.Split, data);
        var changes = new List<string>();
        files.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        files.DropFiles(["a", "b"]);
        Assert.Contains(nameof(files.FilePaths), changes);
        Assert.Contains(nameof(files.FilePathsText), changes);
        files.IsClearAddDragDrop = false;
        files.DropFiles(["b", "c"]);
        Assert.Equal(["a", "b", "c"], files.FilePaths);
        Assert.Equal("a\nb\nc", files.PathsTooltip);
        volume.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        volume.Volume = 32;
        Assert.Contains(nameof(volume.Volume), changes);
        changes.Clear();
        volume.Volume = 32;
        Assert.Empty(changes);
        files.Clear();
        Assert.Empty(data.FilePaths);
    }

    [Theory]
    [InlineData("pw-play", "--volume=0.25")]
    [InlineData("paplay", "--volume=16384")]
    [InlineData("mpv", "--volume=25")]
    [InlineData("ffplay", "25")]
    public void SelectBackendFromFakePathAndPassVolume(string backend, string volumeArgument)
    {
        using var dir = new TemporaryMediaDirectory();
        string[] names = ["pw-play", "paplay", "mpv", "ffplay"];
        foreach (string name in names.Skip(Array.IndexOf(names, backend))) dir.Executable(name);
        // An earlier non-executable file must not win selection.
        if (backend != "pw-play") File.WriteAllText(Path.Combine(dir.Path, "pw-play"), "not executable");
        using var player = new LinuxAudioPlayer(dir.Path);
        string path = "/tmp/a file;$(false).wav";
        ProcessStartInfo info = player.CreateStartInfo(path, .25f);
        Assert.Equal(Path.Combine(dir.Path, backend), info.FileName);
        Assert.False(info.UseShellExecute);
        Assert.Contains(volumeArgument, info.ArgumentList);
        Assert.Equal(path, info.ArgumentList.Last());
        if (backend == "mpv") Assert.Contains("--no-video", info.ArgumentList);
    }

    [Fact]
    public async Task PlaybackDoesNotWaitAndCanBeKilledOrReplaced()
    {
        using var dir = new TemporaryMediaDirectory();
        dir.Executable("pw-play");
        var processes = new List<Process>();
        using var player = new LinuxAudioPlayer(dir.Path, info =>
        {
            var process = Process.Start(info);
            processes.Add(Process.GetProcessById(process.Id));
            return process;
        });
        player.Play("/tmp/a.wav", .5f);
        Assert.False(processes[0].HasExited);
        player.Play("/tmp/b.wav", .5f);
        await processes[0].WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        player.Stop();
        await processes[1].WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        foreach (Process process in processes) process.Dispose();
    }

    [Fact]
    public void MissingBackendIsExplicit()
    {
        using var dir = new TemporaryMediaDirectory();
        using var player = new LinuxAudioPlayer(dir.Path);
        Assert.Throws<NotSupportedException>(() => player.Play("/tmp/a.wav", 1));
    }

    [Fact]
    public void TimerEventsUseResolvedPathsAndCombinedVolumeAndDetachOnDispose()
    {
        using var dir = new TemporaryMediaDirectory();
        string path = Path.Combine(dir.Path, "Sound File.wav");
        File.WriteAllText(path, "fake audio");
        var state = TestSupport.CreateState();
        var timer = new TimerModel { CurrentState = state };
        var fake = new FakeAudioPlayer();
        var component = new SoundComponent(state, fake);
        var settings = (SoundSettings)component.GetSettingsControl(LayoutMode.Vertical);
        settings.GeneralVolume = 50;
        foreach (SoundData data in settings.SoundDataDictionary.Values)
        {
            data.FilePaths = ["Z:" + path.Replace('/', '\\').Replace("Sound File", "sound file")];
            data.Volume = 40;
        }
        timer.Start();
        Assert.Equal(path, fake.Path);
        Assert.Equal(.2f, fake.Volume, 4);
        timer.Pause();
        timer.Pause();
        timer.Reset(false);
        Assert.Equal(4, fake.Plays);
        component.Activated = false;
        timer.Start();
        Assert.Equal(4, fake.Plays);
        component.Dispose();
        Assert.True(fake.Disposed);
        int stops = fake.Stops;
        timer.Reset(false);
        Assert.Equal(stops, fake.Stops);
    }

    private sealed class FakeAudioPlayer : IAudioPlayer
    {
        public string Path;
        public float Volume;
        public int Plays, Stops;
        public bool Disposed;
        public void Play(string path, float volume) { Path = path; Volume = volume; Plays++; }
        public void Stop() => Stops++;
        public void Dispose() => Disposed = true;
    }
}

internal sealed class TemporaryMediaDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "media-test-" + Guid.NewGuid().ToString("N"));
    public TemporaryMediaDirectory() => Directory.CreateDirectory(Path);
    public void Executable(string name)
    {
        string file = System.IO.Path.Combine(Path, name);
        File.WriteAllText(file, "#!/bin/sh\nexec /bin/sleep 60\n");
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    public void Dispose() => Directory.Delete(Path, true);
}
