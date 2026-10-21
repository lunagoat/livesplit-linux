using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.TimeFormatters;
using System;
using System.Drawing;
using System.Linq;
using System.Xml;

namespace LiveSplit.UI.Components;

public class PreviousSegmentSettings : SettingsBase
{
    private Color textColor;
    private bool overrideTextColor;
    private Color backgroundColor;
    private Color backgroundColor2;
    private GradientType backgroundGradient;
    private TimeAccuracy deltaAccuracy;
    private bool dropDecimals;
    private bool display2Rows;
    private bool showPossibleTimeSave;
    private TimeAccuracy timeSaveAccuracy;
    private string comparison;
    private LiveSplitState currentState;
    private LayoutMode mode;

    public Color TextColor { get => textColor; set => SetField(ref textColor, value); }

    public bool OverrideTextColor
    {
        get => overrideTextColor;
        set
        {
            if (SetField(ref overrideTextColor, value))
            {
                OnPropertyChanged(nameof(IsTextColorEnabled));
            }
        }
    }

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

    public GradientType BackgroundGradient
    {
        get => backgroundGradient;
        set
        {
            if (SetField(ref backgroundGradient, value))
            {
                OnPropertyChanged(nameof(GradientString));
                OnPropertyChanged(nameof(IsColor1Visible));
                OnPropertyChanged(nameof(BackgroundSwatch2Color));
            }
        }
    }

    public TimeAccuracy DeltaAccuracy { get => deltaAccuracy; set => SetField(ref deltaAccuracy, value); }

    public bool DropDecimals { get => dropDecimals; set => SetField(ref dropDecimals, value); }

    public bool Display2Rows
    {
        get => display2Rows;
        set
        {
            if (SetField(ref display2Rows, value))
            {
                OnPropertyChanged(nameof(Display2RowsShown));
            }
        }
    }

    public bool ShowPossibleTimeSave
    {
        get => showPossibleTimeSave;
        set
        {
            if (SetField(ref showPossibleTimeSave, value))
            {
                OnPropertyChanged(nameof(IsTimeSaveAccuracyEnabled));
            }
        }
    }

    public TimeAccuracy TimeSaveAccuracy { get => timeSaveAccuracy; set => SetField(ref timeSaveAccuracy, value); }

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

    public LiveSplitState CurrentState
    {
        get => currentState;
        set
        {
            if (SetField(ref currentState, value))
            {
                OnPropertyChanged(nameof(ComparisonOptions));
            }
        }
    }

    public LayoutMode Mode
    {
        get => mode;
        set
        {
            if (SetField(ref mode, value))
            {
                OnPropertyChanged(nameof(IsDisplay2RowsEnabled));
                OnPropertyChanged(nameof(Display2RowsShown));
            }
        }
    }

    public static readonly string[] GradientOptions = ["Plain", "Vertical", "Horizontal"];

    public string GradientString
    {
        get => BackgroundGradient.ToString();
        set => BackgroundGradient = (GradientType)Enum.Parse(typeof(GradientType), value);
    }

    public bool IsColor1Visible => BackgroundGradient != GradientType.Plain;

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

    public bool IsTextColorEnabled => OverrideTextColor;
    public bool IsDisplay2RowsEnabled => Mode != LayoutMode.Horizontal;
    public bool Display2RowsShown
    {
        get => !IsDisplay2RowsEnabled || Display2Rows;
        set => Display2Rows = value;
    }

    // Re-evaluated when the settings view opens; keep unknown persisted comparisons selectable.
    public string[] ComparisonOptions
    {
        get
        {
            var options = new[] { "Current Comparison" }
                .Concat(CurrentState?.Run.Comparisons.Where(x => x is not BestSplitTimesComparisonGenerator.ComparisonName and not NoneComparisonGenerator.ComparisonName) ?? Enumerable.Empty<string>())
                .ToList();
            if (!options.Contains(Comparison))
            {
                options.Add(Comparison);
            }

            return options.ToArray();
        }
    }

    public bool IsTimeSaveAccuracyEnabled => ShowPossibleTimeSave;

    public PreviousSegmentSettings()
    {
        TextColor = Color.FromArgb(255, 255, 255);
        OverrideTextColor = false;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = GradientType.Plain;
        DeltaAccuracy = TimeAccuracy.Tenths;
        TimeSaveAccuracy = TimeAccuracy.Tenths;
        DropDecimals = true;
        Comparison = "Current Comparison";
        Display2Rows = false;
        ShowPossibleTimeSave = false;
    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        TextColor = SettingsHelper.ParseColor(element["TextColor"]);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"]);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"]);
        GradientString = SettingsHelper.ParseString(element["BackgroundGradient"]);
        DeltaAccuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["DeltaAccuracy"]);
        DropDecimals = SettingsHelper.ParseBool(element["DropDecimals"]);
        Comparison = SettingsHelper.ParseString(element["Comparison"]);
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"], false);
        ShowPossibleTimeSave = SettingsHelper.ParseBool(element["ShowPossibleTimeSave"], false);
        TimeSaveAccuracy = SettingsHelper.ParseEnum(element["TimeSaveAccuracy"], TimeAccuracy.Tenths);
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
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.6") ^
        SettingsHelper.CreateSetting(document, parent, "TextColor", TextColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "DeltaAccuracy", DeltaAccuracy) ^
        SettingsHelper.CreateSetting(document, parent, "DropDecimals", DropDecimals) ^
        SettingsHelper.CreateSetting(document, parent, "Comparison", Comparison) ^
        SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
        SettingsHelper.CreateSetting(document, parent, "ShowPossibleTimeSave", ShowPossibleTimeSave) ^
        SettingsHelper.CreateSetting(document, parent, "TimeSaveAccuracy", TimeSaveAccuracy);
    }
}
