using System.Xml;

namespace LiveSplit.UI.Components;

public class ManualGameTimeSettings : SettingsBase
{
    private bool useSegmentTimes;
    public bool UseSegmentTimes { get => useSegmentTimes; set { if (SetField(ref useSegmentTimes, value)) { OnPropertyChanged(nameof(UseSplitTimes)); } } }

    public bool UseSplitTimes { get => !UseSegmentTimes; set => UseSegmentTimes = !value; }

    public ManualGameTimeSettings()
    {

        UseSegmentTimes = true;

    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        UseSegmentTimes = SettingsHelper.ParseBool(element["UseSegmentTimes"]);
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
        SettingsHelper.CreateSetting(document, parent, "UseSegmentTimes", UseSegmentTimes);
    }
}
