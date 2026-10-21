using LiveSplit.Localization;
using System;
using System.Drawing;
using System.Xml;

namespace LiveSplit.UI.Components;

public class BlankSpaceSettings : SettingsBase
{
    private static string T(string source) => UiLocalizer.Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());

    /// <summary>Items of the background gradient drop down (<see cref="GradientString"/>).</summary>
    public static readonly string[] GradientOptions = [.. Enum.GetNames<GradientType>()];

    private float spaceHeight;
    private float spaceWidth;
    private LayoutMode mode;
    private Color backgroundColor;
    private Color backgroundColor2;
    private GradientType backgroundGradient;

    public float SpaceHeight
    {
        get => spaceHeight;
        set
        {
            if (SetField(ref spaceHeight, value))
            {
                OnPropertyChanged(nameof(SizeValue));
            }
        }
    }

    public float SpaceWidth
    {
        get => spaceWidth;
        set
        {
            if (SetField(ref spaceWidth, value))
            {
                OnPropertyChanged(nameof(SizeValue));
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
                OnPropertyChanged(nameof(SizeValue));
                OnPropertyChanged(nameof(SizeLabel));
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

    /// <summary>The width (horizontal layout) or height (vertical layout) slider.</summary>
    public float SizeValue
    {
        get => Mode == LayoutMode.Horizontal ? SpaceWidth : SpaceHeight;
        set
        {
            if (Mode == LayoutMode.Horizontal)
            {
                SpaceWidth = value;
            }
            else
            {
                SpaceHeight = value;
            }
        }
    }
    public float SizeMinimum => 1;
    public float SizeMaximum => 1000;
    public string SizeLabel => T(Mode == LayoutMode.Horizontal ? "Width:" : "Height:");

    public BlankSpaceSettings()
    {
        SpaceHeight = 100;
        SpaceWidth = 100;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = GradientType.Plain;
    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);

        SpaceHeight = SettingsHelper.ParseFloat(element["SpaceHeight"]);
        SpaceWidth = SettingsHelper.ParseFloat(element["SpaceWidth"]);
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"], Color.Transparent);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"], Color.Transparent);
        GradientString = SettingsHelper.ParseString(element["BackgroundGradient"], GradientType.Plain.ToString());
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
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.7") ^
        SettingsHelper.CreateSetting(document, parent, "SpaceHeight", SpaceHeight) ^
        SettingsHelper.CreateSetting(document, parent, "SpaceWidth", SpaceWidth) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient);
    }
}
