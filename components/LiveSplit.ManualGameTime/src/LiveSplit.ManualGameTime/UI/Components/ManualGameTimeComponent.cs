using LiveSplit.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.UI.Components;

[GlobalFontConsumer(GlobalFont.TextFont)]
public class ManualGameTimeComponent : IComponent
{
    private readonly LiveSplitState state;
    private readonly ITimerModel model;
    private readonly SimpleLabel label = new();
    private readonly GraphicsCache cache = new();

    public ManualGameTimeSettings Settings { get; } = new();
    public string InputText { get; set; } = "";
    public string InputError { get; private set; }
    public bool IsInputActive => state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused;
    public string ComponentName => "Manual Game Time";
    public float VerticalHeight => 36;
    public float HorizontalWidth => 320;
    public float MinimumWidth => 100;
    public float MinimumHeight => 36;
    public float PaddingTop => 0;
    public float PaddingBottom => 0;
    public float PaddingLeft => 0;
    public float PaddingRight => 0;
    public IDictionary<string, Action> ContextMenuControls => null;

    public ManualGameTimeComponent(LiveSplitState state)
    {
        this.state = state;
        model = new TimerModel { CurrentState = state };
        state.OnStart += OnStart;
        state.OnReset += OnReset;
        state.OnUndoSplit += OnUndoSplit;
    }

    private void ClearInput()
    {
        InputText = "";
        InputError = null;
    }

    private void OnStart(object sender, EventArgs e)
    {
        ClearInput();
        state.IsGameTimePaused = true;
        state.SetGameTime(TimeSpan.Zero);
    }

    private void OnReset(object sender, TimerPhase e) => ClearInput();

    private void OnUndoSplit(object sender, EventArgs e)
    {
        int index = state.CurrentSplitIndex;
        state.SetGameTime(index > 0 ? state.Run[index - 1].SplitTime.GameTime : TimeSpan.Zero);
    }

    /// <summary>Receives composed text from the host while this component has focus.</summary>
    public void AppendText(string text)
    {
        if (!IsInputActive || string.IsNullOrEmpty(text)) return;
        InputText += text.Replace("\r", "").Replace("\n", "");
        InputError = null;
    }

    /// <summary>Receives local editing keys from the focused host; see the UI contract.</summary>
    public void HandleKeyDown(KeyEventArgs e)
    {
        if (!IsInputActive) return;
        switch (e.KeyCode)
        {
            case Keys.Enter:
                SubmitInput();
                break;
            case Keys.Back:
                int[] elements = StringInfo.ParseCombiningCharacters(InputText ?? "");
                if (elements.Length > 0) InputText = InputText[..elements[^1]];
                InputError = null;
                break;
            case Keys.Delete:
            case Keys.Escape:
                ClearInput();
                break;
            default:
                return;
        }
        e.Handled = e.SuppressKeyPress = true;
    }

    public bool SubmitInput()
    {
        if (!IsInputActive) return false;
        TimeSpan newTime;
        try
        {
            TimeSpan total = TimeSpan.Zero;
            foreach (string part in (InputText ?? "").Replace(" ", "").Split('+'))
            {
                total += TimeSpanParser.Parse(part);
            }
            newTime = total + (Settings.UseSegmentTimes ? state.CurrentTime.GameTime ?? TimeSpan.Zero : TimeSpan.Zero);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            InputError = "Invalid game time";
            return false;
        }

        state.SetGameTime(newTime);
        model.Split();
        ClearInput();
        return true;
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        label.Text = !IsInputActive ? "Start the timer to enter game time"
            : InputError != null ? InputError + ": " + InputText : "Enter Game Time: " + InputText;
        cache.Restart();
        cache["Text"] = label.Text;
        if (cache.HasChanged) invalidator?.Invalidate(0, 0, width, height);
    }

    private void Draw(Graphics g, LiveSplitState state, float width, float height)
    {
        label.X = 5;
        label.Y = 0;
        label.Width = width - 10;
        label.Height = height;
        label.Font = state.LayoutSettings.TextFont;
        label.ForeColor = state.LayoutSettings.TextColor;
        label.HasShadow = state.LayoutSettings.DropShadows;
        label.HorizontalAlignment = StringAlignment.Near;
        label.VerticalAlignment = StringAlignment.Center;
        label.Draw(g);
    }

    public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion) => Draw(g, state, HorizontalWidth, height);
    public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion) => Draw(g, state, width, VerticalHeight);
    public object GetSettingsControl(LayoutMode mode) => Settings;
    public XmlNode GetSettings(XmlDocument document) => Settings.GetSettings(document);
    public void SetSettings(XmlNode settings) => Settings.SetSettings(settings);
    public int GetSettingsHashCode() => Settings.GetSettingsHashCode();

    public void Dispose()
    {
        state.OnStart -= OnStart;
        state.OnReset -= OnReset;
        state.OnUndoSplit -= OnUndoSplit;
    }
}
