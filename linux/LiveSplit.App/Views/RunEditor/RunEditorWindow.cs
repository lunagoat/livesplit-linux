using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LiveSplit.Hosting;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Services;
using LiveSplit.UI;
using LiveSplit.View.Dialogs;
using LiveSplit.Web;
using LiveSplit.Web.Share;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Image = System.Drawing.Image;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;

namespace LiveSplit.View.RunEditor;

public sealed partial class RunEditorWindow : DialogWindow
{
    public RunEditorModel Editor { get; }
    private readonly RunEditorRequest request;
    private readonly TabControl tabs = new();
    private readonly StackPanel rows = new() { Spacing = 1 };
    private readonly Grid headings = new();
    private readonly StackPanel metadata = new() { Spacing = 10, Margin = new Thickness(12) };
    private readonly StackPanel autoSplitter = new() { Spacing = 12, Margin = new Thickness(12) };
    private readonly ComboBox comparison = new() { MinWidth = 150 };
    private readonly Button btnRenameComp;
    private readonly Button btnRemoveComp;
    private readonly Button gameIconBtn;
    private readonly ComboBox attempts = new() { MinWidth = 300 };
    private readonly ComboBox month = new() { MinWidth = 110 };
    private readonly CheckBox completed = new() { Content = "Completed only" };
    private readonly TextBlock pageLabel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6) };
    private readonly HashSet<int> selection = [];
    private readonly List<Bitmap> previews = [];
    private readonly List<Border> rowBorders = [];
    private readonly List<Button> structuralButtons = [];
    private readonly Dictionary<(int row, int col), TextBox> cellBoxes = [];
    private readonly AutoCompleteBox game;
    private readonly AutoCompleteBox category;
    private readonly StackPanel historyBar;
    private Bitmap gameIconPreview;
    private int currentRow;
    private int currentColumn = 1;
    private int page;
    private bool refreshing;
    private bool closed;
    private int lookupVersion;
    private int dragRow = -1;
    private Point dragStart;
    private readonly bool loadOnline;

    public RunEditorWindow(RunEditorRequest request, bool loadOnline = true) : base("Edit Splits")
    {
        this.request = request;
        this.loadOnline = loadOnline;
        Editor = new RunEditorModel(request.State.Run, request.AllowChangingSegments) { Method = request.State.CurrentTimingMethod };
        Editor.Edited += () => request.Host?.NotifyRunEdited();
        Editor.SegmentsChanged += () => request.Host?.NotifySegmentRemovedOrAdded();
        Editor.ComparisonRenamed += args =>
        {
            if (request.State.CurrentComparison == args.OldName)
                request.State.CurrentComparison = args.NewName == "Current Comparison" ? Model.Run.PersonalBestComparisonName : args.NewName;
            request.Host?.NotifyComparisonRenamed(args);
        };
        Width = 1100;
        Height = 760;
        MinWidth = 820;
        MinHeight = 520;
        CanResize = true;
        SizeToContent = SizeToContent.Manual;

        gameIconBtn = new Button
        {
            Width = 56,
            Height = 56,
            Padding = new Thickness(2),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.Parse("#1e232a")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3a4350")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
        };
        ToolTip.SetTip(gameIconBtn, "Game icon (click to set, right-click for options)");
        gameIconBtn.Click += (_, _) => ImportIcon(-1);
        SetupGameIconContextMenu();

        game = new AutoCompleteBox { Text = Editor.Run.GameName, MinWidth = 220, FilterMode = AutoCompleteFilterMode.Contains, MinimumPrefixLength = 2 };
        category = new AutoCompleteBox { Text = Editor.Run.CategoryName, MinWidth = 170, ItemsSource = new[] { "Any%", "100%", "Low%" } };
        game.LostFocus += async (_, _) => await CommitNamesAsync();
        category.LostFocus += async (_, _) => await CommitNamesAsync();

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,*,130,130"), Margin = new Thickness(0, 0, 0, 12) };
        Grid.SetColumn(gameIconBtn, 0);
        header.Children.Add(gameIconBtn);
        AddField(header, 1, "Game name", game);
        AddField(header, 2, "Category", category);
        AddField(header, 3, "Start timer at", Field(Editor.Format(Editor.Run.Offset), text =>
        {
            if (!RunEditorModel.TryTime(text, out TimeSpan? time, true)) return false;
            Editor.Run.Offset = time ?? TimeSpan.Zero;
            Editor.Changed();
            return true;
        }));
        var count = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue, Value = Editor.Run.AttemptCount, FormatString = "0" };
        count.ValueChanged += (_, _) => { Editor.Run.AttemptCount = (int)(count.Value ?? 0); Editor.Changed(); };
        AddField(header, 4, "Attempts", count);

        var toolbar = new WrapPanel { Orientation = Orientation.Horizontal };
        StructureButton(toolbar, "Insert above", () => { Editor.Insert(currentRow); selection.Clear(); selection.Add(currentRow); RefreshGrid(); });
        StructureButton(toolbar, "Insert below", () => { Editor.Insert(currentRow + 1); currentRow++; selection.Clear(); selection.Add(currentRow); RefreshGrid(); });
        StructureButton(toolbar, "Remove", () =>
        {
            Editor.Remove(SelectedRows());
            currentRow = Math.Clamp(currentRow, 0, Math.Max(0, Editor.Run.Count - 1));
            selection.Clear();
            selection.Add(currentRow);
            RefreshGrid();
        });
        StructureButton(toolbar, "Move up", () => MoveSelection(-1));
        StructureButton(toolbar, "Move down", () => MoveSelection(1));
        Button(toolbar, "Icons…", IconMenu);
        StructureButton(toolbar, "Group…", GroupSelection);
        StructureButton(toolbar, "Ungroup", () =>
        {
            foreach (int row in SelectedRows()) Editor.Run[row].Name = RunEditorModel.UngroupName(Editor.Run[row].Name);
            Editor.Changed(); RefreshGrid();
        });
        Button(toolbar, "Other…", OtherMenu);

        var comparisons = new WrapPanel();
        comparisons.Children.Add(comparison);
        Button(comparisons, "Add…", () => ComparisonAction("Add"));
        btnRenameComp = Button(comparisons, "Rename…", () => ComparisonAction("Rename"));
        Button(comparisons, "Clone…", () => ComparisonAction("Clone"));
        btnRemoveComp = Button(comparisons, "Remove", () =>
        {
            if (comparison.SelectedItem is string name)
            {
                if (!Editor.Run.CustomComparisons.Contains(name))
                {
                    status.Text = "Only custom comparisons can be removed.";
                    return;
                }
                Editor.RemoveComparison(name);
                RefreshComparisons();
                RefreshGrid();
            }
        });
        Button(comparisons, "Import…", ImportComparisonMenu);
        Button(comparisons, "Choose generated…", ChooseComparisons);
        comparison.SelectionChanged += (_, _) => UpdateComparisonButtons();

        var method = new ComboBox { ItemsSource = new[] { "Real Time", "Game Time" }, SelectedIndex = (int)Editor.Method, MinWidth = 140 };
        method.SelectionChanged += (_, _) => { Editor.Method = (TimingMethod)Math.Max(0, method.SelectedIndex); RefreshGrid(); RefreshAttempts(); };
        var history = new CheckBox { Content = "Attempt history", Margin = new Thickness(16, 0), VerticalAlignment = VerticalAlignment.Center };
        history.IsCheckedChanged += (_, _) =>
        {
            historyBar.IsVisible = history.IsChecked == true;
            Editor.AttemptIndex = null;
            if (historyBar.IsVisible) RefreshAttempts();
            RefreshGrid();
        };
        var modeBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8) };
        modeBar.Children.Add(method);
        modeBar.Children.Add(history);
        historyBar = new StackPanel { Spacing = 6, IsVisible = false, Margin = new Thickness(0, 0, 0, 8) };
        var filters = new WrapPanel();
        filters.Children.Add(attempts);
        filters.Children.Add(completed);
        filters.Children.Add(month);
        Button(filters, "◀", () => { page = Math.Max(0, page - 1); RefreshAttempts(); });
        filters.Children.Add(pageLabel);
        Button(filters, "▶", () => { page++; RefreshAttempts(); });
        Button(filters, "Edit attempt…", EditAttempt);
        Button(filters, "Delete attempt", () =>
        {
            if (Editor.AttemptIndex is int id && Confirm("Delete this attempt and its segment history?")) { Editor.DeleteAttempt(id); RefreshAttempts(); RefreshGrid(); }
        });
        historyBar.Children.Add(filters);
        month.ItemsSource = new[] { "All months" }.Concat(Editor.Run.AttemptHistory.Where(a => a.Started.HasValue).Select(a => a.Started.Value.Time.ToString("yyyy/MM")).Distinct().OrderDescending()).ToArray();
        month.SelectedIndex = 0;
        month.SelectionChanged += (_, _) => { if (!refreshing) { page = 0; RefreshAttempts(); } };
        completed.IsCheckedChanged += (_, _) => { page = 0; RefreshAttempts(); };
        attempts.SelectionChanged += (_, _) =>
        {
            if (refreshing) return;
            Editor.AttemptIndex = (attempts.SelectedItem as AttemptChoice)?.Attempt.Index;
            RefreshGrid();
        };

        var segmentBody = new DockPanel();
        var top = new StackPanel { Spacing = 5 };
        top.Children.Add(toolbar);
        top.Children.Add(comparisons);
        top.Children.Add(modeBar);
        top.Children.Add(historyBar);

        var headerHost = new Border
        {
            ClipToBounds = true,
            Background = new SolidColorBrush(Color.Parse("#273547")),
            CornerRadius = new CornerRadius(3, 3, 0, 0)
        };
        headerHost.Child = headings;
        top.Children.Add(headerHost);
        DockPanel.SetDock(top, Dock.Top);
        segmentBody.Children.Add(top);

        var scrollViewer = new ScrollViewer
        {
            Content = rows,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        scrollViewer.ScrollChanged += (_, _) =>
        {
            headings.RenderTransform = new TranslateTransform(-scrollViewer.Offset.X, 0);
        };
        segmentBody.Children.Add(scrollViewer);

        tabs.Items.Add(new TabItem { Header = "Segments", Content = segmentBody });
        tabs.Items.Add(new TabItem { Header = "Metadata", Content = new ScrollViewer { Content = metadata } });
        tabs.Items.Add(new TabItem { Header = "Auto Splitter", Content = new ScrollViewer { Content = autoSplitter } });
        var body = new DockPanel();
        DockPanel.SetDock(header, Dock.Top); body.Children.Add(header);
        DockPanel.SetDock(status, Dock.Bottom); body.Children.Add(status);
        body.Children.Add(tabs);
        SetBody(body);
        AddButton("OK", () =>
        {
            Focus();
            Editor.Run.GameName = game.Text ?? "";
            Editor.Run.CategoryName = category.Text ?? "";
            if (this.GetVisualDescendants().OfType<TextBox>().Any(x => x.Tag as string == "invalid"))
            { status.Text = "Correct the highlighted time before saving."; return; }
            Close(DialogResult.OK);
        }, isDefault: true);
        AddButton("Cancel", DialogResult.Cancel, isCancel: true);
        AddHandler(KeyDownEvent, HandleKeys, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Closed += (_, _) =>
        {
            closed = true;
            gameIconPreview?.Dispose();
            foreach (Bitmap bitmap in previews) bitmap.Dispose();
        };
        selection.Add(0);
        RefreshGameIcon();
        RefreshComparisons();
        RefreshGrid();
        RenderMetadata(null);
        RefreshAutoSplitter();
        Opened += async (_, _) =>
        {
            if (loadOnline)
            {
                await LoadGameNamesAsync();
                await LoadAutoSplitterAsync();
                await LoadMetadataAsync();
            }
        };
    }

    private void SetupGameIconContextMenu()
    {
        var menu = new ContextMenu();
        var setItem = new MenuItem { Header = "Import game icon…" };
        setItem.Click += (_, _) => ImportIcon(-1);
        var dlIconItem = new MenuItem { Header = "Download game icon" };
        dlIconItem.Click += (_, _) => DownloadGameImage(false);
        var dlBoxItem = new MenuItem { Header = "Download box art" };
        dlBoxItem.Click += (_, _) => DownloadGameImage(true);
        var urlItem = new MenuItem { Header = "Image from URL…" };
        urlItem.Click += (_, _) => DownloadImageUrl(-1);
        var clearItem = new MenuItem { Header = "Clear game icon" };
        clearItem.Click += (_, _) => { Editor.SetIcon(-1, null); RefreshGameIcon(); };
        menu.ItemsSource = new Control[] { setItem, dlIconItem, dlBoxItem, urlItem, new Separator(), clearItem };
        gameIconBtn.ContextMenu = menu;
    }

    private void RefreshGameIcon()
    {
        gameIconPreview?.Dispose();
        gameIconPreview = null;
        if (Editor.Run.GameIcon is Image image)
        {
            using var stream = new MemoryStream();
            image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            gameIconPreview = new Bitmap(stream);
            gameIconBtn.Content = new Avalonia.Controls.Image { Source = gameIconPreview, Width = 48, Height = 48, Stretch = Stretch.Uniform };
        }
        else
        {
            gameIconBtn.Content = new TextBlock { Text = "🎮\nIcon", TextAlignment = TextAlignment.Center, FontSize = 11, Foreground = Brushes.Gray };
        }
    }

    private void AddField(Grid grid, int column, string label, Control input)
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 10, 0) };
        panel.Children.Add(Label(label)); panel.Children.Add(input);
        Grid.SetColumn(panel, column); grid.Children.Add(panel);
    }

    private TextBox Field(string initial, Func<string, bool> commit)
    {
        var box = new TextBox { Text = initial, MinWidth = 80 };
        box.GotFocus += (_, _) => initial = box.Text;
        box.LostFocus += (_, _) =>
        {
            if (box.Text == initial) return;
            bool valid = commit(box.Text ?? "");
            box.Tag = valid ? null : "invalid";
            box.BorderBrush = valid ? null : Brushes.OrangeRed;
            status.Text = valid ? "" : "Invalid time. Use h:mm:ss.fff, or leave the cell empty.";
            if (valid) initial = box.Text;
        };
        return box;
    }

    private static Button Button(Panel parent, string text, Action action)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 0, 5, 3), Padding = new Thickness(10, 6) };
        button.Click += (_, _) => action(); parent.Children.Add(button); return button;
    }

    private void StructureButton(Panel parent, string text, Action action) => structuralButtons.Add(Button(parent, text, action));
    private int[] SelectedRows() => selection.Where(i => i >= 0 && i < Editor.Run.Count).Order().ToArray();

    private void UpdateComparisonButtons()
    {
        bool isCustom = comparison.SelectedItem is string name && Editor.Run.CustomComparisons.Contains(name) && name != Model.Run.PersonalBestComparisonName;
        if (btnRenameComp != null) btnRenameComp.IsEnabled = isCustom;
        if (btnRemoveComp != null) btnRemoveComp.IsEnabled = isCustom;
    }

    private void RefreshComparisons()
    {
        string selected = comparison.SelectedItem as string;
        comparison.ItemsSource = Editor.Run.Comparisons.ToArray();
        comparison.SelectedItem = Editor.Run.Comparisons.Contains(selected) ? selected : Editor.Run.CustomComparisons.FirstOrDefault();
        UpdateComparisonButtons();
    }

    public void RefreshGrid()
    {
        refreshing = true;
        rows.Children.Clear();
        rowBorders.Clear();
        cellBoxes.Clear();
        headings.Children.Clear();
        foreach (Bitmap bitmap in previews) bitmap.Dispose();
        previews.Clear();

        currentRow = Math.Clamp(currentRow, 0, Math.Max(0, Editor.Run.Count - 1));
        selection.RemoveWhere(i => i >= Editor.Run.Count);
        if (selection.Count == 0 && Editor.Run.Count > 0) selection.Add(currentRow);

        string[] custom = (historyBar?.IsVisible == true) ? [] : Editor.Run.CustomComparisons.Where(c => c != Model.Run.PersonalBestComparisonName).ToArray();
        int fixedWidth = 42 + 48 + 130 + 130 + 140 + custom.Length * 140;
        int availableWidth = Math.Max(760, (int)(Bounds.Width > 0 ? Bounds.Width : Width) - 55);
        int nameWidth = Math.Max(240, availableWidth - fixedWidth);
        string widths = $"42,48,{nameWidth},130,130,140" + string.Concat(custom.Select(_ => ",140"));

        headings.ColumnDefinitions = new ColumnDefinitions(widths);
        string[] titles = ["#", "Icon", "Segment name", "Split time", "Segment time", (historyBar?.IsVisible == true) ? "Best difference" : "Best segment", .. custom];
        for (int c = 0; c < titles.Length; c++)
        {
            var label = new TextBlock
            {
                Text = titles[c],
                Margin = new Thickness(8, 10),
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (c >= 6)
            {
                string compName = titles[c];
                var menu = new ContextMenu();
                var renameItem = new MenuItem { Header = $"Rename '{compName}'…" };
                renameItem.Click += (_, _) => { comparison.SelectedItem = compName; ComparisonAction("Rename"); };
                var cloneItem = new MenuItem { Header = $"Clone '{compName}'…" };
                cloneItem.Click += (_, _) => { comparison.SelectedItem = compName; ComparisonAction("Clone"); };
                var removeItem = new MenuItem { Header = $"Remove '{compName}'" };
                removeItem.Click += (_, _) => { Editor.RemoveComparison(compName); RefreshComparisons(); RefreshGrid(); };
                menu.ItemsSource = new Control[] { renameItem, cloneItem, new Separator(), removeItem };
                label.ContextMenu = menu;
                ToolTip.SetTip(label, "Right-click to rename, clone or remove");
            }
            Grid.SetColumn(label, c);
            headings.Children.Add(label);
        }

        for (int r = 0; r < Editor.Run.Count; r++)
        {
            int row = r;
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(widths) };
            var number = new Button
            {
                Content = (r + 1).ToString(),
                Padding = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = Brushes.Transparent
            };
            number.PointerPressed += (_, e) => { SelectRow(row, e.KeyModifiers); dragRow = row; dragStart = e.GetPosition(this); };
            number.PointerMoved += async (_, e) =>
            {
                if (dragRow != row || !e.GetCurrentPoint(number).Properties.IsLeftButtonPressed || !Editor.AllowChangingSegments || historyBar?.IsVisible == true) return;
                Point delta = e.GetPosition(this) - dragStart;
                if (Math.Abs(delta.X) + Math.Abs(delta.Y) < 6) return;
                dragRow = -1;
                var data = new DataObject(); data.Set("LiveSplit.Segment", row);
                await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
            };
            number.Click += (_, _) => { currentRow = row; };
            grid.Children.Add(number);

            var icon = new Button
            {
                Padding = new Thickness(3),
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            ToolTip.SetTip(icon, "Segment icon (click to set, right-click for options)");
            if (Editor.Run[r].Icon is Image image)
            {
                using var stream = new MemoryStream();
                image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Position = 0;
                var bitmap = new Bitmap(stream);
                previews.Add(bitmap);
                icon.Content = new Avalonia.Controls.Image { Source = bitmap, Width = 28, Height = 28, Stretch = Stretch.Uniform };
            }
            else icon.Content = "+";
            icon.Click += (_, _) => ImportIcon(row);
            SetupSegmentIconContextMenu(icon, row);
            Grid.SetColumn(icon, 1);
            grid.Children.Add(icon);

            int totalEditableColumns = 4 + custom.Length;
            for (int col = 1; col <= totalEditableColumns; col++)
            {
                int column = col;
                string name = col >= 5 ? custom[col - 5] : null;
                var box = Field(Editor.Cell(row, column, name), text => Editor.EditCell(row, column, text, name));
                box.BorderThickness = new Thickness(0, 0, 0, 1);
                box.Background = Brushes.Transparent;
                box.Padding = new Thickness(8, 7);
                box.IsReadOnly = (historyBar?.IsVisible == true) && column is not (2 or 3);
                box.GotFocus += (_, _) =>
                {
                    currentRow = row;
                    currentColumn = column;
                    if (!selection.Contains(row))
                    {
                        selection.Clear();
                        selection.Add(row);
                    }
                    UpdateSelection();
                };
                box.LostFocus += (_, _) => { if (box.Tag == null) UpdateTimeCells(); };
                cellBoxes[(row, column)] = box;
                Grid.SetColumn(box, column + 1);
                grid.Children.Add(box);
            }

            var border = new Border { Child = grid };
            DragDrop.SetAllowDrop(border, true);
            border.AddHandler(DragDrop.DragOverEvent, (_, e) =>
                e.DragEffects = Editor.AllowChangingSegments && (historyBar?.IsVisible != true) && e.Data.Contains("LiveSplit.Segment")
                    ? DragDropEffects.Move : DragDropEffects.None);
            border.AddHandler(DragDrop.DragEnterEvent, (_, _) =>
            {
                if (Editor.AllowChangingSegments && (historyBar?.IsVisible != true))
                {
                    border.BorderBrush = Brushes.DeepSkyBlue;
                    border.BorderThickness = new Thickness(0, 2, 0, 0);
                }
            });
            border.AddHandler(DragDrop.DragLeaveEvent, (_, _) =>
            {
                border.BorderBrush = null;
                border.BorderThickness = new Thickness(0);
            });
            border.AddHandler(DragDrop.DropEvent, (_, e) =>
            {
                border.BorderBrush = null;
                border.BorderThickness = new Thickness(0);
                if (e.Data.Get("LiveSplit.Segment") is int from)
                {
                    Editor.Move(from, row);
                    currentRow = row;
                    selection.Clear();
                    selection.Add(row);
                    RefreshGrid();
                }
            });
            rowBorders.Add(border);
            rows.Children.Add(border);
        }

        bool canModify = Editor.AllowChangingSegments && (historyBar?.IsVisible != true);
        foreach (Button button in structuralButtons) button.IsEnabled = canModify;
        UpdateSelection();
        refreshing = false;
    }

    private void SetupSegmentIconContextMenu(Button iconBtn, int row)
    {
        var menu = new ContextMenu();
        var setItem = new MenuItem { Header = "Import icon…" };
        setItem.Click += (_, _) => ImportIcon(row);
        var urlItem = new MenuItem { Header = "Image from URL…" };
        urlItem.Click += (_, _) => DownloadImageUrl(row);
        var clearItem = new MenuItem { Header = "Clear icon" };
        clearItem.Click += (_, _) => { Editor.SetIcon(row, null); RefreshGrid(); };
        var setSelItem = new MenuItem { Header = "Import for selected segments…" };
        setSelItem.Click += (_, _) =>
        {
            string path = PlatformServices.Current.ChooseOpenFile(this, "Choose icon", "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files|*.*");
            if (path == null) return;
            foreach (int r in SelectedRows()) Editor.SetIcon(r, Image.FromFile(path));
            RefreshGrid();
        };
        var clearSelItem = new MenuItem { Header = "Clear for selected segments" };
        clearSelItem.Click += (_, _) => { foreach (int r in SelectedRows()) Editor.SetIcon(r, null); RefreshGrid(); };
        menu.ItemsSource = new Control[] { setItem, urlItem, clearItem, new Separator(), setSelItem, clearSelItem };
        iconBtn.ContextMenu = menu;
    }

    private void FocusCell(int row, int col)
    {
        if (cellBoxes.TryGetValue((row, col), out TextBox box))
        {
            box.Focus();
            box.SelectAll();
        }
    }

    private void UpdateTimeCells()
    {
        if (refreshing) return;
        for (int r = 0; r < rowBorders.Count; r++)
        {
            var grid = (Grid)rowBorders[r].Child;
            foreach (TextBox box in grid.Children.OfType<TextBox>())
            {
                int column = Grid.GetColumn(box) - 1;
                if (column < 2 || box.IsFocused || box.Tag as string == "invalid") continue;
                string[] custom = Editor.Run.CustomComparisons.Where(c => c != Model.Run.PersonalBestComparisonName).ToArray();
                box.Text = Editor.Cell(r, column, column >= 5 ? custom[column - 5] : null);
            }
        }
    }

    private void SelectRow(int row, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            for (int i = Math.Min(row, currentRow); i <= Math.Max(row, currentRow); i++) selection.Add(i);
        }
        else if (modifiers.HasFlag(KeyModifiers.Control)) { if (!selection.Add(row)) selection.Remove(row); }
        else { selection.Clear(); selection.Add(row); }
        currentRow = row; UpdateSelection();
    }

    private void UpdateSelection()
    {
        for (int r = 0; r < rowBorders.Count; r++)
            rowBorders[r].Background = new SolidColorBrush(Color.Parse(selection.Contains(r) ? "#263E56" : r % 2 == 0 ? "#202329" : "#24282E"));
    }

    private void MoveSelection(int delta)
    {
        int[] selected = SelectedRows();
        if (selected.Length == 0 || selected[0] + delta < 0 || selected[^1] + delta >= Editor.Run.Count) return;
        foreach (int row in delta < 0 ? selected : selected.Reverse()) Editor.Move(row, row + delta);
        selection.Clear(); foreach (int row in selected) selection.Add(row + delta);
        currentRow += delta; RefreshGrid();
    }

    private void GroupSelection()
    {
        string name = "";
        if (PlatformServices.Current.Prompt(this, "Create segment group", "Group name:", ref name) != DialogResult.OK) return;
        TryAction(() => Editor.Group(SelectedRows(), name)); RefreshGrid();
    }

    private void ComparisonAction(string action)
    {
        string source = comparison.SelectedItem as string;
        if (action == "Rename" && (source == null || !Editor.Run.CustomComparisons.Contains(source) || source == Model.Run.PersonalBestComparisonName))
        {
            status.Text = "Only custom comparisons can be renamed.";
            return;
        }
        string name = action == "Rename" ? source : "";
        if (PlatformServices.Current.Prompt(this, action + " comparison", "Comparison name:", ref name) != DialogResult.OK) return;
        TryAction(() => { if (action == "Rename") Editor.RenameComparison(source, name); else Editor.AddComparison(name, action == "Clone" ? source : null); });
        RefreshComparisons(); RefreshGrid();
    }

    private void ImportComparisonMenu()
    {
        var menu = new EditorOptionsWindow("Import comparison");
        menu.Action("From file…", () => { ImportComparisonFile(); menu.Close(DialogResult.OK); });
        menu.Action("From URL…", () => { ImportComparisonUrl(); menu.Close(DialogResult.OK); });
        UiThread.RunSync(() => menu.ShowModalAsync(this));
    }

    private void ImportComparisonFile()
    {
        string path = PlatformServices.Current.ChooseOpenFile(this, "Import comparison", "Splits (*.lss)|*.lss|All files|*.*");
        if (path == null) return;
        TryAction(() =>
        {
            using var stream = File.OpenRead(path);
            IRun imported = new StandardFormatsRunFactory(stream, path).Create(new StandardComparisonGeneratorsFactory());
            string name = Path.GetFileNameWithoutExtension(path);
            if (PlatformServices.Current.Prompt(this, "Import comparison", "Comparison name:", ref name) != DialogResult.OK) return;
            Editor.AddComparison(name);
            for (int i = 0; i < Math.Min(imported.Count, Editor.Run.Count); i++) Editor.Run[i].Comparisons[name] = imported[i].PersonalBestSplitTime;
            Editor.Run.FixSplits(); Editor.Changed(); RefreshComparisons(); RefreshGrid();
        });
    }

    private async void ImportComparisonUrl()
    {
        string url = "";
        if (PlatformServices.Current.Prompt(this, "Import comparison from URL", "URL:", ref url) != DialogResult.OK || string.IsNullOrWhiteSpace(url)) return;
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            byte[] bytes = await client.GetByteArrayAsync(url);
            if (closed) return;
            using var stream = new MemoryStream(bytes);
            IRun imported = new StandardFormatsRunFactory(stream, url).Create(new StandardComparisonGeneratorsFactory());
            string name = "Imported";
            if (PlatformServices.Current.Prompt(this, "Import comparison", "Comparison name:", ref name) != DialogResult.OK) return;
            Editor.AddComparison(name);
            for (int i = 0; i < Math.Min(imported.Count, Editor.Run.Count); i++) Editor.Run[i].Comparisons[name] = imported[i].PersonalBestSplitTime;
            Editor.Run.FixSplits(); Editor.Changed(); RefreshComparisons(); RefreshGrid();
        }
        catch (Exception ex) { if (!closed) status.Text = "Import failed: " + ex.Message; }
    }

    private void ImportIcon(int row)
    {
        string path = PlatformServices.Current.ChooseOpenFile(this, "Choose icon", "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files|*.*");
        if (path != null) TryAction(() =>
        {
            Editor.SetIcon(row, Image.FromFile(path));
            if (row < 0) RefreshGameIcon();
            else RefreshGrid();
        });
    }

    private void IconMenu()
    {
        var dialog = new EditorOptionsWindow("Icons");
        dialog.Action("Import selected segment icons…", () =>
        {
            string path = PlatformServices.Current.ChooseOpenFile(dialog, "Choose icon", "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp");
            if (path == null) return;
            foreach (int row in SelectedRows()) Editor.SetIcon(row, Image.FromFile(path));
            RefreshGrid();
        });
        dialog.Action("Clear selected segment icons", () => { foreach (int row in SelectedRows()) Editor.SetIcon(row, null); RefreshGrid(); });
        dialog.Action("Import game icon…", () => ImportIcon(-1));
        dialog.Action("Clear game icon", () => { Editor.SetIcon(-1, null); RefreshGameIcon(); });
        dialog.Action("Download game icon…", () => DownloadGameImage(false));
        dialog.Action("Download box art…", () => DownloadGameImage(true));
        dialog.Action("Image from URL…", () => DownloadImageUrl(-1));
        UiThread.RunSync(() => dialog.ShowModalAsync(this));
    }

    private void OtherMenu()
    {
        var dialog = new EditorOptionsWindow("Other actions");
        dialog.Action("Clear history", () => { if (Confirm("Clear all attempt and segment history?")) { Editor.Run.ClearHistory(); Editor.Changed(); RefreshAttempts(); RefreshGrid(); } });
        dialog.Action("Clear all times", () => { if (Confirm("Clear all times, attempts and custom comparisons?")) { Editor.Run.ClearTimes(); Editor.Changed(true); RefreshComparisons(); RefreshAttempts(); RefreshGrid(); } });
        dialog.Action("Clean sum of best…", () =>
        {
            SumOfBest.Clean(Editor.Run, parameters => Confirm("Remove potentially inaccurate " + Editor.Format(parameters.timeBetween) + " between " + (parameters.startingSegment?.Name ?? "start") + " and " + parameters.endingSegment.Name + "?"));
            Editor.Changed(); RefreshGrid();
        });
        dialog.Action("Use layout…", () =>
        {
            string path = PlatformServices.Current.ChooseOpenFile(dialog, "Layout for this run", "LiveSplit layout|*.lsl");
            if (path != null) { Editor.Run.LayoutPath = path; Editor.Changed(); }
        });
        dialog.Action("Clear layout association", () => { Editor.Run.LayoutPath = null; Editor.Changed(); });
        UiThread.RunSync(() => dialog.ShowModalAsync(this));
    }

    private void TryAction(Action action)
    {
        try { action(); status.Text = ""; }
        catch (Exception ex) { status.Text = ex.Message; }
    }

    private bool Confirm(string text) => PlatformServices.Current.ShowMessage(this, text, "Edit Splits", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    private async void HandleKeys(object sender, KeyEventArgs e)
    {
        if (tabs.SelectedIndex != 0) return;

        if (e.Key == Key.Enter && e.Source is TextBox)
        {
            e.Handled = true;
            if (currentRow + 1 < Editor.Run.Count)
            {
                currentRow++;
                FocusCell(currentRow, currentColumn);
            }
            return;
        }

        if (e.Key == Key.Escape && e.Source is TextBox box)
        {
            string[] custom = Editor.Run.CustomComparisons.Where(c => c != Model.Run.PersonalBestComparisonName).ToArray();
            box.Text = Editor.Cell(currentRow, currentColumn, currentColumn >= 5 ? custom[currentColumn - 5] : null);
            box.Tag = null;
            box.BorderBrush = null;
            status.Text = "";
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers == KeyModifiers.Alt && e.Key is Key.Up or Key.Down)
        {
            MoveSelection(e.Key == Key.Up ? -1 : 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Insert && Editor.AllowChangingSegments)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                Editor.Insert(currentRow + 1);
                currentRow++;
            }
            else
            {
                Editor.Insert(currentRow);
            }
            selection.Clear();
            selection.Add(currentRow);
            RefreshGrid();
            e.Handled = true;
        }
        else if (e.Key == Key.A && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Source is not TextBox)
        {
            selection.Clear();
            for (int i = 0; i < Editor.Run.Count; i++) selection.Add(i);
            UpdateSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Editor.Remove(SelectedRows());
            currentRow = Math.Clamp(currentRow, 0, Math.Max(0, Editor.Run.Count - 1));
            selection.Clear();
            selection.Add(currentRow);
            RefreshGrid();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && e.Source is not TextBox)
        {
            Editor.EditCell(currentRow, currentColumn, "", comparison.SelectedItem as string);
            RefreshGrid();
            e.Handled = true;
        }
        else if ((e.Key == Key.C && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift)) ||
                 (e.Key == Key.C && e.KeyModifiers == KeyModifiers.Control && e.Source is not TextBox))
        {
            string text = string.Join('\n', SelectedRows().Select(r => string.Join('\t', Enumerable.Range(1, 4).Select(c => Editor.Cell(r, c)))));
            if (Clipboard != null) await Clipboard.SetTextAsync(text);
            e.Handled = true;
        }
        else if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control) && Clipboard != null)
        {
            string text = await Clipboard.GetTextAsync();
            if (text != null && (text.Contains('\t') || text.Contains('\n')))
            {
                e.Handled = true;
                Paste(text);
            }
        }
    }

    public void Paste(string text)
    {
        int invalid = Editor.Paste(currentRow, currentColumn, text);
        RefreshGrid();
        status.Text = invalid == 0 ? "" : $"Skipped {invalid} invalid or read-only cells.";
    }
}

internal sealed class EditorOptionsWindow : DialogWindow
{
    private readonly StackPanel body = new() { Spacing = 6, MinWidth = 300 };
    public EditorOptionsWindow(string title) : base(title) { SetBody(body); AddButton("Close", DialogResult.OK, isCancel: true); }
    public void Action(string text, Action action)
    {
        var button = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += (_, _) => action(); body.Children.Add(button);
    }
}
