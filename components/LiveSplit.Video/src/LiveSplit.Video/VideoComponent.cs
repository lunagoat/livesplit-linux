using LiveSplit.Hosting;
using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Xml;

namespace LiveSplit.Video;

/// <summary>External playback. Native video surface embedding is deferred to the UI host.</summary>
public class VideoComponent : LogicComponent, IDeactivatableComponent
{
    public VideoSettings Settings { get; } = new();
    public LiveSplitState State { get; }
    private readonly IVideoPlayer player;
    private bool activated = true;
    public bool Activated
    {
        get => activated;
        set { activated = value; if (!value) player.Stop(); }
    }
    public override string ComponentName => "Video";

    public VideoComponent(LiveSplitState state) : this(state, new MpvVideoPlayer()) { }
    public VideoComponent(LiveSplitState state, IVideoPlayer player)
    {
        State = state;
        this.player = player;
        State.OnStart += OnStart;
        State.OnReset += OnReset;
        State.OnPause += OnPause;
        State.OnResume += OnResume;
    }
    private void TryPlayback(Action action)
    {
        try { action(); }
        catch (Exception ex) { Log.Error(ex); }
    }
    private TimeSpan Position(TimeSpan extra) => TimeSpan.FromSeconds(Math.Max(0, ((State.CurrentTime.RealTime ?? TimeSpan.Zero) + Settings.Offset + extra).TotalSeconds));
    private void OnStart(object sender, EventArgs e)
    {
        if (Activated && !string.IsNullOrWhiteSpace(Settings.VideoPath))
            TryPlayback(() => player.Start(PathCompat.Resolve(Settings.VideoPath), Position(TimeSpan.Zero)));
    }
    private void OnReset(object sender, TimerPhase phase) => player.Stop();
    private void OnPause(object sender, EventArgs e) { if (Activated) TryPlayback(() => player.SetPaused(true)); }
    private void OnResume(object sender, EventArgs e) { if (Activated) TryPlayback(() => player.SetPaused(false)); }
    public void Synchronize() => Synchronize(TimeSpan.Zero);
    public void Synchronize(TimeSpan offset) { if (Activated) TryPlayback(() => player.Seek(Position(offset))); }
    public override object GetSettingsControl(LayoutMode mode) { Settings.Mode = mode; return Settings; }
    public override XmlNode GetSettings(XmlDocument document) => Settings.GetSettings(document);
    public override void SetSettings(XmlNode settings) => Settings.SetSettings(settings);
    public int GetSettingsHashCode() => Settings.GetSettingsHashCode();
    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }
    public override void Dispose()
    {
        State.OnStart -= OnStart;
        State.OnReset -= OnReset;
        State.OnPause -= OnPause;
        State.OnResume -= OnResume;
        player.Dispose();
    }
}
