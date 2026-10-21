using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public abstract class InfoSettingsPage(object model) : SettingsPage(model)
{
    protected Control TwoRows() => Check("Display 2 Rows", "Display2Rows", "IsDisplay2RowsEnabled", "Display2RowsShown");
    protected Control TextColor(string title = "Text Color") => ColorOverride(title, "OverrideTextColor", "TextColor", "IsTextColorEnabled");
    protected Control TimeColor(string title = "Time Color") => ColorOverride(title, "OverrideTimeColor", "TimeColor", "IsTimeColorEnabled");
}
