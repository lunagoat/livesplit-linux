using LiveSplit.Hosting;
using LiveSplit.Localization;
using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.TimeFormatters;
using LiveSplit.UI;
using System;
using System.IO;
using System.Xml;

namespace LiveSplit.Video;

public class VideoSettings : SettingsBase
{
    private string videoPath = "";
    private TimeSpan offset;
    private float height = 200, width = 200;
    private LayoutMode mode;
    private readonly ITimeFormatter timeFormatter = new ShortTimeFormatter();
    private static string T(string source) => UiLocalizer.Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());
    public string MRL => string.IsNullOrEmpty(VideoPath) ? "" : new Uri(Path.GetFullPath(PathCompat.Resolve(VideoPath))).AbsoluteUri;
    public string VideoPath
    {
        get => videoPath;
        set { if (SetField(ref videoPath, value)) OnPropertyChanged(nameof(MRL)); }
    }
    public TimeSpan Offset
    {
        get => offset;
        set { if (SetField(ref offset, value)) OnPropertyChanged(nameof(OffsetString)); }
    }
    public float Height
    {
        get => height;
        set { if (SetField(ref height, value)) OnPropertyChanged(nameof(SizeValue)); }
    }
    public float Width
    {
        get => width;
        set { if (SetField(ref width, value)) OnPropertyChanged(nameof(SizeValue)); }
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
                OnPropertyChanged(nameof(SizeMaximum));
            }
        }
    }
    public float SizeValue { get => Mode == LayoutMode.Horizontal ? Width : Height; set { if (Mode == LayoutMode.Horizontal) Width = value; else Height = value; } }
    public int SizeMinimum => 100;
    public int SizeMaximum => Mode == LayoutMode.Horizontal ? 400 : 300;
    public string SizeLabel => T(Mode == LayoutMode.Horizontal ? "Width:" : "Height:");
    public string OffsetString
    {
        get => timeFormatter.Format(Offset);
        set
        {
            if (string.IsNullOrWhiteSpace(value) || System.Text.RegularExpressions.Regex.IsMatch(value, "[^0-9:.,-]")) return;
            try { Offset = TimeSpanParser.Parse(value); }
            catch (Exception ex) { Log.Error(ex); }
        }
    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        VideoPath = SettingsHelper.ParseString(element["VideoPath"]);
        OffsetString = SettingsHelper.ParseString(element["Offset"]);
        Height = SettingsHelper.ParseFloat(element["Height"]);
        Width = SettingsHelper.ParseFloat(element["Width"]);
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
        SettingsHelper.CreateSetting(document, parent, "VideoPath", VideoPath) ^
        SettingsHelper.CreateSetting(document, parent, "Offset", OffsetString) ^
        SettingsHelper.CreateSetting(document, parent, "Height", Height) ^
        SettingsHelper.CreateSetting(document, parent, "Width", Width);
    }

}
