using LiveSplit.Localization;
using LiveSplit.TimeFormatters;
using System;
using System.Drawing;
using System.Linq;
using System.Xml;

namespace LiveSplit.UI.Components;

public class TimerSettings : SettingsBase
{
    private static string T(string source) => UiLocalizer.Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());

    /// <summary>Items of the digits format drop down (<see cref="DigitsFormat"/>).</summary>
    public static readonly string[] DigitsFormatOptions = ["1", "00:01", "0:00:01", "00:00:01"];
    /// <summary>Items of the accuracy drop down (<see cref="Accuracy"/>); the first item is the empty string.</summary>
    public static readonly string[] AccuracyOptions = ["", ".2", ".23", ".234"];
    /// <summary>Items of the timing method drop down (<see cref="TimingMethod"/>).</summary>
    public static readonly string[] TimingMethodOptions = ["Current Timing Method", "Real Time", "Game Time"];
    /// <summary>Items of the background gradient drop down (<see cref="GradientString"/>), in enum order.</summary>
    public static readonly string[] GradientOptions = [.. Enum.GetValues<DeltasGradientType>().Select(GetBackgroundTypeString)];

    private float timerHeight;
    private float timerWidth;
    private float decimalsSize;
    private string digitsFormat;
    private string accuracy;
    private LayoutMode mode;
    private Color timerColor;
    private bool overrideSplitColors;
    private bool centerTimer;
    private bool showGradient;
    private string timingMethod;
    private Color backgroundColor;
    private Color backgroundColor2;
    private DeltasGradientType backgroundGradient;

    public float TimerHeight
    {
        get => timerHeight;
        set
        {
            if (SetField(ref timerHeight, value))
            {
                OnPropertyChanged(nameof(SizeValue));
            }
        }
    }

    public float TimerWidth
    {
        get => timerWidth;
        set
        {
            if (SetField(ref timerWidth, value))
            {
                OnPropertyChanged(nameof(SizeValue));
            }
        }
    }

    public float DecimalsSize { get => decimalsSize; set => SetField(ref decimalsSize, value); }

    private string timerFormat
    {
        get => DigitsFormat + Accuracy;
        set
        {
            int decimalIndex = value.IndexOf('.');
            if (decimalIndex < 0)
            {
                DigitsFormat = value;
                Accuracy = "";
            }
            else
            {
                DigitsFormat = value[..decimalIndex];
                Accuracy = value[decimalIndex..];
            }
        }
    }
    public string DigitsFormat { get => digitsFormat; set => SetField(ref digitsFormat, value); }
    public string Accuracy { get => accuracy; set => SetField(ref accuracy, value); }

    public LayoutMode Mode
    {
        get => mode;
        set
        {
            if (SetField(ref mode, value))
            {
                OnPropertyChanged(nameof(SizeValue));
                OnPropertyChanged(nameof(SizeMinimum));
                OnPropertyChanged(nameof(SizeMaximum));
                OnPropertyChanged(nameof(SizeLabel));
            }
        }
    }

    public Color TimerColor { get => timerColor; set => SetField(ref timerColor, value); }
    public bool OverrideSplitColors
    {
        get => overrideSplitColors;
        set
        {
            if (SetField(ref overrideSplitColors, value))
            {
                OnPropertyChanged(nameof(IsTimerColorEnabled));
            }
        }
    }

    public bool CenterTimer { get => centerTimer; set => SetField(ref centerTimer, value); }

    public bool ShowGradient { get => showGradient; set => SetField(ref showGradient, value); }

    public string TimingMethod { get => timingMethod; set => SetField(ref timingMethod, value); }

    public Color BackgroundColor
    {
        get => backgroundColor;
        set
        {
            if (SetField(ref backgroundColor, value))
            {
                OnPropertyChanged(nameof(BackgroundSwatch2Color));
            }
        }
    }

    public Color BackgroundColor2
    {
        get => backgroundColor2;
        set
        {
            if (SetField(ref backgroundColor2, value))
            {
                OnPropertyChanged(nameof(BackgroundSwatch2Color));
            }
        }
    }

    public DeltasGradientType BackgroundGradient
    {
        get => backgroundGradient;
        set
        {
            if (SetField(ref backgroundGradient, value))
            {
                OnPropertyChanged(nameof(GradientString));
                OnPropertyChanged(nameof(IsColor1Visible));
                OnPropertyChanged(nameof(IsColor2Visible));
                OnPropertyChanged(nameof(BackgroundSwatch2Color));
            }
        }
    }

    public string GradientString
    {
        get => GetBackgroundTypeString(BackgroundGradient);
        set => BackgroundGradient = (DeltasGradientType)Enum.Parse(typeof(DeltasGradientType), value.Replace(" ", ""));
    }

    // Computed, UI facing state that used to live in the WinForms event handlers.

    /// <summary>The first background swatch is only shown for the two colour gradients.</summary>
    public bool IsColor1Visible => BackgroundGradient is not DeltasGradientType.Plain && !GradientString.Contains("Delta");

    /// <summary>The second background swatch is hidden when the colour is derived from the timer colour.</summary>
    public bool IsColor2Visible => !GradientString.Contains("Delta");

    /// <summary>
    /// What the second background swatch edits: <see cref="BackgroundColor2"/> when the first swatch is visible,
    /// otherwise (plain background) <see cref="BackgroundColor"/>.
    /// </summary>
    public Color BackgroundSwatch2Color
    {
        get => IsColor1Visible ? BackgroundColor2 : BackgroundColor;
        set
        {
            if (IsColor1Visible)
            {
                BackgroundColor2 = value;
            }
            else
            {
                BackgroundColor = value;
            }
        }
    }

    /// <summary>The "Color:" row of the "Timer Color" group is enabled only while the layout colour is overridden.</summary>
    public bool IsTimerColorEnabled => OverrideSplitColors;

    /// <summary>The width (horizontal layout) or height (vertical layout) slider.</summary>
    public float SizeValue
    {
        get => Mode == LayoutMode.Horizontal ? TimerWidth : TimerHeight;
        set
        {
            if (Mode == LayoutMode.Horizontal)
            {
                TimerWidth = value;
            }
            else
            {
                TimerHeight = value;
            }
        }
    }
    public float SizeMinimum => Mode == LayoutMode.Horizontal ? 50 : 20;
    public float SizeMaximum => Mode == LayoutMode.Horizontal ? 500 : 150;
    public string SizeLabel => T(Mode == LayoutMode.Horizontal ? "Width:" : "Height:");

    public TimerSettings()
    {
        TimerWidth = 225;
        TimerHeight = 50;
        DigitsFormat = "1";
        Accuracy = ".23";
        TimerColor = Color.FromArgb(170, 170, 170);
        OverrideSplitColors = false;
        ShowGradient = true;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = DeltasGradientType.Plain;
        CenterTimer = false;
        TimingMethod = "Current Timing Method";
        DecimalsSize = 35f;
    }

    public static string GetBackgroundTypeString(DeltasGradientType type)
    {
        return type switch
        {
            DeltasGradientType.Horizontal => "Horizontal",
            DeltasGradientType.HorizontalWithDeltaColor => "Horizontal With Delta Color",
            DeltasGradientType.PlainWithDeltaColor => "Plain With Delta Color",
            DeltasGradientType.Vertical => "Vertical",
            DeltasGradientType.VerticalWithDeltaColor => "Vertical With Delta Color",
            _ => "Plain",
        };
    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);

        TimerHeight = SettingsHelper.ParseFloat(element["TimerHeight"]);
        TimerWidth = SettingsHelper.ParseFloat(element["TimerWidth"]);
        ShowGradient = SettingsHelper.ParseBool(element["ShowGradient"], true);
        TimerColor = SettingsHelper.ParseColor(element["TimerColor"], Color.FromArgb(170, 170, 170));
        DecimalsSize = SettingsHelper.ParseFloat(element["DecimalsSize"], 35f);
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"], Color.Transparent);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"], Color.Transparent);
        BackgroundGradient = SettingsHelper.ParseEnum(element["BackgroundGradient"], DeltasGradientType.Plain);
        CenterTimer = SettingsHelper.ParseBool(element["CenterTimer"], false);
        TimingMethod = SettingsHelper.ParseString(element["TimingMethod"], "Current Timing Method");

        OverrideSplitColors = version >= new Version(1, 3)
            ? SettingsHelper.ParseBool(element["OverrideSplitColors"])
            : !SettingsHelper.ParseBool(element["UseSplitColors"], true);

        if (version >= new Version(1, 2))
        {
            if (version >= new Version(1, 5))
            {
                timerFormat = SettingsHelper.ParseString(element["TimerFormat"]);
            }
            else
            {
                TimeAccuracy accuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["TimerAccuracy"]);
                DigitsFormat = "1";
                Accuracy = accuracy switch
                {
                    TimeAccuracy.Hundredths => ".23",
                    TimeAccuracy.Tenths => ".2",
                    _ => "",
                };
            }
        }
        else
        {
            DigitsFormat = "1";
            Accuracy = ".23";
        }
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
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.5") ^
        SettingsHelper.CreateSetting(document, parent, "TimerHeight", TimerHeight) ^
        SettingsHelper.CreateSetting(document, parent, "TimerWidth", TimerWidth) ^
        SettingsHelper.CreateSetting(document, parent, "TimerFormat", timerFormat) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideSplitColors", OverrideSplitColors) ^
        SettingsHelper.CreateSetting(document, parent, "ShowGradient", ShowGradient) ^
        SettingsHelper.CreateSetting(document, parent, "TimerColor", TimerColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "CenterTimer", CenterTimer) ^
        SettingsHelper.CreateSetting(document, parent, "TimingMethod", TimingMethod) ^
        SettingsHelper.CreateSetting(document, parent, "DecimalsSize", DecimalsSize);
    }
}
