using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using LiveSplit.Controls.Settings;
using LiveSplit.Hosting;
using System;
using System.Collections;
using System.IO;
using System.Linq;

namespace LiveSplit.View.ComponentSettings;

public sealed class AutoSplittingRuntimeView : SettingsPage
{
    public AutoSplittingRuntimeView(object model) : base(model)
    {
        Add(CreateScriptPathRow());

        var errorBlock = new TextBlock
        {
            Foreground = Brushes.Tomato,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4)
        };
        Bind(errorBlock, TextBlock.TextProperty, "Error", BindingMode.OneWay);
        errorBlock.Bind(IsVisibleProperty, new Binding("Error")
        {
            Source = Model,
            Converter = new FuncValueConverter<string, bool>(s => !string.IsNullOrEmpty(s))
        });
        Add(errorBlock);

        var itemsControl = new ItemsControl();
        Bind(itemsControl, ItemsControl.ItemsSourceProperty, "Items", BindingMode.OneWay);
        itemsControl.ItemTemplate = new FuncDataTemplate<object>((item, _) => CreateWidgetControl(item));

        var scroll = new ScrollViewer
        {
            Content = itemsControl,
            MaxHeight = 400,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };
        Add(Section("Auto Splitter Settings", scroll));
    }

    private Control CreateScriptPathRow()
    {
        var box = new TextBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        Bind(box, TextBox.TextProperty, "ScriptPath");
        if (Has("IsScriptPathEnabled"))
            Bind(box, IsEnabledProperty, "IsScriptPathEnabled", BindingMode.OneWay);

        DragDrop.SetAllowDrop(box, true);
        box.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        });
        box.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var path = e.Data.GetFiles()?.Select(f => f.TryGetLocalPath()).FirstOrDefault(p => p != null);
            if (!string.IsNullOrEmpty(path))
            {
                Model.GetType().GetProperty("ScriptPath")?.SetValue(Model, path);
            }
            e.Handled = true;
        });

        var browse = new Button { Content = T("Browse...") };
        if (Has("IsScriptPathEnabled"))
            Bind(browse, IsEnabledProperty, "IsScriptPathEnabled", BindingMode.OneWay);

        browse.Click += async (_, _) =>
        {
            var provider = TopLevel.GetTopLevel(box)?.StorageProvider;
            if (provider == null) return;
            string existing = Read("ScriptPath")?.ToString();
            existing = PathCompat.Resolve(existing ?? "");
            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = T("Select Auto Splitting Script"),
                SuggestedStartLocation = File.Exists(existing) ? await provider.TryGetFolderFromPathAsync(Path.GetDirectoryName(existing)) : null,
                SuggestedFileName = File.Exists(existing) ? Path.GetFileName(existing) : null,
                FileTypeFilter =
                [
                    new FilePickerFileType(T("Auto Splitting Runtime (*.wasm)")) { Patterns = ["*.wasm"] },
                    FilePickerFileTypes.All
                ]
            });
            string picked = files.Select(f => f.TryGetLocalPath()).FirstOrDefault(p => p != null);
            if (!string.IsNullOrEmpty(picked))
            {
                Model.GetType().GetProperty("ScriptPath")?.SetValue(Model, picked);
            }
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6 };
        grid.Children.Add(box);
        Grid.SetColumn(browse, 1);
        grid.Children.Add(browse);

        return Row("Script Path:", grid);
    }

    private Control CreateWidgetControl(object item)
    {
        if (item == null) return new Panel();

        string type = item.GetType().GetProperty("Type")?.GetValue(item)?.ToString() ?? "";
        string desc = item.GetType().GetProperty("Description")?.GetValue(item)?.ToString() ?? "";
        string tooltip = item.GetType().GetProperty("Tooltip")?.GetValue(item)?.ToString() ?? "";
        int level = (int)(item.GetType().GetProperty("HeadingLevel")?.GetValue(item) ?? 0);

        Control control;
        switch (type)
        {
            case "title":
                control = new TextBlock
                {
                    Text = T(desc),
                    FontWeight = FontWeight.Bold,
                    FontSize = Math.Max(12, 16 - level * 2),
                    Margin = new Thickness(level * 16, 8, 0, 4)
                };
                break;

            case "bool":
                var check = new CheckBox
                {
                    Content = new TextBlock { Text = T(desc), TextWrapping = TextWrapping.Wrap },
                    Margin = new Thickness((level + 1) * 16, 2, 0, 2)
                };
                check.Bind(CheckBox.IsCheckedProperty, new Binding("BooleanValue") { Source = item, Mode = BindingMode.TwoWay });
                if (!string.IsNullOrEmpty(tooltip)) ToolTip.SetTip(check, T(tooltip));
                control = check;
                break;

            case "choice":
                var choicesList = item.GetType().GetProperty("Choices")?.GetValue(item) as IEnumerable;
                var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 140 };
                if (choicesList != null) combo.ItemsSource = choicesList;
                combo.ItemTemplate = new FuncDataTemplate<object>((choice, _) =>
                {
                    string d = choice?.GetType().GetProperty("Description")?.GetValue(choice)?.ToString() ?? "";
                    return new TextBlock { Text = T(d) };
                });

                string currentKey = item.GetType().GetProperty("StringValue")?.GetValue(item)?.ToString();
                if (choicesList != null)
                {
                    foreach (var c in choicesList)
                    {
                        if (c?.GetType().GetProperty("Key")?.GetValue(c)?.ToString() == currentKey)
                        {
                            combo.SelectedItem = c;
                            break;
                        }
                    }
                }

                combo.SelectionChanged += (_, _) =>
                {
                    var selected = combo.SelectedItem;
                    string key = selected?.GetType().GetProperty("Key")?.GetValue(selected)?.ToString();
                    if (key != null) item.GetType().GetProperty("StringValue")?.SetValue(item, key);
                };

                var choiceGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("180,*"),
                    ColumnSpacing = 10,
                    Margin = new Thickness((level + 1) * 16, 2, 0, 2)
                };
                choiceGrid.Children.Add(new TextBlock { Text = T(desc), VerticalAlignment = VerticalAlignment.Center });
                Grid.SetColumn(combo, 1);
                choiceGrid.Children.Add(combo);
                if (!string.IsNullOrEmpty(tooltip)) ToolTip.SetTip(choiceGrid, T(tooltip));
                control = choiceGrid;
                break;

            case "file-select":
                var fileBox = new TextBox { HorizontalAlignment = HorizontalAlignment.Stretch };
                fileBox.Bind(TextBox.TextProperty, new Binding("StringValue") { Source = item, Mode = BindingMode.TwoWay });
                var fileBrowse = new Button { Content = T("Browse...") };
                string filter = item.GetType().GetProperty("Filter")?.GetValue(item)?.ToString() ?? "*.*";

                fileBrowse.Click += async (_, _) =>
                {
                    var provider = TopLevel.GetTopLevel(fileBox)?.StorageProvider;
                    if (provider == null) return;
                    var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = T("Select File"),
                        FileTypeFilter = [new FilePickerFileType(filter) { Patterns = [filter] }, FilePickerFileTypes.All]
                    });
                    string picked = files.Select(f => f.TryGetLocalPath()).FirstOrDefault(p => p != null);
                    if (!string.IsNullOrEmpty(picked))
                    {
                        item.GetType().GetProperty("StringValue")?.SetValue(item, picked);
                    }
                };

                var fileGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6 };
                fileGrid.Children.Add(fileBox);
                Grid.SetColumn(fileBrowse, 1);
                fileGrid.Children.Add(fileBrowse);

                var fsGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("180,*"),
                    ColumnSpacing = 10,
                    Margin = new Thickness((level + 1) * 16, 2, 0, 2)
                };
                fsGrid.Children.Add(new TextBlock { Text = T(desc), VerticalAlignment = VerticalAlignment.Center });
                Grid.SetColumn(fileGrid, 1);
                fsGrid.Children.Add(fileGrid);
                if (!string.IsNullOrEmpty(tooltip)) ToolTip.SetTip(fsGrid, T(tooltip));
                control = fsGrid;
                break;

            default:
                control = new TextBlock { Text = T(desc), Margin = new Thickness(level * 16, 2, 0, 2) };
                break;
        }

        return control;
    }
}
