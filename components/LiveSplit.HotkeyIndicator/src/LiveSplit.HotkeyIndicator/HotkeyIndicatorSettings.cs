using System;
using System.Drawing;
using System.Xml;

namespace LiveSplit.UI.Components;

public class HotkeyIndicatorSettings : SettingsBase
{
    private float indicatorHeight;
    public float IndicatorHeight { get => indicatorHeight; set { if (SetField(ref indicatorHeight, value)) { OnPropertyChanged(nameof(SizeValue)); } } }
    private float indicatorWidth;
    public float IndicatorWidth { get => indicatorWidth; set { if (SetField(ref indicatorWidth, value)) { OnPropertyChanged(nameof(SizeValue)); } } }
    private LayoutMode mode;
    public LayoutMode Mode { get => mode; set { if (SetField(ref mode, value)) { OnPropertyChanged(nameof(SizeValue)); OnPropertyChanged(nameof(SizeLabel)); } } }
    private Color hotkeysOnColor;
    public Color HotkeysOnColor { get => hotkeysOnColor; set => SetField(ref hotkeysOnColor, value); }
    private Color hotkeysOffColor;
    public Color HotkeysOffColor { get => hotkeysOffColor; set => SetField(ref hotkeysOffColor, value); }
    private GradientType backgroundGradient;
    public GradientType BackgroundGradient { get => backgroundGradient; set { if (SetField(ref backgroundGradient, value)) { OnPropertyChanged(nameof(GradientString)); } } }
    public string GradientString
    {
        get => BackgroundGradient.ToString();
        set => BackgroundGradient = (GradientType)Enum.Parse(typeof(GradientType), value);
    }

    public float SizeValue { get => Mode == LayoutMode.Horizontal ? IndicatorWidth : IndicatorHeight; set { if (Mode == LayoutMode.Horizontal) IndicatorWidth = value; else IndicatorHeight = value; } }
    public int SizeMinimum => 1;
    public int SizeMaximum => 50;
    public string SizeLabel => Mode == LayoutMode.Horizontal ? "Width:" : "Height:";

    public HotkeyIndicatorSettings()
    {

        IndicatorHeight = 3f;
        IndicatorWidth = 3f;

        HotkeysOnColor = Color.FromArgb(41, 204, 84);
        HotkeysOffColor = Color.FromArgb(204, 55, 41);

    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        IndicatorHeight = SettingsHelper.ParseFloat(element["IndicatorHeight"], 3f);
        IndicatorWidth = SettingsHelper.ParseFloat(element["IndicatorWidth"], 3f);
        HotkeysOnColor = SettingsHelper.ParseColor(element["HotkeysOnColor"]);
        HotkeysOffColor = SettingsHelper.ParseColor(element["HotkeysOffColor"]);
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
        return SettingsHelper.CreateSetting(document, parent, "HotkeysOnColor", HotkeysOnColor) ^
        SettingsHelper.CreateSetting(document, parent, "HotkeysOffColor", HotkeysOffColor) ^
        SettingsHelper.CreateSetting(document, parent, "IndicatorHeight", IndicatorHeight) ^
        SettingsHelper.CreateSetting(document, parent, "IndicatorWidth", IndicatorWidth);
    }

}
