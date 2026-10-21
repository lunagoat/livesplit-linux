using LiveSplit.Model;
using System;
using System.Drawing;
using System.Xml;

namespace LiveSplit.UI.Components;

public class WorldRecordSettings : SettingsBase
{
    public static readonly string[] GradientOptions = ["Plain", "Vertical", "Horizontal"];

    private LayoutMode mode;
    private bool display2Rows;
    private Color textColor;
    private bool overrideTextColor;
    private Color timeColor;
    private bool overrideTimeColor;
    private Color backgroundColor;
    private Color backgroundColor2;
    private GradientType backgroundGradient;

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

    public Color TimeColor { get => timeColor; set => SetField(ref timeColor, value); }
    public bool OverrideTimeColor
    {
        get => overrideTimeColor;
        set
        {
            if (SetField(ref overrideTimeColor, value))
            {
                OnPropertyChanged(nameof(IsTimeColorEnabled));
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

    public string GradientString
    {
        get => BackgroundGradient.ToString();
        set => BackgroundGradient = (GradientType)Enum.Parse(typeof(GradientType), value);
    }

    private LiveSplitState currentState;
    public LiveSplitState CurrentState { get => currentState; set => SetField(ref currentState, value); }

    private bool centeredText;
    public bool CenteredText
    {
        get => centeredText;
        set
        {
            if (SetField(ref centeredText, value))
            {
                OnPropertyChanged(nameof(CenteredTextShown));
            }
        }
    }

    private bool filterVariables;
    public bool FilterVariables { get => filterVariables; set => SetField(ref filterVariables, value); }

    private bool filterPlatform;
    public bool FilterPlatform { get => filterPlatform; set => SetField(ref filterPlatform, value); }

    private bool filterRegion;
    public bool FilterRegion { get => filterRegion; set => SetField(ref filterRegion, value); }

    private bool filterSubcategories;
    public bool FilterSubcategories { get => filterSubcategories; set => SetField(ref filterSubcategories, value); }

    private string timingMethod;
    public string TimingMethod { get => timingMethod; set => SetField(ref timingMethod, value); }

    private WorldRecordPrecisionType wRPrecision;
    public WorldRecordPrecisionType WRPrecision { get => wRPrecision; set => SetField(ref wRPrecision, value); }

    public static readonly string[] TimingMethodOptions = ["Default for Leaderboard", "Real Time", "Real Time Without Loads", "Game Time"];

    public LayoutMode Mode
    {
        get => mode;
        set
        {
            if (SetField(ref mode, value))
            {
                OnPropertyChanged(nameof(IsDisplay2RowsEnabled));
                OnPropertyChanged(nameof(Display2RowsShown));
                OnPropertyChanged(nameof(IsCenteredTextEnabled));
                OnPropertyChanged(nameof(CenteredTextShown));
            }
        }
    }

    public bool Display2Rows
    {
        get => display2Rows;
        set
        {
            if (SetField(ref display2Rows, value))
            {
                OnPropertyChanged(nameof(Display2RowsShown));
                OnPropertyChanged(nameof(IsCenteredTextEnabled));
                OnPropertyChanged(nameof(CenteredTextShown));
            }
        }
    }

    /// <summary>The first background swatch is hidden for a plain background.</summary>
    public bool IsColor1Visible => BackgroundGradient != GradientType.Plain;

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

    public bool IsTextColorEnabled => OverrideTextColor;
    public bool IsTimeColorEnabled => OverrideTimeColor;

    /// <summary>"Display 2 Rows" is only configurable in vertical layouts; horizontal layouts always use two rows.</summary>
    public bool IsDisplay2RowsEnabled => Mode != LayoutMode.Horizontal;

    /// <summary>The state the "Display 2 Rows" checkbox displays (always checked in horizontal layouts).</summary>
    public bool Display2RowsShown => Mode == LayoutMode.Horizontal || Display2Rows;

    public bool IsCenteredTextEnabled => !Display2RowsShown;
    public bool CenteredTextShown => IsCenteredTextEnabled && CenteredText;

    public WorldRecordSettings()
    {
        TextColor = Color.FromArgb(255, 255, 255);
        OverrideTextColor = false;
        TimeColor = Color.FromArgb(255, 255, 255);
        OverrideTimeColor = false;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = GradientType.Plain;
        Display2Rows = false;
        CenteredText = true;
        FilterVariables = false;
        FilterPlatform = false;
        FilterRegion = false;
        FilterSubcategories = true;
        TimingMethod = "Default for Leaderboard";
        WRPrecision = WorldRecordPrecisionType.FromLeaderboard;

    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        TextColor = SettingsHelper.ParseColor(element["TextColor"]);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        TimeColor = SettingsHelper.ParseColor(element["TimeColor"]);
        OverrideTimeColor = SettingsHelper.ParseBool(element["OverrideTimeColor"]);
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"]);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"]);
        GradientString = SettingsHelper.ParseString(element["BackgroundGradient"]);
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"]);
        CenteredText = SettingsHelper.ParseBool(element["CenteredText"]);
        FilterRegion = SettingsHelper.ParseBool(element["FilterRegion"]);
        FilterPlatform = SettingsHelper.ParseBool(element["FilterPlatform"]);
        FilterVariables = SettingsHelper.ParseBool(element["FilterVariables"]);
        FilterSubcategories = SettingsHelper.ParseBool(element["FilterSubcategories"], true);
        TimingMethod = SettingsHelper.ParseString(element["TimingMethod"], "Default for Leaderboard");
        WRPrecision = SettingsHelper.ParseEnum(element["PrecisionType"], WorldRecordPrecisionType.FromLeaderboard);
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
        SettingsHelper.CreateSetting(document, parent, "TimeColor", TimeColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTimeColor", OverrideTimeColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
        SettingsHelper.CreateSetting(document, parent, "CenteredText", CenteredText) ^
        SettingsHelper.CreateSetting(document, parent, "FilterRegion", FilterRegion) ^
        SettingsHelper.CreateSetting(document, parent, "FilterPlatform", FilterPlatform) ^
        SettingsHelper.CreateSetting(document, parent, "FilterVariables", FilterVariables) ^
        SettingsHelper.CreateSetting(document, parent, "FilterSubcategories", FilterSubcategories) ^
        SettingsHelper.CreateSetting(document, parent, "TimingMethod", TimingMethod) ^
        SettingsHelper.CreateSetting(document, parent, "PrecisionType", WRPrecision);
    }

}
