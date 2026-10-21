using LiveSplit.Localization;
using LiveSplit.Model;
using LiveSplit.TimeFormatters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Drawing;
using System.Linq;
using System.Xml;

namespace LiveSplit.UI.Components;

public class SplitsSettings : SettingsBase
{
    private static string T(string source) => UiLocalizer.Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());

    /// <summary>Items of the background gradient drop down (<see cref="GradientString"/>), in enum order.</summary>
    public static readonly string[] GradientOptions = [.. Enum.GetValues<ExtendedGradientType>().Select(x => x.ToString())];
    /// <summary>Items of the current split background gradient drop down (<see cref="SplitGradientString"/>): Plain, Vertical, Horizontal.</summary>
    public static readonly string[] SplitGradientOptions = ["Plain", "Vertical", "Horizontal"];

    private int visualSplitCount;
    private Color currentSplitTopColor;
    private Color currentSplitBottomColor;
    private int splitPreviewCount;
    private float splitWidth;
    private float splitHeight;
    private float iconSize;
    private bool display2Rows;
    private Color backgroundColor;
    private Color backgroundColor2;
    private ExtendedGradientType backgroundGradient;
    private bool displayIcons;
    private bool iconShadows;
    private bool showThinSeparators;
    private bool alwaysShowLastSplit;
    private bool showBlankSplits;
    private bool lockLastSplit;
    private bool separatorLastSplit;
    private bool dropDecimals;
    private TimeAccuracy deltasAccuracy;
    private bool overrideDeltasColor;
    private Color deltasColor;
    private bool showColumnLabels;
    private Color labelsColor;
    private bool automaticAbbreviations;
    private Color beforeNamesColor;
    private Color currentNamesColor;
    private Color afterNamesColor;
    private bool overrideTextColor;
    private Color beforeTimesColor;
    private Color currentTimesColor;
    private Color afterTimesColor;
    private bool overrideTimesColor;
    private TimeAccuracy splitTimesAccuracy;
    private GradientType currentSplitGradient;
    private LayoutMode mode;
    private bool loading;
    private readonly HashSet<ColumnSettings> attachedColumns = [];

    /// <summary>"Total Splits": number of rows of the component (1..1000).</summary>
    public int VisualSplitCount
    {
        get => visualSplitCount;
        set
        {
            if (SetField(ref visualSplitCount, value))
            {
                OnPropertyChanged(nameof(UpcomingSplitsMaximum));
            }

            // Same as the old code: re-assigning the count (also done when AlwaysShowLastSplit changes) clamps the preview count.
            if (SplitPreviewCount > UpcomingSplitsMaximum)
            {
                SplitPreviewCount = UpcomingSplitsMaximum;
            }
        }
    }

    public Color CurrentSplitTopColor
    {
        get => currentSplitTopColor;
        set
        {
            if (SetField(ref currentSplitTopColor, value))
            {
                OnPropertyChanged(nameof(CurrentSplitBottomSwatchColor));
            }
        }
    }

    public Color CurrentSplitBottomColor
    {
        get => currentSplitBottomColor;
        set
        {
            if (SetField(ref currentSplitBottomColor, value))
            {
                OnPropertyChanged(nameof(CurrentSplitBottomSwatchColor));
            }
        }
    }

    /// <summary>"Upcoming Splits": how many splits after the current one are kept visible.</summary>
    public int SplitPreviewCount { get => splitPreviewCount; set => SetField(ref splitPreviewCount, value); }

    public float SplitWidth
    {
        get => splitWidth;
        set
        {
            if (SetField(ref splitWidth, value))
            {
                OnPropertyChanged(nameof(SizeValue));
            }
        }
    }

    public float SplitHeight
    {
        get => splitHeight;
        set
        {
            if (SetField(ref splitHeight, value))
            {
                OnPropertyChanged(nameof(ScaledSplitHeight));
                OnPropertyChanged(nameof(SizeValue));
            }
        }
    }

    public float ScaledSplitHeight { get => SplitHeight * 10f; set => SplitHeight = value / 10f; }

    public float IconSize { get => iconSize; set => SetField(ref iconSize, value); }

    public bool Display2Rows
    {
        get => display2Rows;
        set
        {
            if (SetField(ref display2Rows, value))
            {
                OnPropertyChanged(nameof(Display2RowsShown));
            }
        }
    }

    public Color BackgroundColor
    {
        get => backgroundColor;
        set
        {
            if (SetField(ref backgroundColor, value))
            {
                OnPropertyChanged(nameof(BackgroundSwatch2Color));
            }
        }
    }

    public Color BackgroundColor2
    {
        get => backgroundColor2;
        set
        {
            if (SetField(ref backgroundColor2, value))
            {
                OnPropertyChanged(nameof(BackgroundSwatch2Color));
            }
        }
    }

    public ExtendedGradientType BackgroundGradient
    {
        get => backgroundGradient;
        set
        {
            if (SetField(ref backgroundGradient, value))
            {
                OnPropertyChanged(nameof(GradientString));
                OnPropertyChanged(nameof(IsColor1Visible));
                OnPropertyChanged(nameof(BackgroundSwatch2Color));
            }
        }
    }

    public string GradientString
    {
        get => BackgroundGradient.ToString();
        set => BackgroundGradient = (ExtendedGradientType)Enum.Parse(typeof(ExtendedGradientType), value);
    }

    /// <summary>The first background swatch is hidden when the gradient is Plain; the second swatch is always visible.</summary>
    public bool IsColor1Visible => BackgroundGradient != ExtendedGradientType.Plain;

    /// <summary>Second background swatch: edits <see cref="BackgroundColor2"/>, or <see cref="BackgroundColor"/> when the gradient is Plain.</summary>
    public Color BackgroundSwatch2Color
    {
        get => IsColor1Visible ? BackgroundColor2 : BackgroundColor;
        set
        {
            if (IsColor1Visible)
            {
                BackgroundColor2 = value;
            }
            else
            {
                BackgroundColor = value;
            }
        }
    }

    public LiveSplitState CurrentState { get; set; }

    public bool DisplayIcons
    {
        get => displayIcons;
        set
        {
            if (SetField(ref displayIcons, value))
            {
                OnPropertyChanged(nameof(IsIconOptionsEnabled));
            }
        }
    }

    public bool IconShadows { get => iconShadows; set => SetField(ref iconShadows, value); }

    public bool ShowThinSeparators
    {
        get => showThinSeparators;
        set
        {
            if (SetField(ref showThinSeparators, value))
            {
                RaiseSplitLayoutChanged();
            }
        }
    }

    public bool AlwaysShowLastSplit
    {
        get => alwaysShowLastSplit;
        set
        {
            if (SetField(ref alwaysShowLastSplit, value))
            {
                OnPropertyChanged(nameof(UpcomingSplitsMaximum));
                if (!loading)
                {
                    VisualSplitCount = VisualSplitCount;
                }

                RaiseSplitLayoutChanged();
            }
        }
    }

    public bool ShowBlankSplits
    {
        get => showBlankSplits;
        set
        {
            if (SetField(ref showBlankSplits, value))
            {
                OnPropertyChanged(nameof(IsLockLastSplitEnabled));
                RaiseSplitLayoutChanged();
            }
        }
    }

    public bool LockLastSplit
    {
        get => lockLastSplit;
        set
        {
            if (SetField(ref lockLastSplit, value))
            {
                RaiseSplitLayoutChanged();
            }
        }
    }

    public bool SeparatorLastSplit
    {
        get => separatorLastSplit;
        set
        {
            if (SetField(ref separatorLastSplit, value))
            {
                RaiseSplitLayoutChanged();
            }
        }
    }

    public bool DropDecimals { get => dropDecimals; set => SetField(ref dropDecimals, value); }

    public TimeAccuracy DeltasAccuracy
    {
        get => deltasAccuracy;
        set
        {
            if (SetField(ref deltasAccuracy, value))
            {
                OnPropertyChanged(nameof(IsDeltaSeconds));
                OnPropertyChanged(nameof(IsDeltaTenths));
                OnPropertyChanged(nameof(IsDeltaHundredths));
                OnPropertyChanged(nameof(IsDeltaMilliseconds));
            }
        }
    }

    public bool OverrideDeltasColor
    {
        get => overrideDeltasColor;
        set
        {
            if (SetField(ref overrideDeltasColor, value))
            {
                OnPropertyChanged(nameof(IsDeltaColorEnabled));
            }
        }
    }

    public Color DeltasColor { get => deltasColor; set => SetField(ref deltasColor, value); }

    public bool ShowColumnLabels
    {
        get => showColumnLabels;
        set
        {
            if (SetField(ref showColumnLabels, value))
            {
                OnPropertyChanged(nameof(ShowColumnLabelsShown));
                OnPropertyChanged(nameof(IsLabelsColorEnabled));
            }
        }
    }

    public Color LabelsColor { get => labelsColor; set => SetField(ref labelsColor, value); }

    public bool AutomaticAbbreviations { get => automaticAbbreviations; set => SetField(ref automaticAbbreviations, value); }
    public Color BeforeNamesColor { get => beforeNamesColor; set => SetField(ref beforeNamesColor, value); }
    public Color CurrentNamesColor { get => currentNamesColor; set => SetField(ref currentNamesColor, value); }
    public Color AfterNamesColor { get => afterNamesColor; set => SetField(ref afterNamesColor, value); }

    public bool OverrideTextColor
    {
        get => overrideTextColor;
        set
        {
            if (SetField(ref overrideTextColor, value))
            {
                OnPropertyChanged(nameof(IsNamesColorEnabled));
            }
        }
    }

    public Color BeforeTimesColor { get => beforeTimesColor; set => SetField(ref beforeTimesColor, value); }
    public Color CurrentTimesColor { get => currentTimesColor; set => SetField(ref currentTimesColor, value); }
    public Color AfterTimesColor { get => afterTimesColor; set => SetField(ref afterTimesColor, value); }

    public bool OverrideTimesColor
    {
        get => overrideTimesColor;
        set
        {
            if (SetField(ref overrideTimesColor, value))
            {
                OnPropertyChanged(nameof(IsTimesColorEnabled));
            }
        }
    }

    public TimeAccuracy SplitTimesAccuracy
    {
        get => splitTimesAccuracy;
        set
        {
            if (SetField(ref splitTimesAccuracy, value))
            {
                OnPropertyChanged(nameof(IsSplitTimesSeconds));
                OnPropertyChanged(nameof(IsSplitTimesTenths));
                OnPropertyChanged(nameof(IsSplitTimesHundredths));
                OnPropertyChanged(nameof(IsSplitTimesMilliseconds));
            }
        }
    }

    public GradientType CurrentSplitGradient
    {
        get => currentSplitGradient;
        set
        {
            if (SetField(ref currentSplitGradient, value))
            {
                OnPropertyChanged(nameof(SplitGradientString));
                OnPropertyChanged(nameof(IsTopColorVisible));
                OnPropertyChanged(nameof(CurrentSplitBottomSwatchColor));
            }
        }
    }

    public string SplitGradientString
    {
        get => CurrentSplitGradient.ToString();
        set => CurrentSplitGradient = (GradientType)Enum.Parse(typeof(GradientType), value);
    }

    /// <summary>The top colour swatch of the current split background is hidden when the gradient is Plain.</summary>
    public bool IsTopColorVisible => CurrentSplitGradient != GradientType.Plain;

    /// <summary>Bottom swatch of the current split background: edits <see cref="CurrentSplitBottomColor"/>, or <see cref="CurrentSplitTopColor"/> when the gradient is Plain.</summary>
    public Color CurrentSplitBottomSwatchColor
    {
        get => IsTopColorVisible ? CurrentSplitBottomColor : CurrentSplitTopColor;
        set
        {
            if (IsTopColorVisible)
            {
                CurrentSplitBottomColor = value;
            }
            else
            {
                CurrentSplitTopColor = value;
            }
        }
    }

    /// <summary>Raised when a setting changes that requires the component to rebuild its rows (not raised while loading settings).</summary>
    public event EventHandler SplitLayoutChanged;

    public LayoutMode Mode
    {
        get => mode;
        set
        {
            if (SetField(ref mode, value))
            {
                OnPropertyChanged(nameof(SizeLabel));
                OnPropertyChanged(nameof(SizeMinimum));
                OnPropertyChanged(nameof(SizeMaximum));
                OnPropertyChanged(nameof(SizeValue));
                OnPropertyChanged(nameof(IsDisplay2RowsEnabled));
                OnPropertyChanged(nameof(Display2RowsShown));
                OnPropertyChanged(nameof(IsColumnLabelsEnabled));
                OnPropertyChanged(nameof(ShowColumnLabelsShown));
                OnPropertyChanged(nameof(IsLabelsColorEnabled));
            }
        }
    }

    /// <summary>The columns, in display order. Each entry is the model of one "Column: ..." group.</summary>
    public ObservableCollection<ColumnSettings> ColumnsList { get; }

    public SplitsSettings(LiveSplitState state)
    {
        CurrentState = state;
        ColumnsList = [];
        ColumnsList.CollectionChanged += ColumnsList_CollectionChanged;

        VisualSplitCount = 8;
        SplitPreviewCount = 1;
        DisplayIcons = true;
        IconShadows = true;
        ShowThinSeparators = true;
        AlwaysShowLastSplit = true;
        ShowBlankSplits = true;
        LockLastSplit = true;
        SeparatorLastSplit = true;
        SplitTimesAccuracy = TimeAccuracy.Seconds;
        CurrentSplitTopColor = Color.FromArgb(51, 115, 244);
        CurrentSplitBottomColor = Color.FromArgb(21, 53, 116);
        SplitWidth = 20;
        SplitHeight = 3.6f;
        IconSize = 24f;
        AutomaticAbbreviations = false;
        BeforeNamesColor = Color.FromArgb(255, 255, 255);
        CurrentNamesColor = Color.FromArgb(255, 255, 255);
        AfterNamesColor = Color.FromArgb(255, 255, 255);
        OverrideTextColor = false;
        BeforeTimesColor = Color.FromArgb(255, 255, 255);
        CurrentTimesColor = Color.FromArgb(255, 255, 255);
        AfterTimesColor = Color.FromArgb(255, 255, 255);
        OverrideTimesColor = false;
        CurrentSplitGradient = GradientType.Vertical;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.FromArgb(1, 255, 255, 255);
        BackgroundGradient = ExtendedGradientType.Alternating;
        DropDecimals = true;
        DeltasAccuracy = TimeAccuracy.Tenths;
        OverrideDeltasColor = false;
        DeltasColor = Color.FromArgb(255, 255, 255);
        Display2Rows = false;
        ShowColumnLabels = false;
        LabelsColor = Color.FromArgb(255, 255, 255);

        ColumnsList.Add(new ColumnSettings(CurrentState, "+/-", ColumnsList) { Data = new ColumnData("+/-", ColumnType.Delta, "Current Comparison", "Current Timing Method") });
        ColumnsList.Add(new ColumnSettings(CurrentState, T("Time"), ColumnsList) { Data = new ColumnData(T("Time"), ColumnType.SplitTime, "Current Comparison", "Current Timing Method") });
    }

    // ---- view state (what the old Load/CheckedChanged handlers derived from the controls) ----

    /// <summary>Maximum of the "Upcoming Splits" box: the total minus the current split and (when shown) the pinned last split.</summary>
    public int UpcomingSplitsMaximum => Math.Max(0, VisualSplitCount - (AlwaysShowLastSplit ? 2 : 1));

    /// <summary>"Lock Last Split to Bottom" is only available when blank splits are shown.</summary>
    public bool IsLockLastSplitEnabled => ShowBlankSplits;

    /// <summary>Enabled state of the "Size" row and "Show Icon Shadows" of the Icons group.</summary>
    public bool IsIconOptionsEnabled => DisplayIcons;

    public bool IsNamesColorEnabled => OverrideTextColor;
    public bool IsTimesColorEnabled => OverrideTimesColor;
    public bool IsDeltaColorEnabled => OverrideDeltasColor;

    /// <summary>"Split Width:" in horizontal layouts, "Split Height:" in vertical layouts (translated).</summary>
    public string SizeLabel => Mode == LayoutMode.Horizontal ? T("Split Width:") : T("Split Height:");
    public float SizeMinimum => Mode == LayoutMode.Horizontal ? 5 : 0;
    public float SizeMaximum => Mode == LayoutMode.Horizontal ? 120 : 250;

    /// <summary>Maps to <see cref="SplitWidth"/> in horizontal layouts and <see cref="ScaledSplitHeight"/> (tenths) in vertical layouts.</summary>
    public float SizeValue
    {
        get => Mode == LayoutMode.Horizontal ? SplitWidth : ScaledSplitHeight;
        set
        {
            if (Mode == LayoutMode.Horizontal)
            {
                SplitWidth = value;
            }
            else
            {
                ScaledSplitHeight = value;
            }
        }
    }

    /// <summary>"Display 2 Rows" is forced on (and disabled) in horizontal layouts.</summary>
    public bool IsDisplay2RowsEnabled => Mode != LayoutMode.Horizontal;

    /// <summary>What the "Display 2 Rows" box shows: always checked in horizontal layouts, <see cref="Display2Rows"/> otherwise. Setting it in horizontal layouts is ignored.</summary>
    public bool Display2RowsShown
    {
        get => Mode == LayoutMode.Horizontal || Display2Rows;
        set
        {
            if (Mode != LayoutMode.Horizontal)
            {
                Display2Rows = value;
            }
        }
    }

    /// <summary>"Show Column Labels" is unavailable (forced off) in horizontal layouts.</summary>
    public bool IsColumnLabelsEnabled => Mode != LayoutMode.Horizontal;

    /// <summary>What the "Show Column Labels" box shows: always unchecked in horizontal layouts, <see cref="ShowColumnLabels"/> otherwise.</summary>
    public bool ShowColumnLabelsShown
    {
        get => Mode != LayoutMode.Horizontal && ShowColumnLabels;
        set
        {
            if (Mode != LayoutMode.Horizontal)
            {
                ShowColumnLabels = value;
            }
        }
    }

    /// <summary>The labels colour is enabled when the labels are shown.</summary>
    public bool IsLabelsColorEnabled => ShowColumnLabelsShown;

    // Radio button groups. Getters are true for the current accuracy; setting one to true selects it (false is ignored,
    // the group raises it for the radio that gets deselected).
    public bool IsSplitTimesSeconds { get => SplitTimesAccuracy == TimeAccuracy.Seconds; set => SelectSplitTimesAccuracy(value, TimeAccuracy.Seconds); }
    public bool IsSplitTimesTenths { get => SplitTimesAccuracy == TimeAccuracy.Tenths; set => SelectSplitTimesAccuracy(value, TimeAccuracy.Tenths); }
    public bool IsSplitTimesHundredths { get => SplitTimesAccuracy == TimeAccuracy.Hundredths; set => SelectSplitTimesAccuracy(value, TimeAccuracy.Hundredths); }
    public bool IsSplitTimesMilliseconds { get => SplitTimesAccuracy == TimeAccuracy.Milliseconds; set => SelectSplitTimesAccuracy(value, TimeAccuracy.Milliseconds); }
    public bool IsDeltaSeconds { get => DeltasAccuracy == TimeAccuracy.Seconds; set => SelectDeltaAccuracy(value, TimeAccuracy.Seconds); }
    public bool IsDeltaTenths { get => DeltasAccuracy == TimeAccuracy.Tenths; set => SelectDeltaAccuracy(value, TimeAccuracy.Tenths); }
    public bool IsDeltaHundredths { get => DeltasAccuracy == TimeAccuracy.Hundredths; set => SelectDeltaAccuracy(value, TimeAccuracy.Hundredths); }
    public bool IsDeltaMilliseconds { get => DeltasAccuracy == TimeAccuracy.Milliseconds; set => SelectDeltaAccuracy(value, TimeAccuracy.Milliseconds); }

    private void SelectSplitTimesAccuracy(bool selected, TimeAccuracy accuracy)
    {
        if (selected)
        {
            SplitTimesAccuracy = accuracy;
        }
    }

    private void SelectDeltaAccuracy(bool selected, TimeAccuracy accuracy)
    {
        if (selected)
        {
            DeltasAccuracy = accuracy;
        }
    }

    private void RaiseSplitLayoutChanged()
    {
        if (!loading)
        {
            SplitLayoutChanged?.Invoke(this, null);
        }
    }

    /// <summary>
    /// The work the old control did in its Load handler, before the page is shown: clamps the split size to the slider
    /// range of the current layout mode and initialises the columns. Called by <c>SplitsComponent.GetSettingsControl</c>
    /// after it sets <see cref="Mode"/>.
    /// </summary>
    public void PrepareForDisplay()
    {
        foreach (ColumnSettings column in ColumnsList.ToList())
        {
            column.Initialize();
        }

        if (Mode == LayoutMode.Horizontal)
        {
            SplitWidth = Math.Min(Math.Max(SizeMinimum, SplitWidth), SizeMaximum);
        }
        else
        {
            ScaledSplitHeight = Math.Min(Math.Max(SizeMinimum, ScaledSplitHeight), SizeMaximum);
        }

        UpdateColumnButtons();
    }

    // ---- columns ----

    /// <summary>"Add Column" button: appends a column with an empty name.</summary>
    public ColumnSettings AddColumn()
    {
        var column = new ColumnSettings(CurrentState, "", ColumnsList);
        ColumnsList.Add(column);
        return column;
    }

    private void ColumnsList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // Reset (Clear) does not supply OldItems. Detach removed models as well as replacements.
        foreach (ColumnSettings column in attachedColumns.Where(x => !ColumnsList.Contains(x)).ToList())
        {
            DetachColumn(column);
        }

        if (e.OldItems != null)
        {
            foreach (ColumnSettings column in e.OldItems)
            {
                DetachColumn(column);
            }
        }

        if (e.NewItems != null)
        {
            foreach (ColumnSettings column in e.NewItems)
            {
                DetachColumn(column);
                column.ColumnRemoved += column_ColumnRemoved;
                column.MovedUp += column_MovedUp;
                column.MovedDown += column_MovedDown;
                attachedColumns.Add(column);
            }
        }

        UpdateColumnButtons();
    }

    private void DetachColumn(ColumnSettings column)
    {
        attachedColumns.Remove(column);
        column.ColumnRemoved -= column_ColumnRemoved;
        column.MovedUp -= column_MovedUp;
        column.MovedDown -= column_MovedDown;
    }

    private void UpdateColumnButtons()
    {
        foreach (ColumnSettings column in ColumnsList)
        {
            column.UpdateEnabledButtons();
        }
    }

    private void column_MovedDown(object sender, EventArgs e)
    {
        var column = (ColumnSettings)sender;
        int index = ColumnsList.IndexOf(column);
        if (index >= 0 && index < ColumnsList.Count - 1)
        {
            ColumnsList.Move(index, index + 1);
        }
    }

    private void column_MovedUp(object sender, EventArgs e)
    {
        var column = (ColumnSettings)sender;
        int index = ColumnsList.IndexOf(column);
        if (index > 0)
        {
            ColumnsList.Move(index, index - 1);
        }
    }

    private void column_ColumnRemoved(object sender, EventArgs e)
    {
        ColumnsList.Remove((ColumnSettings)sender);
    }

    // ---- persistence (unchanged) ----

    public void SetSettings(XmlNode node)
    {
        loading = true;
        try
        {
            SetSettingsCore(node);
        }
        finally
        {
            loading = false;
        }
    }

    private void SetSettingsCore(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);

        CurrentSplitTopColor = SettingsHelper.ParseColor(element["CurrentSplitTopColor"]);
        CurrentSplitBottomColor = SettingsHelper.ParseColor(element["CurrentSplitBottomColor"]);
        VisualSplitCount = SettingsHelper.ParseInt(element["VisualSplitCount"]);
        SplitPreviewCount = SettingsHelper.ParseInt(element["SplitPreviewCount"]);
        DisplayIcons = SettingsHelper.ParseBool(element["DisplayIcons"]);
        ShowThinSeparators = SettingsHelper.ParseBool(element["ShowThinSeparators"]);
        AlwaysShowLastSplit = SettingsHelper.ParseBool(element["AlwaysShowLastSplit"]);
        SplitWidth = SettingsHelper.ParseFloat(element["SplitWidth"]);
        AutomaticAbbreviations = SettingsHelper.ParseBool(element["AutomaticAbbreviations"], false);
        ShowColumnLabels = SettingsHelper.ParseBool(element["ShowColumnLabels"], false);
        LabelsColor = SettingsHelper.ParseColor(element["LabelsColor"], Color.FromArgb(255, 255, 255));
        OverrideTimesColor = SettingsHelper.ParseBool(element["OverrideTimesColor"], false);
        BeforeTimesColor = SettingsHelper.ParseColor(element["BeforeTimesColor"], Color.FromArgb(255, 255, 255));
        CurrentTimesColor = SettingsHelper.ParseColor(element["CurrentTimesColor"], Color.FromArgb(255, 255, 255));
        AfterTimesColor = SettingsHelper.ParseColor(element["AfterTimesColor"], Color.FromArgb(255, 255, 255));
        SplitHeight = SettingsHelper.ParseFloat(element["SplitHeight"], 6);
        SplitGradientString = SettingsHelper.ParseString(element["CurrentSplitGradient"], GradientType.Vertical.ToString());
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"], Color.Transparent);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"], Color.Transparent);
        GradientString = SettingsHelper.ParseString(element["BackgroundGradient"], ExtendedGradientType.Plain.ToString());
        SeparatorLastSplit = SettingsHelper.ParseBool(element["SeparatorLastSplit"], true);
        DropDecimals = SettingsHelper.ParseBool(element["DropDecimals"], true);
        DeltasAccuracy = SettingsHelper.ParseEnum(element["DeltasAccuracy"], TimeAccuracy.Tenths);
        OverrideDeltasColor = SettingsHelper.ParseBool(element["OverrideDeltasColor"], false);
        DeltasColor = SettingsHelper.ParseColor(element["DeltasColor"], Color.FromArgb(255, 255, 255));
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"], false);
        SplitTimesAccuracy = SettingsHelper.ParseEnum(element["SplitTimesAccuracy"], TimeAccuracy.Seconds);
        ShowBlankSplits = SettingsHelper.ParseBool(element["ShowBlankSplits"], true);
        LockLastSplit = SettingsHelper.ParseBool(element["LockLastSplit"], false);
        IconSize = SettingsHelper.ParseFloat(element["IconSize"], 24f);
        IconShadows = SettingsHelper.ParseBool(element["IconShadows"], true);

        if (version >= new Version(1, 5))
        {
            XmlElement columnsElement = element["Columns"];
            ColumnsList.Clear();
            foreach (object child in columnsElement.ChildNodes)
            {
                var columnData = ColumnData.FromXml((XmlNode)child);
                ColumnsList.Add(new ColumnSettings(CurrentState, columnData.Name, ColumnsList) { Data = columnData });
            }
        }
        else
        {
            ColumnsList.Clear();
            string comparison = SettingsHelper.ParseString(element["Comparison"]);
            if (SettingsHelper.ParseBool(element["ShowSplitTimes"]))
            {
                ColumnsList.Add(new ColumnSettings(CurrentState, "+/-", ColumnsList) { Data = new ColumnData("+/-", ColumnType.Delta, comparison, "Current Timing Method") });
                ColumnsList.Add(new ColumnSettings(CurrentState, "Time", ColumnsList) { Data = new ColumnData("Time", ColumnType.SplitTime, comparison, "Current Timing Method") });
            }
            else
            {
                ColumnsList.Add(new ColumnSettings(CurrentState, "+/-", ColumnsList) { Data = new ColumnData("+/-", ColumnType.DeltaorSplitTime, comparison, "Current Timing Method") });
            }
        }

        if (version >= new Version(1, 3))
        {
            BeforeNamesColor = SettingsHelper.ParseColor(element["BeforeNamesColor"]);
            CurrentNamesColor = SettingsHelper.ParseColor(element["CurrentNamesColor"]);
            AfterNamesColor = SettingsHelper.ParseColor(element["AfterNamesColor"]);
            OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        }
        else
        {
            if (version >= new Version(1, 2))
            {
                BeforeNamesColor = CurrentNamesColor = AfterNamesColor = SettingsHelper.ParseColor(element["SplitNamesColor"]);
            }
            else
            {
                BeforeNamesColor = Color.FromArgb(255, 255, 255);
                CurrentNamesColor = Color.FromArgb(255, 255, 255);
                AfterNamesColor = Color.FromArgb(255, 255, 255);
            }

            OverrideTextColor = !SettingsHelper.ParseBool(element["UseTextColor"], true);
        }
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
        int hashCode = SettingsHelper.CreateSetting(document, parent, "Version", "1.6") ^
        SettingsHelper.CreateSetting(document, parent, "CurrentSplitTopColor", CurrentSplitTopColor) ^
        SettingsHelper.CreateSetting(document, parent, "CurrentSplitBottomColor", CurrentSplitBottomColor) ^
        SettingsHelper.CreateSetting(document, parent, "VisualSplitCount", VisualSplitCount) ^
        SettingsHelper.CreateSetting(document, parent, "SplitPreviewCount", SplitPreviewCount) ^
        SettingsHelper.CreateSetting(document, parent, "DisplayIcons", DisplayIcons) ^
        SettingsHelper.CreateSetting(document, parent, "ShowThinSeparators", ShowThinSeparators) ^
        SettingsHelper.CreateSetting(document, parent, "AlwaysShowLastSplit", AlwaysShowLastSplit) ^
        SettingsHelper.CreateSetting(document, parent, "SplitWidth", SplitWidth) ^
        SettingsHelper.CreateSetting(document, parent, "SplitTimesAccuracy", SplitTimesAccuracy) ^
        SettingsHelper.CreateSetting(document, parent, "AutomaticAbbreviations", AutomaticAbbreviations) ^
        SettingsHelper.CreateSetting(document, parent, "BeforeNamesColor", BeforeNamesColor) ^
        SettingsHelper.CreateSetting(document, parent, "CurrentNamesColor", CurrentNamesColor) ^
        SettingsHelper.CreateSetting(document, parent, "AfterNamesColor", AfterNamesColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
        SettingsHelper.CreateSetting(document, parent, "BeforeTimesColor", BeforeTimesColor) ^
        SettingsHelper.CreateSetting(document, parent, "CurrentTimesColor", CurrentTimesColor) ^
        SettingsHelper.CreateSetting(document, parent, "AfterTimesColor", AfterTimesColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTimesColor", OverrideTimesColor) ^
        SettingsHelper.CreateSetting(document, parent, "ShowBlankSplits", ShowBlankSplits) ^
        SettingsHelper.CreateSetting(document, parent, "LockLastSplit", LockLastSplit) ^
        SettingsHelper.CreateSetting(document, parent, "IconSize", IconSize) ^
        SettingsHelper.CreateSetting(document, parent, "IconShadows", IconShadows) ^
        SettingsHelper.CreateSetting(document, parent, "SplitHeight", SplitHeight) ^
        SettingsHelper.CreateSetting(document, parent, "CurrentSplitGradient", CurrentSplitGradient) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "SeparatorLastSplit", SeparatorLastSplit) ^
        SettingsHelper.CreateSetting(document, parent, "DeltasAccuracy", DeltasAccuracy) ^
        SettingsHelper.CreateSetting(document, parent, "DropDecimals", DropDecimals) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideDeltasColor", OverrideDeltasColor) ^
        SettingsHelper.CreateSetting(document, parent, "DeltasColor", DeltasColor) ^
        SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
        SettingsHelper.CreateSetting(document, parent, "ShowColumnLabels", ShowColumnLabels) ^
        SettingsHelper.CreateSetting(document, parent, "LabelsColor", LabelsColor);

        XmlElement columnsElement = null;
        if (document != null)
        {
            columnsElement = document.CreateElement("Columns");
            parent.AppendChild(columnsElement);
        }

        int count = 1;
        foreach (ColumnData columnData in ColumnsList.Select(x => x.Data))
        {
            XmlElement settings = null;
            if (document != null)
            {
                settings = document.CreateElement("Settings");
                columnsElement.AppendChild(settings);
            }

            hashCode ^= columnData.CreateElement(document, settings) * count;
            count++;
        }

        return hashCode;
    }
}
