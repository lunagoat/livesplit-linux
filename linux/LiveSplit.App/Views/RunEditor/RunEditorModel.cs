using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.TimeFormatters;
using LiveSplit.UI;
using LiveSplit.Web.SRL;
using System;
using System.Collections.Generic;
using System.Linq;
using Image = System.Drawing.Image;

namespace LiveSplit.View.RunEditor;

public sealed class RunEditorModel
{
    public IRun Run { get; }
    public bool AllowChangingSegments { get; }
    public TimingMethod Method { get; set; }
    public int? AttemptIndex { get; set; }
    public List<Image> ImagesToDispose { get; } = [];
    public event Action Edited;
    public event Action SegmentsChanged;
    public event Action<RenameEventArgs> ComparisonRenamed;
    private readonly ITimeFormatter formatter = new ShortTimeFormatterMilliseconds();

    public RunEditorModel(IRun run, bool allowChangingSegments)
    {
        Run = run;
        AllowChangingSegments = allowChangingSegments;
    }

    public void Changed(bool clearAssociation = false)
    {
        if (clearAssociation) Run.Metadata.RunID = null;
        Run.HasChanged = true;
        Edited?.Invoke();
    }

    public static bool TryTime(string text, out TimeSpan? value, bool allowNegative = false)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        try
        {
            value = TimeSpanParser.Parse(text);
            return allowNegative || value >= TimeSpan.Zero;
        }
        catch { return false; }
    }

    public string Format(TimeSpan? value) => value.HasValue ? formatter.Format(value) : "";

    public IList<TimeSpan?> SegmentTimes()
    {
        if (AttemptIndex is int attempt) return HistoryTimeCalculator.GetSegmentTimesForAttempt(Run, attempt, Method);
        TimeSpan previous = TimeSpan.Zero;
        return Run.Select(segment =>
        {
            TimeSpan? current = segment.PersonalBestSplitTime[Method];
            TimeSpan? result = current - previous;
            if (current.HasValue) previous = current.Value;
            return result;
        }).ToList();
    }

    public string Cell(int row, int column, string comparison = null)
    {
        ISegment segment = Run[row];
        if (column == 1) return segment.Name;
        if (AttemptIndex is int attempt)
        {
            return Format(column switch
            {
                2 => HistoryTimeCalculator.GetSplitTimesForAttempt(Run, attempt, Method)[row],
                3 => SegmentTimes()[row],
                4 => HistoryTimeCalculator.GetSegmentBestDiffsForAttempt(Run, attempt, Method)[row],
                _ => null,
            });
        }
        return Format(column switch
        {
            2 => segment.PersonalBestSplitTime[Method],
            3 => SegmentTimes()[row],
            4 => segment.BestSegmentTime[Method],
            _ => segment.Comparisons[comparison][Method],
        });
    }

    public bool EditCell(int row, int column, string text, string comparison = null, bool fix = true)
    {
        if (row < 0 || row >= Run.Count) return false;
        ISegment segment = Run[row];
        if (column == 1)
        {
            if (AttemptIndex.HasValue) return false;
            segment.Name = text;
            Changed();
            return true;
        }
        if (!TryTime(text, out TimeSpan? value)) return false;
        if (AttemptIndex is int attempt)
        {
            if (column is not (2 or 3)) return false;
            if (column == 2 && value.HasValue)
            {
                TimeSpan previous = row > 0 ? HistoryTimeCalculator.GetSplitTimesForAttempt(Run, attempt, Method)[row - 1] ?? TimeSpan.Zero : TimeSpan.Zero;
                value = value < previous ? TimeSpan.Zero : value - previous;
            }
            segment.SegmentHistory.TryGetValue(attempt, out Time history);
            history[Method] = value;
            segment.SegmentHistory[attempt] = history;
            if (fix) Run.FixSplits();
            Changed();
            return true;
        }
        Time previousPb = Run[^1].PersonalBestSplitTime;
        if (column == 3)
        {
            IList<TimeSpan?> times = SegmentTimes();
            times[row] = value;
            TimeSpan total = TimeSpan.Zero;
            for (int i = 0; i < Run.Count; i++)
            {
                Time time = Run[i].PersonalBestSplitTime;
                time[Method] = total + times[i];
                Run[i].PersonalBestSplitTime = time;
                if (time[Method].HasValue) total = time[Method].Value;
            }
        }
        else
        {
            Time time = column == 4 ? segment.BestSegmentTime : segment.Comparisons[column == 2 ? Model.Run.PersonalBestComparisonName : comparison];
            time[Method] = value;
            if (column == 4) segment.BestSegmentTime = time;
            else segment.Comparisons[column == 2 ? Model.Run.PersonalBestComparisonName : comparison] = time;
        }
        if (fix) Run.FixSplits();
        Changed(previousPb.RealTime != Run[^1].PersonalBestSplitTime.RealTime || previousPb.GameTime != Run[^1].PersonalBestSplitTime.GameTime);
        return true;
    }

    public void SetIcon(int row, Image image)
    {
        Image old = row < 0 ? Run.GameIcon : Run[row].Icon;
        if (old != null && !ImagesToDispose.Contains(old)) ImagesToDispose.Add(old);
        if (image != null && !ImagesToDispose.Contains(image)) ImagesToDispose.Add(image);
        if (row < 0) Run.GameIcon = image;
        else Run[row].Icon = image;
        Changed();
    }

    public void KeepCurrentImages()
    {
        ImagesToDispose.Remove(Run.GameIcon);
        foreach (ISegment segment in Run) ImagesToDispose.Remove(segment.Icon);
    }

    public void Insert(int index)
    {
        if (!AllowChangingSegments || AttemptIndex.HasValue) return;
        index = Math.Clamp(index, 0, Run.Count);
        var segment = new Segment("");
        if (index < Run.Count) Run.ImportBestSegment(index);
        if (Run.Count > 0)
        {
            int max = Run.AttemptHistory.Select(x => x.Index).DefaultIfEmpty(0).Max();
            for (int id = Run.GetMinSegmentHistoryIndex(); id <= max; id++) segment.SegmentHistory[id] = default;
        }
        Run.Insert(index, segment);
        Run.FixSplits();
        Changed();
        SegmentsChanged?.Invoke();
    }

    public void Remove(IEnumerable<int> indices)
    {
        if (!AllowChangingSegments || AttemptIndex.HasValue) return;
        foreach (int index in indices.Distinct().OrderDescending())
        {
            if (Run.Count <= 1 || index < 0 || index >= Run.Count) continue;
            foreach (TimingMethod method in Enum.GetValues<TimingMethod>()) MergeDeletedHistory(index, method);
            SetIcon(index, null);
            Run.RemoveAt(index);
        }
        Run.FixSplits();
        Changed(true);
        SegmentsChanged?.Invoke();
    }

    private void MergeDeletedHistory(int index, TimingMethod method)
    {
        if (index + 1 >= Run.Count) return;
        ISegment next = Run[index + 1];
        foreach (int id in Run.SelectMany(s => s.SegmentHistory.Keys).Distinct().ToArray())
        {
            if (!Run[index].SegmentHistory.TryGetValue(id, out Time removed))
            {
                next.SegmentHistory.Remove(id);
                continue;
            }
            for (int i = index + 1; removed[method].HasValue && i < Run.Count; i++)
            {
                if (Run[i].SegmentHistory.TryGetValue(id, out Time time) && time[method].HasValue)
                {
                    time[method] += removed[method];
                    Run[i].SegmentHistory[id] = time;
                    break;
                }
            }
        }
        TimeSpan? best = Run[index].BestSegmentTime[method] + next.BestSegmentTime[method];
        foreach (Time time in next.SegmentHistory.Values)
            if (time[method] < best) best = time[method];
        Time updated = next.BestSegmentTime;
        updated[method] = best;
        next.BestSegmentTime = updated;
    }

    public void Move(int from, int to)
    {
        if (!AllowChangingSegments || AttemptIndex.HasValue || from < 0 || to < 0 || from >= Run.Count || to >= Run.Count) return;
        while (from != to)
        {
            int first = from < to ? from : from - 1;
            ISegment a = Run[first];
            ISegment b = Run[first + 1];
            foreach (int id in a.SegmentHistory.Keys.Intersect(b.SegmentHistory.Keys).ToArray())
            {
                Time x = a.SegmentHistory[id], y = b.SegmentHistory[id];
                if (x.RealTime.HasValue != y.RealTime.HasValue || x.GameTime.HasValue != y.GameTime.HasValue)
                {
                    a.SegmentHistory.Remove(id);
                    b.SegmentHistory.Remove(id);
                }
            }
            foreach (string comparison in Run.CustomComparisons)
            {
                Time previous = first > 0 ? Run[first - 1].Comparisons[comparison] : new Time(TimeSpan.Zero, TimeSpan.Zero);
                Time aTime = a.Comparisons[comparison];
                Time bTime = b.Comparisons[comparison];
                b.Comparisons[comparison] = previous + (bTime - aTime);
                a.Comparisons[comparison] = bTime;
            }
            Run[first] = b;
            Run[first + 1] = a;
            from += from < to ? 1 : -1;
        }
        Run.FixSplits();
        Changed(true);
        SegmentsChanged?.Invoke();
    }

    public void ValidateComparison(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || SRLComparisonGenerator.IsRaceComparison(name)) throw new ArgumentException("Enter a comparison name that does not start with [Race].");
        if (Run.Comparisons.Contains(name)) throw new ArgumentException("A comparison with this name already exists.");
    }

    public void AddComparison(string name, string clone = null)
    {
        ValidateComparison(name);
        Run.CustomComparisons.Add(name);
        foreach (ISegment segment in Run) segment.Comparisons[name] = clone == null ? default : segment.Comparisons[clone];
        Changed();
    }

    public void RenameComparison(string oldName, string newName)
    {
        if (oldName == newName) return;
        if (oldName == Model.Run.PersonalBestComparisonName || !Run.CustomComparisons.Contains(oldName)) throw new ArgumentException("Only custom comparisons can be renamed.");
        ValidateComparison(newName);
        Run.CustomComparisons[Run.CustomComparisons.IndexOf(oldName)] = newName;
        foreach (ISegment segment in Run)
        {
            segment.Comparisons[newName] = segment.Comparisons[oldName];
            segment.Comparisons.Remove(oldName);
        }
        ComparisonRenamed?.Invoke(new RenameEventArgs { OldName = oldName, NewName = newName });
        Changed();
    }

    public void RemoveComparison(string name)
    {
        if (name == Model.Run.PersonalBestComparisonName || !Run.CustomComparisons.Remove(name)) return;
        foreach (ISegment segment in Run) segment.Comparisons.Remove(name);
        ComparisonRenamed?.Invoke(new RenameEventArgs { OldName = name, NewName = "Current Comparison" });
        Changed();
    }

    public int Paste(int row, int column, string text)
    {
        string[] comparisons = Run.CustomComparisons.Where(c => c != Model.Run.PersonalBestComparisonName).ToArray();
        string[][] values = text.Replace("\r", "").TrimEnd('\n').Split('\n').Select(line => line.Split('\t')).ToArray();
        bool containsSplit = column <= 2 && values.Any(line => column + line.Length > 2);
        int invalid = 0;
        for (int r = 0; r < values.Length && row + r < Run.Count; r++)
        {
            for (int c = 0; c < values[r].Length; c++)
            {
                int target = column + c;
                if (target > comparisons.Length + 4 || target < 1 || (containsSplit && target == 3)) continue;
                if (!EditCell(row + r, target, values[r][c], target >= 5 ? comparisons[target - 5] : null, false)) invalid++;
            }
        }
        if (!AttemptIndex.HasValue) Run.FixSplits();
        Changed();
        return invalid;
    }

    public void DeleteAttempt(int id)
    {
        Run.AttemptHistory = Run.AttemptHistory.Where(a => a.Index != id).ToList();
        foreach (ISegment segment in Run) segment.SegmentHistory.Remove(id);
        AttemptIndex = null;
        Changed();
    }

    public void Group(IEnumerable<int> selection, string name)
    {
        int[] rows = selection.Distinct().Order().ToArray();
        if (!AllowChangingSegments || rows.Length == 0) return;
        if (rows[^1] - rows[0] + 1 != rows.Length) throw new ArgumentException("Select consecutive segments to form a group.");
        foreach (int row in rows) Run[row].Name = UngroupName(Run[row].Name);
        foreach (int row in rows.SkipLast(1)) Run[row].Name = "-" + Run[row].Name;
        Run[rows[^1]].Name = "{" + name + "} " + Run[rows[^1]].Name;
        Changed();
    }

    public static string UngroupName(string name)
    {
        name = name.TrimStart('-');
        return name.StartsWith('{') && name.Contains('}') ? name[(name.IndexOf('}') + 1)..].TrimStart() : name;
    }
}
