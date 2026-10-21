using LiveSplit.Localization;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.UI.Components;

/// <summary>
/// Model of one entry of the "Columns" list of the Splits settings (it used to be a user control per column).
/// Wraps a <see cref="ColumnData"/> (what the component renders from) and adds the view state: the comparison items,
/// the group title and the enabled state of the move buttons. A view binds one editor per instance.
/// </summary>
public class ColumnSettings : SettingsBase
{
    private static string T(string source) => UiLocalizer.Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());

    /// <summary>Items of the column type drop down (<see cref="Type"/>), in <see cref="ColumnType"/> order.</summary>
    public static readonly string[] ColumnTypeOptions = ["Delta", "Split Time", "Delta or Split Time", "Segment Delta", "Segment Time", "Segment Delta or Segment Time", "Custom Variable"];
    /// <summary>Items of the timing method drop down (<see cref="TimingMethod"/>).</summary>
    public static readonly string[] TimingMethodOptions = ["Current Timing Method", "Real Time", "Game Time"];

    private ColumnData data;
    private IReadOnlyList<string> comparisonOptions;
    private bool canMoveUp;
    private bool canMoveDown;

    public ColumnData Data
    {
        get => data;
        set
        {
            if (SetField(ref data, value))
            {
                OnPropertyChanged(nameof(ColumnName));
                OnPropertyChanged(nameof(Type));
                OnPropertyChanged(nameof(Comparison));
                OnPropertyChanged(nameof(TimingMethod));
                OnPropertyChanged(nameof(GroupTitle));
                comparisonOptions = BuildComparisonOptions();
                OnPropertyChanged(nameof(ComparisonOptions));
            }
        }
    }

    public string ColumnName
    {
        get => Data.Name;
        set
        {
            if (Data.Name != value)
            {
                Data.Name = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GroupTitle));
            }
        }
    }

    /// <summary>The column type as shown in the drop down (one of <see cref="ColumnTypeOptions"/>).</summary>
    public string Type
    {
        get => GetColumnType(Data.Type);
        set
        {
            ColumnType type = ParseColumnType(value);
            if (Data.Type != type)
            {
                Data.Type = type;
                OnPropertyChanged();
                UpdateComparisonItems();
            }
        }
    }

    /// <summary>"Current Comparison" or the name of a comparison (one of <see cref="ComparisonOptions"/>).</summary>
    public string Comparison
    {
        get => Data.Comparison;
        set
        {
            if (Data.Comparison != value)
            {
                Data.Comparison = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>One of <see cref="TimingMethodOptions"/>.</summary>
    public string TimingMethod
    {
        get => Data.TimingMethod;
        set
        {
            if (Data.TimingMethod != value)
            {
                Data.TimingMethod = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Items of the comparison drop down. Depends on <see cref="Type"/> and on the comparisons of the run.</summary>
    public IReadOnlyList<string> ComparisonOptions => comparisonOptions;

    /// <summary>Title of the group box around the column editor: "Column: &lt;name&gt;" (translated).</summary>
    public string GroupTitle => string.Format(T("Column: {0}"), ColumnName);

    public bool CanMoveUp { get => canMoveUp; private set => SetField(ref canMoveUp, value); }
    public bool CanMoveDown { get => canMoveDown; private set => SetField(ref canMoveDown, value); }

    protected LiveSplitState CurrentState { get; set; }
    protected IList<ColumnSettings> ColumnsList { get; set; }

    protected int ColumnIndex => ColumnsList.IndexOf(this);
    protected int TotalColumns => ColumnsList.Count;

    public event EventHandler ColumnRemoved;
    public event EventHandler MovedUp;
    public event EventHandler MovedDown;

    public ColumnSettings(LiveSplitState state, string columnName, IList<ColumnSettings> columnsList)
    {
        data = new ColumnData(columnName, ColumnType.Delta, "Current Comparison", "Current Timing Method");

        CurrentState = state;
        ColumnsList = columnsList;
        comparisonOptions = BuildComparisonOptions();
    }

    /// <summary>
    /// What the old control did when it was loaded into the settings page: translate the legacy "Time" name and rebuild
    /// the comparison items (which can reset an invalid comparison to "Current Comparison"). Called by
    /// <see cref="SplitsSettings.PrepareForDisplay"/>, so a view does not need to call it.
    /// </summary>
    public void Initialize()
    {
        if (string.Equals(ColumnName, "Time", StringComparison.Ordinal))
        {
            ColumnName = T("Time");
        }

        UpdateComparisonItems();
    }

    /// <summary>Recomputes <see cref="CanMoveUp"/> and <see cref="CanMoveDown"/> from the position in the column list.</summary>
    public void UpdateEnabledButtons()
    {
        CanMoveDown = ColumnIndex < TotalColumns - 1;
        CanMoveUp = ColumnIndex > 0;
    }

    /// <summary>"Remove Column" button.</summary>
    public void Remove()
    {
        ColumnRemoved?.Invoke(this, null);
    }

    /// <summary>"Move Up" button.</summary>
    public void MoveUp()
    {
        MovedUp?.Invoke(this, null);
    }

    /// <summary>"Move Down" button.</summary>
    public void MoveDown()
    {
        MovedDown?.Invoke(this, null);
    }

    private void UpdateComparisonItems()
    {
        if (!IsTypeWithBestSplitTimes(Data.Type) && Comparison == BestSplitTimesComparisonGenerator.ComparisonName)
        {
            Comparison = "Current Comparison";
        }

        comparisonOptions = BuildComparisonOptions();
        OnPropertyChanged(nameof(ComparisonOptions));
    }

    private static bool IsTypeWithBestSplitTimes(ColumnType type)
    {
        return type is ColumnType.Delta or ColumnType.DeltaorSplitTime or ColumnType.SplitTime;
    }

    private List<string> BuildComparisonOptions()
    {
        var items = new List<string> { "Current Comparison" };
        IEnumerable<string> comparisons = CurrentState?.Run?.Comparisons ?? [];

        if (IsTypeWithBestSplitTimes(Data.Type))
        {
            items.AddRange(comparisons.Where(x => x != NoneComparisonGenerator.ComparisonName));
        }
        else
        {
            items.AddRange(comparisons.Where(x => x is not BestSplitTimesComparisonGenerator.ComparisonName and not NoneComparisonGenerator.ComparisonName));
        }

        if (Comparison != null && !items.Contains(Comparison))
        {
            items.Add(Comparison);
        }

        return items;
    }

    private static string GetColumnType(ColumnType type)
    {
        return type switch
        {
            ColumnType.SplitTime => "Split Time",
            ColumnType.Delta => "Delta",
            ColumnType.DeltaorSplitTime => "Delta or Split Time",
            ColumnType.SegmentTime => "Segment Time",
            ColumnType.SegmentDelta => "Segment Delta",
            ColumnType.SegmentDeltaorSegmentTime => "Segment Delta or Segment Time",
            ColumnType.CustomVariable => "Custom Variable",
            _ => "Unknown",
        };
    }

    private static ColumnType ParseColumnType(string columnType)
    {
        return (ColumnType)Enum.Parse(typeof(ColumnType), columnType.Replace(" ", string.Empty));
    }
}
