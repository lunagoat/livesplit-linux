using LiveSplit.UI;
using System.Collections.Generic;
namespace LiveSplit.AutoSplittingRuntime;

public record ASRChoice(string Key, string Description);
public sealed class ASRWidget(ComponentSettings owner, string key, string type, string description, string tooltip, int level) : SettingsBase
{
    public string Key { get; } = key;
    public string Type { get; } = type;
    public string Description { get; } = description;
    public string Tooltip { get; } = tooltip;
    public int HeadingLevel { get; } = level;
    public string Filter { get; internal set; }
    public List<ASRChoice> Choices { get; } = [];
    private bool booleanValue;
    private string stringValue;
    public bool BooleanValue { get => booleanValue; set { if (SetField(ref booleanValue, value)) owner.SetValue(this); } }
    public string StringValue { get => stringValue; set { if (SetField(ref stringValue, value)) owner.SetValue(this); } }
    internal void Initialize(bool boolean, string text) { booleanValue = boolean; stringValue = text; }
}
