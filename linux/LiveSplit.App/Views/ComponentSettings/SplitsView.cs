using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using LiveSplit.Controls.Settings;
using System;

namespace LiveSplit.View.ComponentSettings;

public class SplitsView : SettingsPage
{
    public SplitsView(object model) : this(model, false) { }

    protected SplitsView(object model, bool subsplits) : base(model)
    {
        Add(Gradient(), Number("Total Splits:", "VisualSplitCount", 1, 1000),
            Number("Upcoming Splits:", "SplitPreviewCount", 0, 1000, maximum: "UpcomingSplitsMaximum"));
        if (subsplits) Add(Number("Minimum Number of Sections Before Current Section:", "MinimumMajorSplits", 0, 1000, "IsMinimumMajorSplitsEnabled"));
        Add(Check("Show Thin Separators", "ShowThinSeparators"), Check("Show Separator Before Last Split", "SeparatorLastSplit"), Check("Always Show Last Split", "AlwaysShowLastSplit"));
        if (!subsplits) Add(Check("Fill with Blank Space if Not Enough Splits", "ShowBlankSplits"));
        Add(Check("Lock Last Split to Bottom", "LockLastSplit", subsplits ? null : "IsLockLastSplitEnabled"),
            Check("Display 2 Rows", "Display2RowsShown", "IsDisplay2RowsEnabled"), Slider("", "SizeValue", 0, 250, size: true),
            Gradient("Current Split Background Color:", "CurrentSplitTopColor", "CurrentSplitBottomSwatchColor", "SplitGradientString", "SplitGradientOptions", "IsTopColorVisible"),
            Section("Split Names", Check("Automatically Abbreviate Names If Necessary", subsplits ? "AutomaticAbbreviation" : "AutomaticAbbreviations"),
                Section("Color", Check("Override Layout Settings", "OverrideTextColor"),
                    Color("Color (Before Current Split):", "BeforeNamesColor", "IsNamesColorEnabled"),
                    Color("Color (Current Split):", "CurrentNamesColor", "IsNamesColorEnabled"),
                    Color("Color (After Current Split):", "AfterNamesColor", "IsNamesColorEnabled"))));
        var icons = new StackPanel { Spacing = 6 };
        icons.Children.Add(Check("Display Icons", "DisplayIcons"));
        icons.Children.Add(Check("Show Icon Shadows", "IconShadows", "IsIconOptionsEnabled"));
        icons.Children.Add(Slider("Size:", "IconSize", 10, 80, "IsIconOptionsEnabled"));
        if (subsplits) icons.Children.Add(Check("Indent Blank Icons", "IndentBlankIcons"));
        Add(Section("Icons", icons),
            Section("Deltas", Accuracy("DeltasAccuracy", before: Check("Drop Decimals When More Than 1 Minute", "DropDecimals")), ColorOverride("Live Delta Color", "OverrideDeltasColor", "DeltasColor", "IsDeltaColorEnabled")),
            Section("Split Times", Section("Color", Check("Override Layout Settings", "OverrideTimesColor"),
                Color("Color (Before Current Split):", "BeforeTimesColor", "IsTimesColorEnabled"),
                Color("Color (Current Split):", "CurrentTimesColor", "IsTimesColorEnabled"),
                Color("Color (After Current Split):", "AfterTimesColor", "IsTimesColorEnabled")), Accuracy("SplitTimesAccuracy")));
        if (subsplits) AddSubsplits();
        var columns = new ItemsControl { ItemTemplate = new FuncDataTemplate<object>((column, _) => new ColumnView(column)) };
        Bind(columns, ItemsControl.ItemsSourceProperty, "ColumnsList", BindingMode.OneWay);
        Add(Section("Columns", Check("Show Column Labels", "ShowColumnLabelsShown", "IsColumnLabelsEnabled"), Color("Labels Color:", "LabelsColor", "IsLabelsColorEnabled"), Action("Add Column", "AddColumn"), columns));
    }

    private void AddSubsplits()
    {
        var visibility = new StackPanel();
        string group = Guid.NewGuid().ToString();
        foreach (var (label, path) in new[] { ("Always Hide Subsplits", "IsHideSubsplits"), ("Contextual Subsplits", "IsNormalSubsplits"), ("Always Show Subsplits", "IsShowSubsplits") })
        {
            var radio = new RadioButton { Content = T(label), GroupName = group };
            Bind(radio, RadioButton.IsCheckedProperty, path);
            visibility.Children.Add(radio);
        }
        Add(Section("Subsplits", new TextBlock { Text = T("To set a split as a subsplit, add a - (dash) to the beginning of the split name.\nOptional: Add a section name with {}. Ex: \"{Section Name} Split Name\""), TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            visibility, Check("Only Current Section", "CurrentSectionOnly", "IsCurrentSectionOnlyEnabled"),
            Check("Indent Subsplits", "IndentSubsplits", "IsIndentSubsplitsEnabled"),
            Check("Indent Last Split Of Section", "IndentSectionSplit", "IsIndentSectionSplitEnabled"), Check("Show Icon of Last Split of Section", "ShowIconSectionSplit"),
            Section("Subsplit Color", Check("Override Layout Settings", "OverrideSubsplitColor"), Gradient("Subsplit Background Color:", "SubsplitTopColor", "SubsplitBottomSwatchColor", "SubsplitGradientString", "SplitGradientOptions", "IsSubsplitTopColorVisible", enabled: "IsSubsplitColorEnabled")),
            Section("Section Header", Check("Show Section Header", "ShowHeader"),
                Check("Show Icon", "ShowSectionIcon", "IsHeaderOptionsEnabled"), Combo("Comparison:", "HeaderComparison", enabled: "IsHeaderOptionsEnabled", translate: false), Combo("Timing Method:", "HeaderTimingMethod", enabled: "IsHeaderOptionsEnabled"),
                Enabled(Section("Section Header Color", Gradient("Background Color:", "HeaderTopColor", "HeaderBottomSwatchColor", "HeaderGradientString", "SplitGradientOptions", "IsHeaderTopColorVisible"),
                    Check("Show Split Name", "HeaderText"), Check("Show Total Section Time", "HeaderTimes"), Check("Override Layout Text Colors", "OverrideHeaderColor"),
                    Color("Name Color:", "HeaderTextColor", "IsHeaderTextColorEnabled"), Color("Time Color:", "HeaderTimesColor", "IsHeaderTextColorEnabled"), Accuracy("HeaderAccuracy", "Time Accuracy")), "IsHeaderOptionsEnabled"),
                Enabled(Section("Section Timer", Check("Show Section Timer", "SectionTimer"), Color("Color:", "SectionTimerColor", "IsSectionTimerOptionsEnabled"), Check("Show Gradient", "SectionTimerGradient", "IsSectionTimerOptionsEnabled"), Accuracy("SectionTimerAccuracy", "Timer Accuracy", "IsSectionTimerOptionsEnabled")), "IsHeaderOptionsEnabled"))));
    }
}

public sealed class SubsplitsView(object model) : SplitsView(model, true);

public sealed class ColumnView : SettingsPage
{
    public ColumnView(object model) : base(model)
    {
        MinWidth = 0;
        var title = new TextBlock { FontWeight = Avalonia.Media.FontWeight.SemiBold };
        Bind(title, TextBlock.TextProperty, "GroupTitle", BindingMode.OneWay);
        Add(new Border { BorderBrush = Avalonia.Media.Brushes.Gray, BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(0, 8, 0, 0) }, title,
            Text("Name:", "ColumnName"), Combo("Column Type:", "Type", "ColumnTypeOptions"), Combo("Comparison:", "Comparison", translate: false), Combo("Timing Method:", "TimingMethod"),
            new WrapPanel { Children = { Action("Move Up", "MoveUp", "CanMoveUp"), Action("Move Down", "MoveDown", "CanMoveDown"), Action("Remove Column", "Remove") } });
    }
}
