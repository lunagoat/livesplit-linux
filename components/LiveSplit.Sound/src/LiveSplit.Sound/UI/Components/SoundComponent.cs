using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Xml;

namespace LiveSplit.UI.Components;

public class SoundComponent : LogicComponent, IDeactivatableComponent
{
    public override string ComponentName => "Sound Effects";

    private bool activated = true;
    public bool Activated
    {
        get => activated;
        set { activated = value; if (!value) Player?.Stop(); }
    }

    private LiveSplitState State { get; set; }
    private SoundSettings Settings { get; set; }
    private IAudioPlayer Player { get; set; }
    private Random Rnd { get; set; }

    public SoundComponent(LiveSplitState state) : this(state, new LinuxAudioPlayer()) { }

    public SoundComponent(LiveSplitState state, IAudioPlayer player)
    {
        Activated = true;

        State = state;
        Settings = new SoundSettings();
        Player = player;

        State.OnStart += State_OnStart;
        State.OnSplit += State_OnSplit;
        State.OnSkipSplit += State_OnSkipSplit;
        State.OnUndoSplit += State_OnUndoSplit;
        State.OnPause += State_OnPause;
        State.OnResume += State_OnResume;
        State.OnReset += State_OnReset;

        Rnd = new Random();
    }

    public override void Dispose()
    {
        State.OnStart -= State_OnStart;
        State.OnSplit -= State_OnSplit;
        State.OnSkipSplit -= State_OnSkipSplit;
        State.OnUndoSplit -= State_OnUndoSplit;
        State.OnPause -= State_OnPause;
        State.OnResume -= State_OnResume;
        State.OnReset -= State_OnReset;

        Player.Dispose();
    }

    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }

    public override object GetSettingsControl(LayoutMode mode)
    {
        return Settings;
    }

    public override XmlNode GetSettings(XmlDocument document)
    {
        return Settings.GetSettings(document);
    }

    public override void SetSettings(XmlNode settings)
    {
        Settings.SetSettings(settings);
    }

    private void State_OnStart(object sender, EventArgs e)
    {
        PlaySound(EventType.StartTimer);
    }

    private void State_OnSplit(object sender, EventArgs e)
    {
        if (State.CurrentPhase == TimerPhase.Ended)
        {
            if (State.Run[^1].PersonalBestSplitTime[State.CurrentTimingMethod] == null
                || State.Run[^1].SplitTime[State.CurrentTimingMethod] < State.Run[^1].PersonalBestSplitTime[State.CurrentTimingMethod])
            {
                PlaySound(EventType.PersonalBest);
            }
            else
            {
                PlaySound(EventType.NotAPersonalBest);
            }

            return;
        }

        EventType type = EventType.Split;

        int splitIndex = State.CurrentSplitIndex - 1;
        TimeSpan? timeDifference = State.Run[splitIndex].SplitTime[State.CurrentTimingMethod] - State.Run[splitIndex].Comparisons[State.CurrentComparison][State.CurrentTimingMethod];

        if (timeDifference != null)
        {
            if (timeDifference < TimeSpan.Zero)
            {
                type = EventType.SplitAheadGaining;

                if (LiveSplitStateHelper.GetPreviousSegmentDelta(State, splitIndex, State.CurrentComparison, State.CurrentTimingMethod) > TimeSpan.Zero)
                {
                    type = EventType.SplitAheadLosing;
                }
            }
            else
            {
                type = EventType.SplitBehindLosing;

                if (LiveSplitStateHelper.GetPreviousSegmentDelta(State, splitIndex, State.CurrentComparison, State.CurrentTimingMethod) < TimeSpan.Zero)
                {
                    type = EventType.SplitBehindGaining;
                }
            }
        }

        //Check for best segment
        TimeSpan? curSegment = LiveSplitStateHelper.GetPreviousSegmentTime(State, splitIndex, State.CurrentTimingMethod);

        if (curSegment != null)
        {
            if (State.Run[splitIndex].BestSegmentTime[State.CurrentTimingMethod] == null || curSegment < State.Run[splitIndex].BestSegmentTime[State.CurrentTimingMethod])
            {
                type = EventType.BestSegment;
            }
        }

        if (Settings.SoundDataDictionary[type].FilePaths.Count == 0)
        {
            type = EventType.Split;
        }

        PlaySound(type);
    }

    private void State_OnSkipSplit(object sender, EventArgs e)
    {
        PlaySound(EventType.SkipSplit);
    }

    private void State_OnUndoSplit(object sender, EventArgs e)
    {
        PlaySound(EventType.UndoSplit);
    }

    private void State_OnPause(object sender, EventArgs e)
    {
        PlaySound(EventType.Pause);
    }

    private void State_OnResume(object sender, EventArgs e)
    {
        PlaySound(EventType.Resume);
    }

    private void State_OnReset(object sender, TimerPhase e)
    {
        if (e != TimerPhase.Ended)
        {
            PlaySound(EventType.Reset);
        }
    }

    private void PlaySound(EventType type)
    {
        Player.Stop();

        if (!Activated)
        {
            return;
        }

        string[] existingPaths = [.. Settings.SoundDataDictionary[type].FilePaths.Select(path => PathCompat.Resolve(path)).Where(File.Exists)];
        if (existingPaths.Length == 0)
        {
            return;
        }

        int index = Rnd.Next(0, existingPaths.Length);
        string path = existingPaths[index];

        int volume = Settings.SoundDataDictionary[type].Volume;

        try
        {
            Player.Play(path, volume / 100f * (Settings.GeneralVolume / 100f));
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    public int GetSettingsHashCode()
    {
        return Settings.GetSettingsHashCode();
    }
}
