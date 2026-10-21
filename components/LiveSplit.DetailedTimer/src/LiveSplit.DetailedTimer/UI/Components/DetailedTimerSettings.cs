using LiveSplit.Localization;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.TimeFormatters;
using System;
using System.Drawing;
using System.Linq;
using System.Xml;

namespace LiveSplit.UI.Components;

public class DetailedTimerSettings : SettingsBase
{
    private static string T(string source) => UiLocalizer.Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());

    private float height;
    public float Height
    {
        get => height;
        set
        {
            if (SetField(ref height, value))
            {
                OnPropertyChanged(nameof(SizeValue));
            }
        }
    }
    private float width;
    public float Width
    {
        get => width;
        set
        {
            if (SetField(ref width, value))
            {
                OnPropertyChanged(nameof(SizeValue));
            }
        }
    }
    private float segmentTimerSizeRatio;
    public float SegmentTimerSizeRatio
    {
        get => segmentTimerSizeRatio;
        set => SetField(ref segmentTimerSizeRatio, value);
    }
    private LiveSplitState currentState;
    public LiveSplitState CurrentState
    {
        get => currentState;
        set
        {
            if (SetField(ref currentState, value))
            {
                OnPropertyChanged(nameof(ComparisonOptions));
                OnPropertyChanged(nameof(Comparison2Options));
            }
        }
    }

    private bool timerShowGradient;
    public bool TimerShowGradient
    {
        get => timerShowGradient;
        set => SetField(ref timerShowGradient, value);
    }
    private bool overrideTimerColors;
    public bool OverrideTimerColors
    {
        get => overrideTimerColors;
        set
        {
            if (SetField(ref overrideTimerColors, value))
            {
                OnPropertyChanged(nameof(IsTimerColorEnabled));
            }
        }
    }
    private bool segmentTimerShowGradient;
    public bool SegmentTimerShowGradient
    {
        get => segmentTimerShowGradient;
        set => SetField(ref segmentTimerShowGradient, value);
    }
    private bool showSplitName;
    public bool ShowSplitName
    {
        get => showSplitName;
        set
        {
            if (SetField(ref showSplitName, value))
            {
                OnPropertyChanged(nameof(IsSplitNameEnabled));
            }
        }
    }

    private float iconSize;
    public float IconSize
    {
        get => iconSize;
        set => SetField(ref iconSize, value);
    }
    private bool displayIcon;
    public bool DisplayIcon
    {
        get => displayIcon;
        set
        {
            if (SetField(ref displayIcon, value))
            {
                OnPropertyChanged(nameof(IsIconSizeEnabled));
            }
        }
    }

    private float decimalsSize;
    public float DecimalsSize
    {
        get => decimalsSize;
        set => SetField(ref decimalsSize, value);
    }
    private float segmentTimerDecimalsSize;
    public float SegmentTimerDecimalsSize
    {
        get => segmentTimerDecimalsSize;
        set => SetField(ref segmentTimerDecimalsSize, value);
    }

    private Color timerColor;
    public Color TimerColor
    {
        get => timerColor;
        set => SetField(ref timerColor, value);
    }
    private Color segmentTimerColor;
    public Color SegmentTimerColor
    {
        get => segmentTimerColor;
        set => SetField(ref segmentTimerColor, value);
    }
    private Color segmentLabelsColor;
    public Color SegmentLabelsColor
    {
        get => segmentLabelsColor;
        set => SetField(ref segmentLabelsColor, value);
    }
    private Color segmentTimesColor;
    public Color SegmentTimesColor
    {
        get => segmentTimesColor;
        set => SetField(ref segmentTimesColor, value);
    }
    private Color splitNameColor;
    public Color SplitNameColor
    {
        get => splitNameColor;
        set => SetField(ref splitNameColor, value);
    }

    private Color backgroundColor;
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
    private Color backgroundColor2;
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

    private DeltasGradientType backgroundGradient;
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
        get => TimerSettings.GetBackgroundTypeString(BackgroundGradient);
        set => BackgroundGradient = (DeltasGradientType)Enum.Parse(typeof(DeltasGradientType), value.Replace(" ", ""));
    }
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
    private string digitsFormat;
    public string DigitsFormat
    {
        get => digitsFormat;
        set => SetField(ref digitsFormat, value);
    }
    private string accuracy;
    public string Accuracy
    {
        get => accuracy;
        set => SetField(ref accuracy, value);
    }
    private string segmentTimerFormat
    {
        get => SegmentDigitsFormat + SegmentAccuracy;
        set
        {
            int decimalIndex = value.IndexOf('.');
            if (decimalIndex < 0)
            {
                SegmentDigitsFormat = value;
                SegmentAccuracy = "";
            }
            else
            {
                SegmentDigitsFormat = value[..decimalIndex];
                SegmentAccuracy = value[decimalIndex..];
            }
        }
    }
    private string segmentDigitsFormat;
    public string SegmentDigitsFormat
    {
        get => segmentDigitsFormat;
        set => SetField(ref segmentDigitsFormat, value);
    }
    private string segmentAccuracy;
    public string SegmentAccuracy
    {
        get => segmentAccuracy;
        set => SetField(ref segmentAccuracy, value);
    }
    private TimeAccuracy segmentTimesAccuracy;
    public TimeAccuracy SegmentTimesAccuracy
    {
        get => segmentTimesAccuracy;
        set
        {
            if (SetField(ref segmentTimesAccuracy, value))
            {
                SegmentTimesFormatter.Accuracy = value;
            }
        }
    }
    public string SegmentLabelsFontString => SettingsHelper.FormatFont(SegmentLabelsFont);
    private Font segmentLabelsFont;
    public Font SegmentLabelsFont
    {
        get => segmentLabelsFont;
        set
        {
            if (SetField(ref segmentLabelsFont, value))
            {
                OnPropertyChanged(nameof(SegmentLabelsFontString));
            }
        }
    }
    public string SegmentTimesFontString => SettingsHelper.FormatFont(SegmentTimesFont);
    private Font segmentTimesFont;
    public Font SegmentTimesFont
    {
        get => segmentTimesFont;
        set
        {
            if (SetField(ref segmentTimesFont, value))
            {
                OnPropertyChanged(nameof(SegmentTimesFontString));
            }
        }
    }
    public string SplitNameFontString => SettingsHelper.FormatFont(SplitNameFont);
    private Font splitNameFont;
    public Font SplitNameFont
    {
        get => splitNameFont;
        set
        {
            if (SetField(ref splitNameFont, value))
            {
                OnPropertyChanged(nameof(SplitNameFontString));
            }
        }
    }

    private string comparison;
    public string Comparison
    {
        get => comparison;
        set
        {
            if (SetField(ref comparison, value))
            {
                OnPropertyChanged(nameof(ComparisonOptions));
            }
        }
    }
    private string comparison2;
    public string Comparison2
    {
        get => comparison2;
        set
        {
            if (SetField(ref comparison2, value))
            {
                OnPropertyChanged(nameof(Comparison2Options));
            }
        }
    }
    private bool hideComparison;
    public bool HideComparison
    {
        get => hideComparison;
        set
        {
            if (SetField(ref hideComparison, value))
            {
                OnPropertyChanged(nameof(IsComparison2Enabled));
            }
        }
    }
    private string timingMethod;
    public string TimingMethod
    {
        get => timingMethod;
        set => SetField(ref timingMethod, value);
    }

    private LayoutMode mode;
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

    public string[] ComparisonOptions => GetComparisonOptions(Comparison);
    private string[] GetComparisonOptions(string selected)
        => new[] { "Current Comparison" }
            .Concat(CurrentState?.Run.Comparisons.Where(x => x is not BestSplitTimesComparisonGenerator.ComparisonName and not NoneComparisonGenerator.ComparisonName) ?? Enumerable.Empty<string>())
            .Concat(string.IsNullOrEmpty(selected) ? [] : new[] { selected }).Distinct().ToArray();
    public string[] Comparison2Options => GetComparisonOptions(Comparison2);
    public static readonly string[] GradientOptions = TimerSettings.GradientOptions;
    public static readonly string[] DigitsFormatOptions = ["1", "00:01", "0:00:01", "00:00:01"];
    public static readonly string[] AccuracyOptions = ["", ".2", ".23", ".234"];
    public static readonly string[] TimingMethodOptions = ["Current Timing Method", "Real Time", "Game Time"];
    private GeneralTimeFormatter segmentTimesFormatter = new() { NullFormat = NullFormat.Dash, Accuracy = TimeAccuracy.Hundredths };
    public GeneralTimeFormatter SegmentTimesFormatter
    {
        get => segmentTimesFormatter;
        set
        {
            if (SetField(ref segmentTimesFormatter, value))
            {
                value.Accuracy = SegmentTimesAccuracy;
            }
        }
    }
    public bool IsTimerColorEnabled => OverrideTimerColors;
    public bool IsSplitNameEnabled => ShowSplitName;
    public bool IsIconSizeEnabled => DisplayIcon;
    public bool IsComparison2Enabled => !HideComparison;
    public bool IsColor1Visible => BackgroundGradient != DeltasGradientType.Plain && !GradientString.Contains("Delta");
    public bool IsColor2Visible => !GradientString.Contains("Delta");
    public Color BackgroundSwatch2Color
    {
        get => IsColor1Visible ? BackgroundColor2 : BackgroundColor;
        set { if (IsColor1Visible) { BackgroundColor2 = value; } else { BackgroundColor = value; } }
    }
    public float SizeValue
    {
        get => Mode == LayoutMode.Horizontal ? Width : Height;
        set { if (Mode == LayoutMode.Horizontal) { Width = value; } else { Height = value; } }
    }
    public float SizeMinimum => Mode == LayoutMode.Horizontal ? 50 : 20;
    public float SizeMaximum => Mode == LayoutMode.Horizontal ? 500 : 150;
    public string SizeLabel => T(Mode == LayoutMode.Horizontal ? "Width:" : "Height:");

    public DetailedTimerSettings()
    {

        Height = 75;
        Width = 200;
        SegmentTimerSizeRatio = 40;

        TimerShowGradient = true;
        OverrideTimerColors = false;
        SegmentTimerShowGradient = true;
        ShowSplitName = false;

        TimerColor = Color.FromArgb(170, 170, 170);
        SegmentTimerColor = Color.FromArgb(170, 170, 170);
        SegmentLabelsColor = Color.FromArgb(255, 255, 255);
        SegmentTimesColor = Color.FromArgb(255, 255, 255);
        SplitNameColor = Color.FromArgb(255, 255, 255);

        DigitsFormat = "1";
        Accuracy = ".23";
        SegmentDigitsFormat = "1";
        SegmentAccuracy = ".23";
        SegmentTimesAccuracy = TimeAccuracy.Hundredths;

        SegmentLabelsFont = new Font("Segoe UI", 13, FontStyle.Regular, GraphicsUnit.Pixel);
        SegmentTimesFont = new Font("Segoe UI", 13, FontStyle.Bold, GraphicsUnit.Pixel);
        SplitNameFont = new Font("Segoe UI", 15, FontStyle.Regular, GraphicsUnit.Pixel);

        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = DeltasGradientType.Plain;

        IconSize = 40f;
        DisplayIcon = false;

        DecimalsSize = 35f;
        SegmentTimerDecimalsSize = 35f;

        Comparison = "Current Comparison";
        Comparison2 = "Best Segments";
        HideComparison = false;
        TimingMethod = "Current Timing Method";

    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);

        Height = SettingsHelper.ParseFloat(element["Height"]);
        Width = SettingsHelper.ParseFloat(element["Width"]);
        SegmentTimerSizeRatio = SettingsHelper.ParseFloat(element["SegmentTimerSizeRatio"]);
        TimerShowGradient = SettingsHelper.ParseBool(element["TimerShowGradient"]);
        SegmentTimerShowGradient = SettingsHelper.ParseBool(element["SegmentTimerShowGradient"]);
        TimerColor = SettingsHelper.ParseColor(element["TimerColor"]);
        SegmentTimerColor = SettingsHelper.ParseColor(element["SegmentTimerColor"]);
        SegmentLabelsColor = SettingsHelper.ParseColor(element["SegmentLabelsColor"]);
        SegmentTimesColor = SettingsHelper.ParseColor(element["SegmentTimesColor"]);
        TimingMethod = SettingsHelper.ParseString(element["TimingMethod"], "Current Timing Method");
        DecimalsSize = SettingsHelper.ParseFloat(element["DecimalsSize"], 35f);
        SegmentTimerDecimalsSize = SettingsHelper.ParseFloat(element["SegmentTimerDecimalsSize"], 35f);
        DisplayIcon = SettingsHelper.ParseBool(element["DisplayIcon"], false);
        IconSize = SettingsHelper.ParseFloat(element["IconSize"], 40f);
        ShowSplitName = SettingsHelper.ParseBool(element["ShowSplitName"], false);
        SplitNameColor = SettingsHelper.ParseColor(element["SplitNameColor"], Color.FromArgb(255, 255, 255));
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"], Color.Transparent);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"], Color.Transparent);
        BackgroundGradient = SettingsHelper.ParseEnum(element["BackgroundGradient"], DeltasGradientType.Plain);
        Comparison = SettingsHelper.ParseString(element["Comparison"], "Current Comparison");
        Comparison2 = SettingsHelper.ParseString(element["Comparison2"], "Best Segments");
        HideComparison = SettingsHelper.ParseBool(element["HideComparison"], false);

        SegmentTimesAccuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["SegmentTimesAccuracy"]);
        SegmentTimesFormatter.Accuracy = SegmentTimesAccuracy;

        if (version >= new Version(1, 3))
        {
            OverrideTimerColors = SettingsHelper.ParseBool(element["OverrideTimerColors"]);
            SegmentLabelsFont = SettingsHelper.GetFontFromElement(element["SegmentLabelsFont"]);
            SegmentTimesFont = SettingsHelper.GetFontFromElement(element["SegmentTimesFont"]);
            SplitNameFont = SettingsHelper.GetFontFromElement(element["SplitNameFont"]);
        }
        else
        {
            OverrideTimerColors = !SettingsHelper.ParseBool(element["TimerUseSplitColors"]);
            SegmentLabelsFont = new Font("Segoe UI", 13, FontStyle.Regular, GraphicsUnit.Pixel);
            SegmentTimesFont = new Font("Segoe UI", 13, FontStyle.Bold, GraphicsUnit.Pixel);
            SplitNameFont = new Font("Segoe UI", 15, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        if (version >= new Version(1, 5))
        {
            timerFormat = element["TimerFormat"].InnerText;
            segmentTimerFormat = element["SegmentTimerFormat"].InnerText;
        }
        else
        {
            DigitsFormat = "1";
            SegmentDigitsFormat = "1";
            TimeAccuracy timerAccuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["TimerAccuracy"]);
            Accuracy = timerAccuracy switch
            {
                TimeAccuracy.Seconds => "",
                TimeAccuracy.Tenths => ".2",
                TimeAccuracy.Hundredths => ".23",
                TimeAccuracy.Milliseconds => ".234",
                _ => ".23",
            };
            TimeAccuracy segmentTimerAccuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["SegmentTimerAccuracy"]);
            SegmentAccuracy = segmentTimerAccuracy switch
            {
                TimeAccuracy.Seconds => "",
                TimeAccuracy.Tenths => ".2",
                TimeAccuracy.Hundredths => ".23",
                TimeAccuracy.Milliseconds => ".234",
                _ => ".23",
            };
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
        SettingsHelper.CreateSetting(document, parent, "Height", Height) ^
        SettingsHelper.CreateSetting(document, parent, "Width", Width) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentTimerSizeRatio", SegmentTimerSizeRatio) ^
        SettingsHelper.CreateSetting(document, parent, "TimerShowGradient", TimerShowGradient) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTimerColors", OverrideTimerColors) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentTimerShowGradient", SegmentTimerShowGradient) ^
        SettingsHelper.CreateSetting(document, parent, "TimerFormat", timerFormat) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentTimerFormat", segmentTimerFormat) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentTimesAccuracy", SegmentTimesAccuracy) ^
        SettingsHelper.CreateSetting(document, parent, "TimerColor", TimerColor) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentTimerColor", SegmentTimerColor) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentLabelsColor", SegmentLabelsColor) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentTimesColor", SegmentTimesColor) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentLabelsFont", SegmentLabelsFont) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentTimesFont", SegmentTimesFont) ^
        SettingsHelper.CreateSetting(document, parent, "SplitNameFont", SplitNameFont) ^
        SettingsHelper.CreateSetting(document, parent, "DisplayIcon", DisplayIcon) ^
        SettingsHelper.CreateSetting(document, parent, "IconSize", IconSize) ^
        SettingsHelper.CreateSetting(document, parent, "ShowSplitName", ShowSplitName) ^
        SettingsHelper.CreateSetting(document, parent, "SplitNameColor", SplitNameColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "Comparison", Comparison) ^
        SettingsHelper.CreateSetting(document, parent, "Comparison2", Comparison2) ^
        SettingsHelper.CreateSetting(document, parent, "HideComparison", HideComparison) ^
        SettingsHelper.CreateSetting(document, parent, "TimingMethod", TimingMethod) ^
        SettingsHelper.CreateSetting(document, parent, "DecimalsSize", DecimalsSize) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentTimerDecimalsSize", SegmentTimerDecimalsSize);
    }

}
