using System;
using System.Drawing;
using System.Xml;

namespace LiveSplit.UI.Components;

public class TitleSettings : SettingsBase
{
    /// <summary>Items of the background gradient drop down (<see cref="GradientString"/>).</summary>
    public static readonly string[] GradientOptions = [.. Enum.GetNames<GradientType>()];
    /// <summary>Items of the text alignment drop down, indexed by <see cref="AlignmentType"/>.</summary>
    public static readonly string[] TextAlignmentOptions = ["Automatic (Based on Icon)", "Left", "Center"];

    private bool showGameName;
    private bool showCategoryName;
    private bool showAttemptCount;
    private bool showFinishedRunsCount;
    private AlignmentType textAlignment;
    private bool singleLine;
    private bool displayGameIcon;
    private bool showRegion;
    private bool showPlatform;
    private bool showVariables;
    private Color titleColor;
    private bool overrideTitleColor;
    private bool overrideTitleFont;
    private Font titleFont;
    private Color backgroundColor;
    private Color backgroundColor2;
    private GradientType backgroundGradient;

    public bool ShowGameName { get => showGameName; set => SetField(ref showGameName, value); }
    public bool ShowCategoryName { get => showCategoryName; set => SetField(ref showCategoryName, value); }
    public bool ShowAttemptCount
    {
        get => showAttemptCount;
        set
        {
            if (SetField(ref showAttemptCount, value))
            {
                OnPropertyChanged(nameof(ShowCount));
            }
        }
    }

    public bool ShowFinishedRunsCount
    {
        get => showFinishedRunsCount;
        set
        {
            if (SetField(ref showFinishedRunsCount, value))
            {
                OnPropertyChanged(nameof(ShowCount));
            }
        }
    }

    public bool ShowCount => ShowAttemptCount || ShowFinishedRunsCount;

    public AlignmentType TextAlignment
    {
        get => textAlignment;
        set
        {
            if (SetField(ref textAlignment, value))
            {
                OnPropertyChanged(nameof(TextAlignmentIndex));
            }
        }
    }

    /// <summary>The text alignment as the index into <see cref="TextAlignmentOptions"/>.</summary>
    public int TextAlignmentIndex
    {
        get => (int)TextAlignment;
        set => TextAlignment = (AlignmentType)value;
    }

    public bool SingleLine { get => singleLine; set => SetField(ref singleLine, value); }
    public bool DisplayGameIcon { get => displayGameIcon; set => SetField(ref displayGameIcon, value); }

    public bool ShowRegion { get => showRegion; set => SetField(ref showRegion, value); }
    public bool ShowPlatform { get => showPlatform; set => SetField(ref showPlatform, value); }
    public bool ShowVariables { get => showVariables; set => SetField(ref showVariables, value); }

    public Color TitleColor { get => titleColor; set => SetField(ref titleColor, value); }
    public bool OverrideTitleColor
    {
        get => overrideTitleColor;
        set
        {
            if (SetField(ref overrideTitleColor, value))
            {
                OnPropertyChanged(nameof(IsTitleColorEnabled));
            }
        }
    }

    // Legacy font override — read from old configs, not written or exposed in UI
    public bool OverrideTitleFont { get => overrideTitleFont; set => SetField(ref overrideTitleFont, value); }
    public Font TitleFont { get => titleFont; set => SetField(ref titleFont, value); }

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

    /// <summary>The "Color:" row of the "Title Color" group is enabled only while the layout colour is overridden.</summary>
    public bool IsTitleColorEnabled => OverrideTitleColor;

    public TitleSettings()
    {
        ShowGameName = true;
        ShowCategoryName = true;
        ShowAttemptCount = true;
        ShowFinishedRunsCount = false;
        DisplayGameIcon = true;
        TitleColor = Color.FromArgb(255, 255, 255, 255);
        OverrideTitleColor = false;
        SingleLine = false;
        ShowRegion = false;
        ShowPlatform = false;
        ShowVariables = true;
        BackgroundColor = Color.FromArgb(255, 42, 42, 42);
        BackgroundColor2 = Color.FromArgb(255, 19, 19, 19);
        BackgroundGradient = GradientType.Vertical;
        TextAlignment = AlignmentType.Auto;
    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);
        DisplayGameIcon = SettingsHelper.ParseBool(element["DisplayGameIcon"], true);

        if (version >= new Version(1, 2))
        {
            TitleFont = SettingsHelper.GetFontFromElement(element["TitleFont"]);
            if (version >= new Version(1, 3))
            {
                OverrideTitleFont = SettingsHelper.ParseBool(element["OverrideTitleFont"]);
                TextAlignment = version >= new Version(1, 7, 3)
                    ? (AlignmentType)SettingsHelper.ParseInt(element["TextAlignment"], 0)
                    : DisplayGameIcon && SettingsHelper.ParseBool(element["CenterTitle"], false)
                        ? AlignmentType.Center
                        : AlignmentType.Auto;
            }
            else
            {
                OverrideTitleFont = !SettingsHelper.ParseBool(element["UseLayoutSettingsFont"]);
            }
        }

        ShowGameName = SettingsHelper.ParseBool(element["ShowGameName"], true);
        ShowCategoryName = SettingsHelper.ParseBool(element["ShowCategoryName"], true);
        ShowAttemptCount = SettingsHelper.ParseBool(element["ShowAttemptCount"]);
        TitleColor = SettingsHelper.ParseColor(element["TitleColor"], Color.FromArgb(255, 255, 255, 255));
        OverrideTitleColor = SettingsHelper.ParseBool(element["OverrideTitleColor"], false);
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"], Color.FromArgb(42, 42, 42, 255));
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"], Color.FromArgb(19, 19, 19, 255));
        GradientString = SettingsHelper.ParseString(element["BackgroundGradient"], GradientType.Vertical.ToString());
        ShowFinishedRunsCount = SettingsHelper.ParseBool(element["ShowFinishedRunsCount"], false);
        SingleLine = SettingsHelper.ParseBool(element["SingleLine"], false);
        ShowRegion = SettingsHelper.ParseBool(element["ShowRegion"], false);
        ShowPlatform = SettingsHelper.ParseBool(element["ShowPlatform"], false);
        ShowVariables = SettingsHelper.ParseBool(element["ShowVariables"], true);
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
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.7.3") ^
        SettingsHelper.CreateSetting(document, parent, "ShowGameName", ShowGameName) ^
        SettingsHelper.CreateSetting(document, parent, "ShowCategoryName", ShowCategoryName) ^
        SettingsHelper.CreateSetting(document, parent, "ShowAttemptCount", ShowAttemptCount) ^
        SettingsHelper.CreateSetting(document, parent, "ShowFinishedRunsCount", ShowFinishedRunsCount) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTitleColor", OverrideTitleColor) ^
        SettingsHelper.CreateSetting(document, parent, "SingleLine", SingleLine) ^
        SettingsHelper.CreateSetting(document, parent, "TitleColor", TitleColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "DisplayGameIcon", DisplayGameIcon) ^
        SettingsHelper.CreateSetting(document, parent, "ShowRegion", ShowRegion) ^
        SettingsHelper.CreateSetting(document, parent, "ShowPlatform", ShowPlatform) ^
        SettingsHelper.CreateSetting(document, parent, "ShowVariables", ShowVariables) ^
        SettingsHelper.CreateSetting(document, parent, "TextAlignment", (int)TextAlignment);
    }
}
