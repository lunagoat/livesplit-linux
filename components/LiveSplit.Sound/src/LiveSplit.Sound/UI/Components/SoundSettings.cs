using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace LiveSplit.UI.Components;

public class SoundSettings : SettingsBase
{
    private int outputDevice;
    private int generalVolume = 100;
    private bool isClearAddDragDrop = true;
    private Dictionary<EventType, SoundData> soundDataDictionary = [];
    public int OutputDevice { get => outputDevice; set => SetField(ref outputDevice, value); }
    public int GeneralVolume { get => generalVolume; set => SetField(ref generalVolume, value); }
    public bool IsOutputDeviceEnabled => false;
    public Dictionary<EventType, SoundData> SoundDataDictionary
    {
        get => soundDataDictionary;
        set
        {
            if (SetField(ref soundDataDictionary, value)) RebuildRows();
        }
    }
    public bool IsClearAddDragDrop
    {
        get => isClearAddDragDrop;
        set
        {
            if (SetField(ref isClearAddDragDrop, value))
                foreach (SoundFileSettings row in FileSettings.Values) row.IsClearAddDragDrop = value;
        }
    }
    public IReadOnlyDictionary<EventType, SoundFileSettings> FileSettings { get; private set; } = new Dictionary<EventType, SoundFileSettings>();
    public IReadOnlyDictionary<EventType, SoundVolumeSettings> VolumeSettings { get; private set; } = new Dictionary<EventType, SoundVolumeSettings>();
    private void RebuildRows()
    {
        FileSettings = SoundDataDictionary.ToDictionary(pair => pair.Key, pair => new SoundFileSettings(pair.Key, pair.Value) { IsClearAddDragDrop = IsClearAddDragDrop });
        VolumeSettings = SoundDataDictionary.ToDictionary(pair => pair.Key, pair => new SoundVolumeSettings(pair.Key, pair.Value));
        OnPropertyChanged(nameof(FileSettings));
        OnPropertyChanged(nameof(VolumeSettings));
    }
    public SoundSettings()
    {
        foreach (EventType type in Enum.GetValues<EventType>())
        {
            SoundDataDictionary.Add(type, new SoundData([], 100));
        }
        RebuildRows();
    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;

        foreach (EventType type in Enum.GetValues(typeof(EventType)))
        {
            SoundDataDictionary[type].FilePaths = ParsePathListSetting(element, type.ToString());
            SoundDataDictionary[type].Volume = SettingsHelper.ParseInt(element[$"{type}Volume"]);
        }

        OutputDevice = SettingsHelper.ParseInt(element[nameof(OutputDevice)]);
        GeneralVolume = SettingsHelper.ParseInt(element[nameof(GeneralVolume)], 100);
        IsClearAddDragDrop = SettingsHelper.ParseBool(element[nameof(IsClearAddDragDrop)], true);
    }

    private IList<string> ParsePathListSetting(XmlElement element, string settingName)
    {
        XmlElement settingElement = element[settingName];
        if (settingElement == null)
        {
            return [];
        }

        IList<string> paths = [];

        XmlNodeList pathTexts = settingElement.SelectNodes("./path/text()");
        foreach (XmlCharacterData pathData in pathTexts)
        {
            if (pathData is XmlText pathText)
            {
                paths.Add(pathData.Data);
            }
        }

        // Support old, single-path setting format
        if (paths.Count == 0)
        {
            if (settingElement.SelectSingleNode("./text()") is XmlText oldSettingValue)
            {
                paths.Add(oldSettingValue.Data);
            }
        }

        return paths;
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
        int hash = SettingsHelper.CreateSetting(document, parent, "Version", "1.6")
            ^ SettingsHelper.CreateSetting(document, parent, nameof(OutputDevice), OutputDevice)
            ^ SettingsHelper.CreateSetting(document, parent, nameof(GeneralVolume), GeneralVolume)
            ^ SettingsHelper.CreateSetting(document, parent, nameof(IsClearAddDragDrop), IsClearAddDragDrop);

        foreach (EventType type in Enum.GetValues(typeof(EventType)))
        {
            hash ^= CreatePathListSetting(document, parent, type.ToString(), SoundDataDictionary[type].FilePaths)
                ^ SettingsHelper.CreateSetting(document, parent, $"{type}Volume", SoundDataDictionary[type].Volume);
        }

        return hash;
    }

    private static int CreatePathListSetting(XmlDocument document, XmlElement parent, string name, IList<string> paths)
    {
        if (document != null)
        {
            XmlElement pathListElement = document.CreateElement(name);
            foreach (string path in paths)
            {
                SettingsHelper.CreateSetting(document, pathListElement, "path", path);
            }

            parent.AppendChild(pathListElement);
        }

        return paths.Aggregate(0, (hash, next) => hash ^ next.GetHashCode());
    }

}
