using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class TitleView : SettingsPage
{
    public TitleView(object model) : base(model)
    {
        Add(Gradient(), ColorOverride("Title Color", "OverrideTitleColor", "TitleColor", "IsTitleColorEnabled"),
    Check("Show Game Name", "ShowGameName"), Check("Show Category Name", "ShowCategoryName"),
    Check("Show Finished Runs Count", "ShowFinishedRunsCount"), Check("Show Attempt Count", "ShowAttemptCount"),
    Check("Display Game Icon", "DisplayGameIcon"), Check("Display Game and Category in Single Line", "SingleLine"),
    Combo("Text Alignment:", "TextAlignmentIndex", "TextAlignmentOptions", index: true),
    Section("Additional Category Info", Check("Show Region", "ShowRegion"), Check("Show Platform", "ShowPlatform"), Check("Show Variables", "ShowVariables")));
    }
}
