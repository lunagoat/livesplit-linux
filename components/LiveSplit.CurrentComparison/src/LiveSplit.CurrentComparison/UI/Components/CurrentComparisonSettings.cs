using LiveSplit.Model;
using System;
using System.Drawing;
using System.Xml;

namespace LiveSplit.UI.Components;

public class CurrentComparisonSettings : SettingsBase
{
    private Color textColor;
    private bool overrideTextColor;
    private Color timeColor;
    private bool overrideTimeColor;
    private Color backgroundColor;
    private Color backgroundColor2;
    private GradientType backgroundGradient;
    private bool display2Rows;
    private LayoutMode mode;
    private LiveSplitState currentState;

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

    public LiveSplitState CurrentState { get => currentState; set => SetField(ref currentState, value); }

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

    public bool IsTimeColorEnabled => OverrideTimeColor;

    public CurrentComparisonSettings()
    {
        TextColor = Color.FromArgb(255, 255, 255);
        OverrideTextColor = false;
        TimeColor = Color.FromArgb(255, 255, 255);
        OverrideTimeColor = false;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = GradientType.Plain;
        Display2Rows = false;
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
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"], false);
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
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.4") ^
        SettingsHelper.CreateSetting(document, parent, "TextColor", TextColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
        SettingsHelper.CreateSetting(document, parent, "TimeColor", TimeColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTimeColor", OverrideTimeColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows);
    }
}
