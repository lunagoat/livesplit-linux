using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Services;
using LiveSplit.View.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace LiveSplit.View.RunEditor;

public sealed partial class RunEditorWindow
{
    private sealed record AttemptChoice(Attempt Attempt, string Text)
    {
        public override string ToString() => Text;
    }

    private void RefreshAttempts()
    {
        if (!historyBar.IsVisible) return;
        int? selected = Editor.AttemptIndex;
        IEnumerable<Attempt> query = Editor.Run.AttemptHistory.OrderByDescending(a => a.Index);
        if (completed.IsChecked == true) query = query.Where(a => a.Time[Editor.Method].HasValue);
        if (month.SelectedItem is string m && m != "All months") query = query.Where(a => a.Started?.Time.ToString("yyyy/MM") == m);
        Attempt[] all = query.ToArray();
        int pages = Math.Max(1, (all.Length + 49) / 50);
        page = Math.Clamp(page, 0, pages - 1);
        AttemptChoice[] choices = all.Skip(page * 50).Take(50).Select(a => new AttemptChoice(a, $"#{a.Index}   {a.Started?.Time.ToLocalTime():yyyy-MM-dd HH:mm}   {Editor.Format(a.Time[Editor.Method])}")).ToArray();
        refreshing = true;
        attempts.ItemsSource = choices;
        attempts.SelectedItem = choices.FirstOrDefault(a => a.Attempt.Index == selected) ?? choices.FirstOrDefault();
        Editor.AttemptIndex = (attempts.SelectedItem as AttemptChoice)?.Attempt.Index;
        pageLabel.Text = $"{page + 1} / {pages} ({all.Length})";
        refreshing = false;
        RefreshGrid();
    }

    private void EditAttempt()
    {
        if (Editor.AttemptIndex is not int id) return;
        int index = Editor.Run.AttemptHistory.ToList().FindIndex(a => a.Index == id);
        if (index < 0) return;
        Attempt original = Editor.Run.AttemptHistory[index];
        var dialog = new AttemptEditorWindow(original);
        if (UiThread.RunSync(() => dialog.ShowModalAsync(this)) != DialogResult.OK) return;
        Editor.Run.AttemptHistory[index] = dialog.Attempt;
        Editor.Changed(); RefreshAttempts();
    }

    private void ChooseComparisons()
    {
        var settings = request.State.Settings;
        var active = Editor.Run.ComparisonGenerators?.Select(g => g.Name).ToHashSet();
        var dialog = new ChooseComparisonsWindow(settings.ComparisonGeneratorStates, settings.HcpHistorySize, settings.HcpNBestRuns, active);
        if (UiThread.RunSync(() => dialog.ShowModalAsync(this)) != DialogResult.OK) return;
        settings.ComparisonGeneratorStates = dialog.States;
        settings.HcpHistorySize = dialog.HistorySize;
        settings.HcpNBestRuns = dialog.NBest;
        Editor.Run.ComparisonGenerators = new StandardComparisonGeneratorsFactory().GetAllGenerators(Editor.Run).Where(g => dialog.States.TryGetValue(g.Name, out bool enabled) && enabled).ToList();
        request.Host?.NotifyRunEdited();
        RefreshComparisons(); RefreshGrid();
    }
}

public sealed class AttemptEditorWindow : DialogWindow
{
    public Attempt Attempt { get; private set; }

    private static string FormatTime(TimeSpan? time)
    {
        if (!time.HasValue) return "";
        TimeSpan t = time.Value;
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss\.fff") : t.ToString(@"m\:ss\.fff");
    }

    public AttemptEditorWindow(Attempt attempt) : base("Edit attempt #" + attempt.Index)
    {
        Attempt = attempt;
        var body = new StackPanel { Spacing = 6, MinWidth = 360 };
        var real = new TextBox { Text = FormatTime(attempt.Time.RealTime) };
        var game = new TextBox { Text = FormatTime(attempt.Time.GameTime) };
        var pause = new TextBox { Text = FormatTime(attempt.PauseTime) };
        var start = new TextBox { Text = attempt.Started?.Time.ToString("O") ?? "" };
        var end = new TextBox { Text = attempt.Ended?.Time.ToString("O") ?? "" };
        var error = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = Avalonia.Media.Brushes.OrangeRed };
        foreach (var pair in new[] { ("Real time", real), ("Game time", game), ("Pause time", pause), ("Started (ISO date and time)", start), ("Ended (ISO date and time)", end) })
        { body.Children.Add(Label(pair.Item1)); body.Children.Add(pair.Item2); }
        body.Children.Add(error); SetBody(body);
        AddButton("OK", () =>
        {
            if (!RunEditorModel.TryTime(real.Text, out TimeSpan? rt) || !RunEditorModel.TryTime(game.Text, out TimeSpan? gt) || !RunEditorModel.TryTime(pause.Text, out TimeSpan? pt)) { error.Text = "Enter valid nonnegative times."; return; }
            if (!TryDate(start.Text, attempt.Started, out AtomicDateTime? started) || !TryDate(end.Text, attempt.Ended, out AtomicDateTime? ended)) { error.Text = "Enter valid dates, or leave them empty."; return; }
            if (started.HasValue && ended.HasValue && ended.Value.Time < started.Value.Time) { error.Text = "The end must follow the start."; return; }
            Attempt = new Attempt(attempt.Index, new Time(rt, gt), started, ended, pt);
            Close(DialogResult.OK);
        }, isDefault: true);
        AddButton("Cancel", DialogResult.Cancel, isCancel: true);
    }

    private static bool TryDate(string text, AtomicDateTime? original, out AtomicDateTime? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime date)) return false;
        value = new AtomicDateTime(date, original.HasValue && date == original.Value.Time && original.Value.SyncedWithAtomicClock);
        return true;
    }
}

public sealed class ChooseComparisonsWindow : DialogWindow
{
    public IDictionary<string, bool> States { get; }
    public int HistorySize => (int)(history.Value ?? 1);
    public int NBest => (int)(best.Value ?? 1);
    private readonly NumericUpDown history;
    private readonly NumericUpDown best;

    public ChooseComparisonsWindow(IDictionary<string, bool> states, int historySize, int nBest, ISet<string> activeGenerators = null) : base("Choose comparisons")
    {
        States = new Dictionary<string, bool>(states);
        var body = new StackPanel { Spacing = 6, MinWidth = 320 };
        string[] names = [BestSegmentsComparisonGenerator.ComparisonName, BestSplitTimesComparisonGenerator.ComparisonName, AverageSegmentsComparisonGenerator.ComparisonName, MedianSegmentsComparisonGenerator.ComparisonName, WorstSegmentsComparisonGenerator.ComparisonName, PercentileComparisonGenerator.ComparisonName, LatestRunComparisonGenerator.ComparisonName, HCPComparisonGenerator.ComparisonName, NoneComparisonGenerator.ComparisonName];
        var hcp = new StackPanel { Spacing = 5, IsVisible = states.TryGetValue(HCPComparisonGenerator.ComparisonName, out bool enabled) && enabled };
        foreach (string name in names)
        {
            bool isChecked = states.TryGetValue(name, out bool active) ? active : (activeGenerators?.Contains(name) == true);
            States[name] = isChecked;
            var check = new CheckBox { Content = name, IsChecked = isChecked };
            check.IsCheckedChanged += (_, _) => { States[name] = check.IsChecked == true; if (name == HCPComparisonGenerator.ComparisonName) hcp.IsVisible = check.IsChecked == true; };
            body.Children.Add(check);
        }
        history = new NumericUpDown { Minimum = 1, Maximum = int.MaxValue, Value = Math.Max(1, historySize), FormatString = "0" };
        best = new NumericUpDown { Minimum = 1, Maximum = int.MaxValue, Value = Math.Max(1, nBest), FormatString = "0" };
        history.ValueChanged += (_, _) => { if (best.Value > history.Value) best.Value = history.Value; };
        best.ValueChanged += (_, _) => { if (history.Value < best.Value) history.Value = best.Value; };
        hcp.Children.Add(Label("HCP history size")); hcp.Children.Add(history);
        hcp.Children.Add(Label("HCP best runs")); hcp.Children.Add(best);
        body.Children.Add(hcp); SetBody(body);
        AddButton("OK", DialogResult.OK, isDefault: true); AddButton("Cancel", DialogResult.Cancel, isCancel: true);
    }
}
