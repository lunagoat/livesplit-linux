using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class TextView : SettingsPage
{
    public TextView(object model) : base(model)
    {
        Add(Gradient(), Check("Display 2 Rows", "Display2Rows", "IsDisplay2RowsEnabled", "Display2RowsShown"),
    Tooltip(Check("Custom Variable", "CustomVariable"), "Custom Variable", "Specifies whether to use a custom variable to display a dynamic value from an autosplitter."),
    Section("Left Text", Text("Text:", "Text1"), ColorOverride("Color", "OverrideTextColor", "TextColor", "IsTextColorEnabled")),
    Section("Right Text", Text("", "Text2", labelProperty: "Text2Label"), ColorOverride("Color", "OverrideTimeColor", "TimeColor", "IsTimeColorEnabled")));
    }
}
