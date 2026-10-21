using LiveSplit.Localization;
using LiveSplit.Model;
using System;
using System.Drawing;
using System.Xml;

namespace LiveSplit.UI.Components;

public class TextComponentSettings : SettingsBase
{
    private static string T(string source) => UiLocalizer.Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());

    /// <summary>Items of the background gradient drop down (<see cref="GradientString"/>).</summary>
    public static readonly string[] GradientOptions = [.. Enum.GetNames<GradientType>()];

    private Color textColor;
    private bool overrideTextColor;
    private Color timeColor;
    private bool overrideTimeColor;
    private Color backgroundColor;
    private Color backgroundColor2;
    private GradientType backgroundGradient;
    private string text1;
    private string text2;
    private bool overrideFont1;
    private Font font1;
    private bool overrideFont2;
    private Font font2;
    private LayoutMode mode;
    private bool display2Rows;
    private bool customVariable;

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

    public string Text1 { get => text1; set => SetField(ref text1, value); }
    public string Text2 { get => text2; set => SetField(ref text2, value); }

    // Legacy font overrides — read from old configs, not written or exposed in UI
    public bool OverrideFont1 { get => overrideFont1; set => SetField(ref overrideFont1, value); }
    public Font Font1 { get => font1; set => SetField(ref font1, value); }
    public bool OverrideFont2 { get => overrideFont2; set => SetField(ref overrideFont2, value); }
    public Font Font2 { get => font2; set => SetField(ref font2, value); }

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

    public bool CustomVariable
    {
        get => customVariable;
        set
        {
            if (SetField(ref customVariable, value))
            {
                OnPropertyChanged(nameof(Text2Label));
            }
        }
    }

    public LiveSplitState CurrentState { get; set; }

    // Computed, UI facing state that used to live in the WinForms event handlers.

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

    /// <summary>Label of the right text box.</summary>
    public string Text2Label => T(CustomVariable ? "Custom Variable Name:" : "Text:");

    public TextComponentSettings()
    {
        TextColor = Color.FromArgb(255, 255, 255);
        OverrideTextColor = false;
        TimeColor = Color.FromArgb(255, 255, 255);
        OverrideTimeColor = false;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = GradientType.Plain;
        Text1 = "Text";
        Text2 = "";
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
        Text1 = SettingsHelper.ParseString(element["Text1"]);
        Text2 = SettingsHelper.ParseString(element["Text2"]);
        Font1 = SettingsHelper.GetFontFromElement(element["Font1"]);
        Font2 = SettingsHelper.GetFontFromElement(element["Font2"]);
        OverrideFont1 = SettingsHelper.ParseBool(element["OverrideFont1"]);
        OverrideFont2 = SettingsHelper.ParseBool(element["OverrideFont2"]);
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"], false);
        CustomVariable = SettingsHelper.ParseBool(element["CustomVariable"], false);
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
        SettingsHelper.CreateSetting(document, parent, "TextColor", TextColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
        SettingsHelper.CreateSetting(document, parent, "TimeColor", TimeColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTimeColor", OverrideTimeColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "Text1", Text1) ^
        SettingsHelper.CreateSetting(document, parent, "Text2", Text2) ^
        SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
        SettingsHelper.CreateSetting(document, parent, "CustomVariable", CustomVariable);
    }
}
