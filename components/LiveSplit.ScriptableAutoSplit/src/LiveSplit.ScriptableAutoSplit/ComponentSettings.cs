using LiveSplit.ASL;
using LiveSplit.Hosting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
namespace LiveSplit.UI.Components;

public class ComponentSettings : SettingsBase
{
    private string scriptPath = "";
    public string ScriptPath { get => scriptPath; set => SetField(ref scriptPath, PathCompat.Resolve(value)); }
    private string gameVersion;
    public string GameVersion => gameVersion;
    public string GameVersionLabel => string.IsNullOrEmpty(gameVersion) ? "" : "Game Version: " + gameVersion;
    private bool ignoreNextPath;
    private readonly Dictionary<string, bool> basicState = [];
    private readonly Dictionary<string, bool> customState = [];
    public ObservableCollection<ASLSettingNode> BasicSettings { get; } = [];
    public ObservableCollection<ASLSettingNode> CustomSettings { get; } = [];
    public bool HasCustomSettings => CustomSettings.Count != 0;
    public ComponentSettings() { }
    public ComponentSettings(string path) { ScriptPath = path; ignoreNextPath = true; }
    public void SetGameVersion(string version)
    {
        if (SetField(ref gameVersion, version, nameof(GameVersion))) OnPropertyChanged(nameof(GameVersionLabel));
    }
    public void SetASLSettings(ASLSettings settings) => Initialize(settings, true);
    public void ResetASLSettings() => Initialize(new ASLSettings(), false);
    private void Initialize(ASLSettings settings, bool loaded)
    {
        if (string.IsNullOrWhiteSpace(ScriptPath)) { basicState.Clear(); customState.Clear(); }
        BasicSettings.Clear(); CustomSettings.Clear();
        foreach (string key in new[] { "start", "split", "reset" })
        {
            bool exists = settings.BasicSettings.TryGetValue(key, out var setting);
            setting ??= new ASLSetting(key, false, char.ToUpper(key[0]) + key.Substring(1), null);
            if (basicState.TryGetValue(key, out bool saved)) setting.Value = saved;
            BasicSettings.Add(new ASLSettingNode(setting, null, exists, value => basicState[key] = value));
        }
        var nodes = new Dictionary<string, ASLSettingNode>();
        var values = new Dictionary<string, bool>();
        foreach (var setting in settings.OrderedSettings)
        {
            if (customState.TryGetValue(setting.Id, out bool saved)) setting.Value = saved;
            ASLSettingNode parent = setting.Parent == null ? null : nodes[setting.Parent];
            var node = new ASLSettingNode(setting, parent, true, value => customState[setting.Id] = value);
            nodes.Add(setting.Id, node);
            if (parent == null) CustomSettings.Add(node); else parent.Children.Add(node);
            values[setting.Id] = setting.Value;
        }
        if (loaded) { customState.Clear(); foreach (var pair in values) customState.Add(pair.Key, pair.Value); }
        OnPropertyChanged(nameof(HasCustomSettings));
    }
    public void SetAll(bool? value, ASLSettingNode branch = null)
    {
        foreach (var node in branch == null ? CustomSettings : new[] { branch }.AsEnumerable())
            SetBranch(node, value);
    }
    private static void SetBranch(ASLSettingNode node, bool? value)
    {
        node.Value = value ?? node.DefaultValue;
        foreach (var child in node.Children) SetBranch(child, value);
    }
    public XmlNode GetSettings(XmlDocument document)
    {
        var node = document.CreateElement("Settings");
        node.AppendChild(SettingsHelper.ToElement(document, "Version", "1.5"));
        node.AppendChild(SettingsHelper.ToElement(document, "ScriptPath", ScriptPath));
        foreach (string key in new[] { "Start", "Reset", "Split" })
            if (basicState.TryGetValue(key.ToLowerInvariant(), out bool value)) node.AppendChild(SettingsHelper.ToElement(document, key, value));
        var custom = document.CreateElement("CustomSettings");
        foreach (var pair in customState)
        {
            var item = SettingsHelper.ToElement(document, "Setting", pair.Value);
            item.SetAttribute("id", pair.Key); item.SetAttribute("type", "bool"); custom.AppendChild(item);
        }
        node.AppendChild(custom); return node;
    }
    public void SetSettings(XmlNode settings)
    {
        var node = (XmlElement)settings;
        if (!ignoreNextPath) ScriptPath = SettingsHelper.ParseString(node["ScriptPath"], "");
        ignoreNextPath = false;
        foreach (string key in new[] { "Start", "Reset", "Split" })
            if (node[key] != null) basicState[key.ToLowerInvariant()] = SettingsHelper.ParseBool(node[key]);
        if (node["CustomSettings"] != null)
            foreach (XmlElement item in node["CustomSettings"].ChildNodes)
                if (item.Name == "Setting" && item.GetAttribute("type") == "bool") customState[item.GetAttribute("id")] = SettingsHelper.ParseBool(item);
        foreach (var basic in BasicSettings) if (basicState.TryGetValue(basic.Id, out bool value)) basic.Value = value;
        Restore(CustomSettings);
    }
    private void Restore(IEnumerable<ASLSettingNode> nodes)
    {
        foreach (var node in nodes) { if (customState.TryGetValue(node.Id, out bool value)) node.Value = value; Restore(node.Children); }
    }
}

public sealed class ASLSettingNode(ASLSetting setting, ASLSettingNode parent, bool available, Action<bool> changed) : SettingsBase
{
    public string Id => setting.Id;
    public string Label => string.IsNullOrEmpty(setting.Label) ? char.ToUpper(Id[0]) + Id.Substring(1) : setting.Label;
    public string Tooltip => setting.ToolTip;
    public bool DefaultValue => setting.DefaultValue;
    public ObservableCollection<ASLSettingNode> Children { get; } = [];
    public bool IsEnabled => available && (parent == null || parent.IsEnabled && parent.Value);
    public bool Value
    {
        get => setting.Value;
        set { if (setting.Value == value) return; setting.Value = value; changed(value); OnPropertyChanged(); NotifyChildren(); }
    }
    private bool expanded = true;
    public bool IsExpanded { get => expanded; set => SetField(ref expanded, value); }
    private void NotifyChildren() { foreach (var child in Children) { child.OnPropertyChanged(nameof(IsEnabled)); child.NotifyChildren(); } }
}
