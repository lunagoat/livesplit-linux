using LiveSplit.Localization;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
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

    private LiveSplitState currentState;
    public LiveSplitState CurrentState
    {
        get => currentState;
        set
        {
            if (SetField(ref currentState, value))
            {
                OnPropertyChanged(nameof(HeaderComparisonOptions));
            }
        }
    }

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

    public bool AutomaticAbbreviation { get => automaticAbbreviations; set => SetField(ref automaticAbbreviations, value); }
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

        AutomaticAbbreviation = false;
        VisualSplitCount = 8;
        SplitPreviewCount = 1;
        MinimumMajorSplits = 0;
        DisplayIcons = true;
        IconShadows = true;
        ShowThinSeparators = false;
        AlwaysShowLastSplit = true;
        LockLastSplit = true;
        SeparatorLastSplit = true;
        SplitTimesAccuracy = TimeAccuracy.Seconds;
        CurrentSplitTopColor = Color.FromArgb(51, 115, 244);
        CurrentSplitBottomColor = Color.FromArgb(21, 53, 116);
        SplitWidth = 20;
        SplitHeight = 3.6f;
        ScaledSplitHeight = 60;
        IconSize = 24f;
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
        HeaderComparison = "Current Comparison";
        HeaderTimingMethod = "Current Timing Method";
        Display2Rows = false;
        ShowColumnLabels = false;
        LabelsColor = Color.FromArgb(255, 255, 255);

        IndentBlankIcons = true;
        IndentSubsplits = true;
        HideSubsplits = false;
        ShowSubsplits = false;
        CurrentSectionOnly = false;
        OverrideSubsplitColor = false;
        SubsplitTopColor = Color.FromArgb(0x8D, 0x00, 0x00, 0x00);
        SubsplitBottomColor = Color.Transparent;
        SubsplitGradient = GradientType.Plain;
        ShowHeader = true;
        IndentSectionSplit = true;
        ShowIconSectionSplit = true;
        ShowSectionIcon = true;
        HeaderTopColor = Color.FromArgb(0x2B, 0xFF, 0xFF, 0xFF);
        HeaderBottomColor = Color.FromArgb(0xD8, 0x00, 0x00, 0x00);
        HeaderGradient = GradientType.Vertical;
        OverrideHeaderColor = false;
        HeaderTextColor = Color.FromArgb(255, 255, 255);
        HeaderText = true;
        HeaderTimesColor = Color.FromArgb(255, 255, 255);
        HeaderTimes = true;
        HeaderAccuracy = TimeAccuracy.Tenths;
        SectionTimer = true;
        SectionTimerColor = Color.FromArgb(0x77, 0x77, 0x77);
        SectionTimerGradient = true;
        SectionTimerAccuracy = TimeAccuracy.Tenths;


        ColumnsList.Add(new ColumnSettings(CurrentState, "+/-", ColumnsList) { Data = new ColumnData("+/-", ColumnType.Delta, "Current Comparison", "Current Timing Method") });
        ColumnsList.Add(new ColumnSettings(CurrentState, T("Time"), ColumnsList) { Data = new ColumnData(T("Time"), ColumnType.SplitTime, "Current Comparison", "Current Timing Method") });
    }

    // ---- view state (what the old Load/CheckedChanged handlers derived from the controls) ----

    /// <summary>Maximum of the "Upcoming Splits" box: the total minus the current split and (when shown) the pinned last split.</summary>
    public int UpcomingSplitsMaximum => Math.Max(0, VisualSplitCount - (AlwaysShowLastSplit ? 2 : 1));

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

    private int minimumMajorSplits;
    public int MinimumMajorSplits
    {
        get => minimumMajorSplits;
        set => SetField(ref minimumMajorSplits, value);
    }

    private string headerComparison;
    public string HeaderComparison
    {
        get => headerComparison;
        set
        {
            if (SetField(ref headerComparison, value))
            {
                OnPropertyChanged(nameof(HeaderComparisonOptions));
            }
        }
    }

    private string headerTimingMethod;
    public string HeaderTimingMethod
    {
        get => headerTimingMethod;
        set => SetField(ref headerTimingMethod, value);
    }

    private bool indentBlankIcons;
    public bool IndentBlankIcons
    {
        get => indentBlankIcons;
        set
        {
            if (SetField(ref indentBlankIcons, value))
            {
                RaiseSplitLayoutChanged();
            }
        }
    }

    private bool indentSubsplits;
    public bool IndentSubsplits
    {
        get => indentSubsplits;
        set
        {
            if (SetField(ref indentSubsplits, value))
            {
                OnPropertyChanged(nameof(IsIndentSectionSplitEnabled));
            }
        }
    }

    private bool hideSubsplits;
    public bool HideSubsplits
    {
        get => hideSubsplits;
        set
        {
            if (SetField(ref hideSubsplits, value))
            {
                OnPropertyChanged(nameof(IsHideSubsplits));
                OnPropertyChanged(nameof(IsNormalSubsplits));
                OnPropertyChanged(nameof(IsShowSubsplits));
                OnPropertyChanged(nameof(IsCurrentSectionOnlyEnabled));
                OnPropertyChanged(nameof(IsIndentSubsplitsEnabled));
                OnPropertyChanged(nameof(IsIndentSectionSplitEnabled));
                OnPropertyChanged(nameof(IsMinimumMajorSplitsEnabled));
            }
        }
    }

    private bool showSubsplits;
    public bool ShowSubsplits
    {
        get => showSubsplits;
        set
        {
            if (SetField(ref showSubsplits, value))
            {
                OnPropertyChanged(nameof(IsHideSubsplits));
                OnPropertyChanged(nameof(IsNormalSubsplits));
                OnPropertyChanged(nameof(IsShowSubsplits));
                OnPropertyChanged(nameof(IsCurrentSectionOnlyEnabled));
                OnPropertyChanged(nameof(IsIndentSubsplitsEnabled));
                OnPropertyChanged(nameof(IsIndentSectionSplitEnabled));
                OnPropertyChanged(nameof(IsMinimumMajorSplitsEnabled));
            }
        }
    }

    private bool currentSectionOnly;
    public bool CurrentSectionOnly
    {
        get => currentSectionOnly;
        set => SetField(ref currentSectionOnly, value);
    }

    private bool overrideSubsplitColor;
    public bool OverrideSubsplitColor
    {
        get => overrideSubsplitColor;
        set
        {
            if (SetField(ref overrideSubsplitColor, value))
            {
                OnPropertyChanged(nameof(IsSubsplitColorEnabled));
            }
        }
    }

    private Color subsplitTopColor;
    public Color SubsplitTopColor
    {
        get => subsplitTopColor;
        set
        {
            if (SetField(ref subsplitTopColor, value))
            {
                OnPropertyChanged(nameof(SubsplitBottomSwatchColor));
            }
        }
    }

    private Color subsplitBottomColor;
    public Color SubsplitBottomColor
    {
        get => subsplitBottomColor;
        set
        {
            if (SetField(ref subsplitBottomColor, value))
            {
                OnPropertyChanged(nameof(SubsplitBottomSwatchColor));
            }
        }
    }

    private GradientType subsplitGradient;
    public GradientType SubsplitGradient
    {
        get => subsplitGradient;
        set
        {
            if (SetField(ref subsplitGradient, value))
            {
                OnPropertyChanged(nameof(SubsplitGradientString));
                OnPropertyChanged(nameof(IsSubsplitTopColorVisible));
                OnPropertyChanged(nameof(SubsplitBottomSwatchColor));
            }
        }
    }

    private bool showHeader;
    public bool ShowHeader
    {
        get => showHeader;
        set
        {
            if (SetField(ref showHeader, value))
            {
                OnPropertyChanged(nameof(IsHeaderOptionsEnabled));
                OnPropertyChanged(nameof(IsHeaderTextColorEnabled));
                OnPropertyChanged(nameof(IsSectionTimerOptionsEnabled));
            }
        }
    }

    private bool indentSectionSplit;
    public bool IndentSectionSplit
    {
        get => indentSectionSplit;
        set => SetField(ref indentSectionSplit, value);
    }

    private bool showIconSectionSplit;
    public bool ShowIconSectionSplit
    {
        get => showIconSectionSplit;
        set => SetField(ref showIconSectionSplit, value);
    }

    private bool showSectionIcon;
    public bool ShowSectionIcon
    {
        get => showSectionIcon;
        set => SetField(ref showSectionIcon, value);
    }

    private Color headerTopColor;
    public Color HeaderTopColor
    {
        get => headerTopColor;
        set
        {
            if (SetField(ref headerTopColor, value))
            {
                OnPropertyChanged(nameof(HeaderBottomSwatchColor));
            }
        }
    }

    private Color headerBottomColor;
    public Color HeaderBottomColor
    {
        get => headerBottomColor;
        set
        {
            if (SetField(ref headerBottomColor, value))
            {
                OnPropertyChanged(nameof(HeaderBottomSwatchColor));
            }
        }
    }

    private GradientType headerGradient;
    public GradientType HeaderGradient
    {
        get => headerGradient;
        set
        {
            if (SetField(ref headerGradient, value))
            {
                OnPropertyChanged(nameof(HeaderGradientString));
                OnPropertyChanged(nameof(IsHeaderTopColorVisible));
                OnPropertyChanged(nameof(HeaderBottomSwatchColor));
            }
        }
    }

    private bool overrideHeaderColor;
    public bool OverrideHeaderColor
    {
        get => overrideHeaderColor;
        set
        {
            if (SetField(ref overrideHeaderColor, value))
            {
                OnPropertyChanged(nameof(IsHeaderTextColorEnabled));
            }
        }
    }

    private Color headerTextColor;
    public Color HeaderTextColor
    {
        get => headerTextColor;
        set => SetField(ref headerTextColor, value);
    }

    private bool headerText;
    public bool HeaderText
    {
        get => headerText;
        set => SetField(ref headerText, value);
    }

    private Color headerTimesColor;
    public Color HeaderTimesColor
    {
        get => headerTimesColor;
        set => SetField(ref headerTimesColor, value);
    }

    private bool headerTimes;
    public bool HeaderTimes
    {
        get => headerTimes;
        set => SetField(ref headerTimes, value);
    }

    private TimeAccuracy headerAccuracy;
    public TimeAccuracy HeaderAccuracy
    {
        get => headerAccuracy;
        set
        {
            if (SetField(ref headerAccuracy, value))
            {
                OnPropertyChanged(nameof(IsHeaderAccuracySeconds));
                OnPropertyChanged(nameof(IsHeaderAccuracyTenths));
                OnPropertyChanged(nameof(IsHeaderAccuracyHundredths));
                OnPropertyChanged(nameof(IsHeaderAccuracyMilliseconds));
            }
        }
    }

    private bool sectionTimer;
    public bool SectionTimer
    {
        get => sectionTimer;
        set
        {
            if (SetField(ref sectionTimer, value))
            {
                OnPropertyChanged(nameof(IsSectionTimerOptionsEnabled));
            }
        }
    }

    private Color sectionTimerColor;
    public Color SectionTimerColor
    {
        get => sectionTimerColor;
        set => SetField(ref sectionTimerColor, value);
    }

    private bool sectionTimerGradient;
    public bool SectionTimerGradient
    {
        get => sectionTimerGradient;
        set => SetField(ref sectionTimerGradient, value);
    }

    private TimeAccuracy sectionTimerAccuracy;
    public TimeAccuracy SectionTimerAccuracy
    {
        get => sectionTimerAccuracy;
        set
        {
            if (SetField(ref sectionTimerAccuracy, value))
            {
                OnPropertyChanged(nameof(IsSectionTimerAccuracySeconds));
                OnPropertyChanged(nameof(IsSectionTimerAccuracyTenths));
                OnPropertyChanged(nameof(IsSectionTimerAccuracyHundredths));
                OnPropertyChanged(nameof(IsSectionTimerAccuracyMilliseconds));
            }
        }
    }

    public static ISegment HilightSplit { get; set; }
    public static ISegment SectionSplit { get; set; }

    public string SubsplitGradientString
    {
        get => SubsplitGradient.ToString();
        set => SubsplitGradient = Enum.Parse<GradientType>(value);
    }
    public bool IsSubsplitTopColorVisible => SubsplitGradient != GradientType.Plain;
    public Color SubsplitBottomSwatchColor
    {
        get => IsSubsplitTopColorVisible ? SubsplitBottomColor : SubsplitTopColor;
        set { if (IsSubsplitTopColorVisible) SubsplitBottomColor = value; else SubsplitTopColor = value; }
    }

    public string HeaderGradientString
    {
        get => HeaderGradient.ToString();
        set => HeaderGradient = Enum.Parse<GradientType>(value);
    }
    public bool IsHeaderTopColorVisible => HeaderGradient != GradientType.Plain;
    public Color HeaderBottomSwatchColor
    {
        get => IsHeaderTopColorVisible ? HeaderBottomColor : HeaderTopColor;
        set { if (IsHeaderTopColorVisible) HeaderBottomColor = value; else HeaderTopColor = value; }
    }

    public bool IsHeaderAccuracySeconds { get => HeaderAccuracy == TimeAccuracy.Seconds; set { if (value) HeaderAccuracy = TimeAccuracy.Seconds; } }

    public bool IsHeaderAccuracyTenths { get => HeaderAccuracy == TimeAccuracy.Tenths; set { if (value) HeaderAccuracy = TimeAccuracy.Tenths; } }

    public bool IsHeaderAccuracyHundredths { get => HeaderAccuracy == TimeAccuracy.Hundredths; set { if (value) HeaderAccuracy = TimeAccuracy.Hundredths; } }

    public bool IsHeaderAccuracyMilliseconds { get => HeaderAccuracy == TimeAccuracy.Milliseconds; set { if (value) HeaderAccuracy = TimeAccuracy.Milliseconds; } }

    public bool IsSectionTimerAccuracySeconds { get => SectionTimerAccuracy == TimeAccuracy.Seconds; set { if (value) SectionTimerAccuracy = TimeAccuracy.Seconds; } }

    public bool IsSectionTimerAccuracyTenths { get => SectionTimerAccuracy == TimeAccuracy.Tenths; set { if (value) SectionTimerAccuracy = TimeAccuracy.Tenths; } }

    public bool IsSectionTimerAccuracyHundredths { get => SectionTimerAccuracy == TimeAccuracy.Hundredths; set { if (value) SectionTimerAccuracy = TimeAccuracy.Hundredths; } }

    public bool IsSectionTimerAccuracyMilliseconds { get => SectionTimerAccuracy == TimeAccuracy.Milliseconds; set { if (value) SectionTimerAccuracy = TimeAccuracy.Milliseconds; } }

    public bool IsHideSubsplits { get => HideSubsplits && !ShowSubsplits; set { if (value) { HideSubsplits = true; ShowSubsplits = false; } } }
    public bool IsShowSubsplits { get => ShowSubsplits && !HideSubsplits; set { if (value) { ShowSubsplits = true; HideSubsplits = false; CurrentSectionOnly = false; } } }
    public bool IsNormalSubsplits { get => !ShowSubsplits && !HideSubsplits; set { if (value) { ShowSubsplits = false; HideSubsplits = false; } } }
    public bool IsCurrentSectionOnlyEnabled => !ShowSubsplits;
    public bool IsIndentSubsplitsEnabled => !HideSubsplits;
    public bool IsIndentSectionSplitEnabled => IsNormalSubsplits && IndentSubsplits;
    public bool IsMinimumMajorSplitsEnabled => IsNormalSubsplits;
    public bool IsSubsplitColorEnabled => OverrideSubsplitColor;
    public bool IsHeaderOptionsEnabled => ShowHeader;
    public bool IsHeaderTextColorEnabled => ShowHeader && OverrideHeaderColor;
    public bool IsSectionTimerOptionsEnabled => ShowHeader && SectionTimer;
    public static readonly string[] HeaderTimingMethodOptions = ["Current Timing Method", "Real Time", "Game Time"];
    public IReadOnlyList<string> HeaderComparisonOptions => new[] { "Current Comparison" }
        .Concat(CurrentState?.Run?.Comparisons.Where(x => x != NoneComparisonGenerator.ComparisonName) ?? [])
        .Append(HeaderComparison).Distinct().ToArray();

    public void SetSettings(XmlNode node)
    {
        loading = true;
        try { SetSettingsCore(node); }
        finally { loading = false; }
    }

    private void SetSettingsCore(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);

        AutomaticAbbreviation = SettingsHelper.ParseBool(element["AutomaticAbbreviation"], false);
        CurrentSplitTopColor = SettingsHelper.ParseColor(element["CurrentSplitTopColor"], Color.FromArgb(51, 115, 244));
        CurrentSplitBottomColor = SettingsHelper.ParseColor(element["CurrentSplitBottomColor"], Color.FromArgb(21, 53, 116));
        VisualSplitCount = SettingsHelper.ParseInt(element["VisualSplitCount"], 8);
        SplitPreviewCount = SettingsHelper.ParseInt(element["SplitPreviewCount"], 1);
        MinimumMajorSplits = SettingsHelper.ParseInt(element["MinimumMajorSplits"], 0);
        DisplayIcons = SettingsHelper.ParseBool(element["DisplayIcons"], true);
        ShowThinSeparators = SettingsHelper.ParseBool(element["ShowThinSeparators"], false);
        AlwaysShowLastSplit = SettingsHelper.ParseBool(element["AlwaysShowLastSplit"], true);
        SplitWidth = SettingsHelper.ParseFloat(element["SplitWidth"], 20);
        IndentBlankIcons = SettingsHelper.ParseBool(element["IndentBlankIcons"], true);
        IndentSubsplits = SettingsHelper.ParseBool(element["IndentSubsplits"], true);
        HideSubsplits = SettingsHelper.ParseBool(element["HideSubsplits"], false);
        ShowSubsplits = SettingsHelper.ParseBool(element["ShowSubsplits"], false);
        CurrentSectionOnly = SettingsHelper.ParseBool(element["CurrentSectionOnly"], false);
        OverrideSubsplitColor = SettingsHelper.ParseBool(element["OverrideSubsplitColor"], false);
        SubsplitTopColor = SettingsHelper.ParseColor(element["SubsplitTopColor"], Color.FromArgb(0x8D, 0x00, 0x00, 0x00));
        SubsplitBottomColor = SettingsHelper.ParseColor(element["SubsplitBottomColor"], Color.Transparent);
        SubsplitGradientString = SettingsHelper.ParseString(element["SubsplitGradient"], GradientType.Plain.ToString());
        ShowHeader = SettingsHelper.ParseBool(element["ShowHeader"], true);
        IndentSectionSplit = SettingsHelper.ParseBool(element["IndentSectionSplit"], true);
        ShowIconSectionSplit = SettingsHelper.ParseBool(element["ShowIconSectionSplit"], true);
        ShowSectionIcon = SettingsHelper.ParseBool(element["ShowSectionIcon"], true);
        HeaderTopColor = SettingsHelper.ParseColor(element["HeaderTopColor"], Color.FromArgb(0x2B, 0xFF, 0xFF, 0xFF));
        HeaderBottomColor = SettingsHelper.ParseColor(element["HeaderBottomColor"], Color.FromArgb(0xD8, 0x00, 0x00, 0x00));
        HeaderGradientString = SettingsHelper.ParseString(element["HeaderGradient"], GradientType.Vertical.ToString());
        OverrideHeaderColor = SettingsHelper.ParseBool(element["OverrideHeaderColor"], false);
        HeaderTextColor = SettingsHelper.ParseColor(element["HeaderTextColor"], Color.FromArgb(255, 255, 255));
        HeaderText = SettingsHelper.ParseBool(element["HeaderText"], true);
        HeaderTimesColor = SettingsHelper.ParseColor(element["HeaderTimesColor"], Color.FromArgb(255, 255, 255));
        HeaderTimes = SettingsHelper.ParseBool(element["HeaderTimes"], true);
        HeaderAccuracy = SettingsHelper.ParseEnum(element["HeaderAccuracy"], TimeAccuracy.Tenths);
        SectionTimer = SettingsHelper.ParseBool(element["SectionTimer"], true);
        SectionTimerColor = SettingsHelper.ParseColor(element["SectionTimerColor"], Color.FromArgb(0x77, 0x77, 0x77));
        SectionTimerGradient = SettingsHelper.ParseBool(element["SectionTimerGradient"], true);
        SectionTimerAccuracy = SettingsHelper.ParseEnum(element["SectionTimerAccuracy"], TimeAccuracy.Tenths);
        OverrideTimesColor = SettingsHelper.ParseBool(element["OverrideTimesColor"], false);
        BeforeTimesColor = SettingsHelper.ParseColor(element["BeforeTimesColor"], Color.FromArgb(255, 255, 255));
        CurrentTimesColor = SettingsHelper.ParseColor(element["CurrentTimesColor"], Color.FromArgb(255, 255, 255));
        AfterTimesColor = SettingsHelper.ParseColor(element["AfterTimesColor"], Color.FromArgb(255, 255, 255));
        SplitHeight = SettingsHelper.ParseFloat(element["SplitHeight"], 3.6f);
        SplitGradientString = SettingsHelper.ParseString(element["CurrentSplitGradient"], GradientType.Vertical.ToString());
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"], Color.Transparent);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"], Color.FromArgb(1, 255, 255, 255));
        GradientString = SettingsHelper.ParseString(element["BackgroundGradient"], ExtendedGradientType.Alternating.ToString());
        SeparatorLastSplit = SettingsHelper.ParseBool(element["SeparatorLastSplit"], true);
        DropDecimals = SettingsHelper.ParseBool(element["DropDecimals"], true);
        DeltasAccuracy = SettingsHelper.ParseEnum(element["DeltasAccuracy"], TimeAccuracy.Tenths);
        OverrideDeltasColor = SettingsHelper.ParseBool(element["OverrideDeltasColor"], false);
        DeltasColor = SettingsHelper.ParseColor(element["DeltasColor"], Color.FromArgb(255, 255, 255));
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"], false);
        SplitTimesAccuracy = SettingsHelper.ParseEnum(element["SplitTimesAccuracy"], TimeAccuracy.Seconds);
        LockLastSplit = SettingsHelper.ParseBool(element["LockLastSplit"], true);
        IconSize = SettingsHelper.ParseFloat(element["IconSize"], 24f);
        IconShadows = SettingsHelper.ParseBool(element["IconShadows"], true);
        ShowColumnLabels = SettingsHelper.ParseBool(element["ShowColumnLabels"], false);
        LabelsColor = SettingsHelper.ParseColor(element["LabelsColor"], Color.FromArgb(255, 255, 255));

        if (version >= new Version(1, 7))
        {
            HeaderComparison = SettingsHelper.ParseString(element["HeaderComparison"], "Current Comparison");
            HeaderTimingMethod = SettingsHelper.ParseString(element["HeaderTimingMethod"], "Current Timing Method");
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
            HeaderComparison = SettingsHelper.ParseString(element["Comparison"], "Current Comparison");
            HeaderTimingMethod = "Current Timing Method";
            ColumnsList.Clear();
            if (SettingsHelper.ParseBool(element["ShowSplitTimes"]))
            {
                ColumnsList.Add(new ColumnSettings(CurrentState, "+/-", ColumnsList) { Data = new ColumnData("+/-", ColumnType.Delta, HeaderComparison, "Current Timing Method") });
                ColumnsList.Add(new ColumnSettings(CurrentState, "Time", ColumnsList) { Data = new ColumnData("Time", ColumnType.SplitTime, HeaderComparison, "Current Timing Method") });
            }
            else
            {
                ColumnsList.Add(new ColumnSettings(CurrentState, "+/-", ColumnsList) { Data = new ColumnData("+/-", ColumnType.DeltaorSplitTime, HeaderComparison, "Current Timing Method") });
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
        int hashCode = SettingsHelper.CreateSetting(document, parent, "Version", "1.7") ^
        SettingsHelper.CreateSetting(document, parent, "AutomaticAbbreviation", AutomaticAbbreviation) ^
        SettingsHelper.CreateSetting(document, parent, "CurrentSplitTopColor", CurrentSplitTopColor) ^
        SettingsHelper.CreateSetting(document, parent, "CurrentSplitBottomColor", CurrentSplitBottomColor) ^
        SettingsHelper.CreateSetting(document, parent, "VisualSplitCount", VisualSplitCount) ^
        SettingsHelper.CreateSetting(document, parent, "SplitPreviewCount", SplitPreviewCount) ^
        SettingsHelper.CreateSetting(document, parent, "MinimumMajorSplits", MinimumMajorSplits) ^
        SettingsHelper.CreateSetting(document, parent, "DisplayIcons", DisplayIcons) ^
        SettingsHelper.CreateSetting(document, parent, "ShowThinSeparators", ShowThinSeparators) ^
        SettingsHelper.CreateSetting(document, parent, "AlwaysShowLastSplit", AlwaysShowLastSplit) ^
        SettingsHelper.CreateSetting(document, parent, "SplitWidth", SplitWidth) ^
        SettingsHelper.CreateSetting(document, parent, "SplitTimesAccuracy", SplitTimesAccuracy) ^
        SettingsHelper.CreateSetting(document, parent, "BeforeNamesColor", BeforeNamesColor) ^
        SettingsHelper.CreateSetting(document, parent, "CurrentNamesColor", CurrentNamesColor) ^
        SettingsHelper.CreateSetting(document, parent, "AfterNamesColor", AfterNamesColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
        SettingsHelper.CreateSetting(document, parent, "BeforeTimesColor", BeforeTimesColor) ^
        SettingsHelper.CreateSetting(document, parent, "CurrentTimesColor", CurrentTimesColor) ^
        SettingsHelper.CreateSetting(document, parent, "AfterTimesColor", AfterTimesColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTimesColor", OverrideTimesColor) ^
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
        SettingsHelper.CreateSetting(document, parent, "HeaderComparison", HeaderComparison) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderTimingMethod", HeaderTimingMethod) ^
        SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
        SettingsHelper.CreateSetting(document, parent, "IndentBlankIcons", IndentBlankIcons) ^
        SettingsHelper.CreateSetting(document, parent, "IndentSubsplits", IndentSubsplits) ^
        SettingsHelper.CreateSetting(document, parent, "HideSubsplits", HideSubsplits) ^
        SettingsHelper.CreateSetting(document, parent, "ShowSubsplits", ShowSubsplits) ^
        SettingsHelper.CreateSetting(document, parent, "CurrentSectionOnly", CurrentSectionOnly) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideSubsplitColor", OverrideSubsplitColor) ^
        SettingsHelper.CreateSetting(document, parent, "SubsplitGradient", SubsplitGradient) ^
        SettingsHelper.CreateSetting(document, parent, "ShowHeader", ShowHeader) ^
        SettingsHelper.CreateSetting(document, parent, "IndentSectionSplit", IndentSectionSplit) ^
        SettingsHelper.CreateSetting(document, parent, "ShowIconSectionSplit", ShowIconSectionSplit) ^
        SettingsHelper.CreateSetting(document, parent, "ShowSectionIcon", ShowSectionIcon) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderGradient", HeaderGradient) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideHeaderColor", OverrideHeaderColor) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderText", HeaderText) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderTimes", HeaderTimes) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderAccuracy", HeaderAccuracy) ^
        SettingsHelper.CreateSetting(document, parent, "SectionTimer", SectionTimer) ^
        SettingsHelper.CreateSetting(document, parent, "SectionTimerGradient", SectionTimerGradient) ^
        SettingsHelper.CreateSetting(document, parent, "SectionTimerAccuracy", SectionTimerAccuracy) ^
        SettingsHelper.CreateSetting(document, parent, "SubsplitTopColor", SubsplitTopColor) ^
        SettingsHelper.CreateSetting(document, parent, "SubsplitBottomColor", SubsplitBottomColor) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderTopColor", HeaderTopColor) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderBottomColor", HeaderBottomColor) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderTextColor", HeaderTextColor) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderTimesColor", HeaderTimesColor) ^
        SettingsHelper.CreateSetting(document, parent, "SectionTimerColor", SectionTimerColor) ^
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
