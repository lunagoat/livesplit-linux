using LiveSplit.Model.Input;
using LiveSplit.Options;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.UI.Components;

public class CounterComponentSettings : SettingsBase, IDisposable
{
    public CompositeHook Hook { get; }
    public CounterComponentSettings() : this(false) { }
    public CounterComponentSettings(bool allowGamepads)
    {
        Hook = new CompositeHook(allowGamepads);
        // Set default values.
        GlobalHotkeysEnabled = false;
        CounterTextColor = Color.FromArgb(255, 255, 255, 255);
        CounterValueColor = Color.FromArgb(255, 255, 255, 255);
        OverrideTextColor = false;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = GradientType.Plain;
        CounterText = "Counter:";
        InitialValue = 0;
        Increment = 1;

        // Hotkeys
        IncrementKey = new KeyOrButton(Keys.Add);
        DecrementKey = new KeyOrButton(Keys.Subtract);
        ResetKey = new KeyOrButton(Keys.NumPad0);

    }

    private bool globalHotkeysEnabled;
    public bool GlobalHotkeysEnabled { get => globalHotkeysEnabled; set => SetField(ref globalHotkeysEnabled, value); }

    private Color counterTextColor;
    public Color CounterTextColor { get => counterTextColor; set => SetField(ref counterTextColor, value); }
    private Color counterValueColor;
    public Color CounterValueColor { get => counterValueColor; set => SetField(ref counterValueColor, value); }
    private bool overrideTextColor;
    public bool OverrideTextColor { get => overrideTextColor; set { if (SetField(ref overrideTextColor, value)) { OnPropertyChanged(nameof(IsCounterColorEnabled)); } } }

    private Color backgroundColor;
    public Color BackgroundColor { get => backgroundColor; set { if (SetField(ref backgroundColor, value)) { OnPropertyChanged(nameof(BackgroundSwatch2Color)); } } }
    private Color backgroundColor2;
    public Color BackgroundColor2 { get => backgroundColor2; set { if (SetField(ref backgroundColor2, value)) { OnPropertyChanged(nameof(BackgroundSwatch2Color)); } } }
    private GradientType backgroundGradient;
    public GradientType BackgroundGradient { get => backgroundGradient; set { if (SetField(ref backgroundGradient, value)) { OnPropertyChanged(nameof(GradientString)); OnPropertyChanged(nameof(IsColor1Visible)); OnPropertyChanged(nameof(BackgroundSwatch2Color)); } } }
    public string GradientString
    {
        get => BackgroundGradient.ToString();
        set => BackgroundGradient = (GradientType)Enum.Parse(typeof(GradientType), value);
    }

    private string counterText;
    public string CounterText { get => counterText; set => SetField(ref counterText, value); }
    private int initialValue;
    public int InitialValue { get => initialValue; set { if (SetField(ref initialValue, value)) { CounterReinitialiseRequired?.Invoke(this, EventArgs.Empty); } } }
    private int increment;
    public int Increment { get => increment; set { if (SetField(ref increment, value)) { IncrementUpdateRequired?.Invoke(this, EventArgs.Empty); } } }

    // Legacy font override — read from old configs, not written or exposed in UI
    private bool overrideCounterFont;
    public bool OverrideCounterFont { get => overrideCounterFont; set => SetField(ref overrideCounterFont, value); }
    private Font counterFont;
    public Font CounterFont { get => counterFont; set => SetField(ref counterFont, value); }

    private KeyOrButton incrementKey;
    public KeyOrButton IncrementKey { get => incrementKey; set { if (SetField(ref incrementKey, value)) { RegisterHotKeys(); } } }
    private KeyOrButton decrementKey;
    public KeyOrButton DecrementKey { get => decrementKey; set { if (SetField(ref decrementKey, value)) { RegisterHotKeys(); } } }
    private KeyOrButton resetKey;
    public KeyOrButton ResetKey { get => resetKey; set { if (SetField(ref resetKey, value)) { RegisterHotKeys(); } } }

    public event EventHandler CounterReinitialiseRequired;
    public event EventHandler IncrementUpdateRequired;

    public static readonly string[] GradientOptions = ["Plain", "Vertical", "Horizontal"];
    public bool IsCounterColorEnabled => OverrideTextColor;
    public bool IsColor1Visible => BackgroundGradient != GradientType.Plain;
    public Color BackgroundSwatch2Color { get => IsColor1Visible ? BackgroundColor2 : BackgroundColor; set { if (IsColor1Visible) BackgroundColor2 = value; else BackgroundColor = value; } }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        GlobalHotkeysEnabled = SettingsHelper.ParseBool(element["GlobalHotkeysEnabled"]);
        CounterTextColor = SettingsHelper.ParseColor(element["CounterTextColor"]);
        CounterValueColor = SettingsHelper.ParseColor(element["CounterValueColor"]);
        OverrideCounterFont = SettingsHelper.ParseBool(element["OverrideCounterFont"]);
        CounterFont = SettingsHelper.GetFontFromElement(element["CounterFont"]);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"]);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"]);
        GradientString = SettingsHelper.ParseString(element["BackgroundGradient"]);
        CounterText = SettingsHelper.ParseString(element["CounterText"]);
        InitialValue = SettingsHelper.ParseInt(element["InitialValue"]);
        Increment = SettingsHelper.ParseInt(element["Increment"]);

        XmlElement incrementElement = element["IncrementKey"];
        IncrementKey = string.IsNullOrEmpty(incrementElement.InnerText) ? null : new KeyOrButton(incrementElement.InnerText);
        XmlElement decrementElement = element["DecrementKey"];
        DecrementKey = string.IsNullOrEmpty(decrementElement.InnerText) ? null : new KeyOrButton(decrementElement.InnerText);
        XmlElement resetElement = element["ResetKey"];
        ResetKey = string.IsNullOrEmpty(resetElement.InnerText) ? null : new KeyOrButton(resetElement.InnerText);

        RegisterHotKeys();
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        XmlElement parent = document.CreateElement("Settings");
        CreateSettingsNode(document, parent);
        return parent;
    }

    public int GetSettingsHashCode()
    {
        return CreateSettingsNode(null, null);
    }

    private int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.0") ^
        SettingsHelper.CreateSetting(document, parent, "GlobalHotkeysEnabled", GlobalHotkeysEnabled) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
        SettingsHelper.CreateSetting(document, parent, "CounterTextColor", CounterTextColor) ^
        SettingsHelper.CreateSetting(document, parent, "CounterValueColor", CounterValueColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "CounterText", CounterText) ^
        SettingsHelper.CreateSetting(document, parent, "InitialValue", InitialValue) ^
        SettingsHelper.CreateSetting(document, parent, "Increment", Increment) ^
        SettingsHelper.CreateSetting(document, parent, "IncrementKey", IncrementKey) ^
        SettingsHelper.CreateSetting(document, parent, "DecrementKey", DecrementKey) ^
        SettingsHelper.CreateSetting(document, parent, "ResetKey", ResetKey);
    }

    private void RegisterHotKeys()
    {
        Hook.UnregisterAllHotkeys();
        if (IncrementKey != null) Hook.RegisterHotKey(IncrementKey);
        if (DecrementKey != null) Hook.RegisterHotKey(DecrementKey);
        if (ResetKey != null) Hook.RegisterHotKey(ResetKey);
    }

    public void UnregisterAllHotkeys(CompositeHook hook) => hook.UnregisterAllHotkeys();
    public void Dispose() => Hook.Dispose();
}
