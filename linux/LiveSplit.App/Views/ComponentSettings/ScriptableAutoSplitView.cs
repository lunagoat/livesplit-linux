using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using LiveSplit.Controls.Settings;
using LiveSplit.Hosting;
using System;
using System.Collections;
using System.IO;
using System.Linq;

namespace LiveSplit.View.ComponentSettings;

public sealed class ScriptableAutoSplitView : SettingsPage
{
    public ScriptableAutoSplitView(object model) : base(model)
    {
        Add(CreateScriptPathRow());
        Add(CreateOptionsRow());

        var treeView = new TreeView
        {
            MinHeight = 160,
            MaxHeight = 320,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        treeView.Styles.Add(new Style(x => x.OfType<TreeViewItem>())
        {
            Setters = { new Setter(TreeViewItem.IsExpandedProperty, new Binding("IsExpanded") { Mode = BindingMode.TwoWay }) }
        });
        Bind(treeView, ItemsControl.ItemsSourceProperty, "CustomSettings", BindingMode.OneWay);
        treeView.ItemTemplate = new FuncTreeDataTemplate<object>(
            (node, _) => CreateNodeControl(node),
            node => (IEnumerable)node?.GetType().GetProperty("Children")?.GetValue(node)
        );

        var btnCheckAll = new Button { Content = T("Check All") };
        btnCheckAll.Click += (_, _) => Model.GetType().GetMethod("SetAll")?.Invoke(Model, [true, null]);

        var btnUncheckAll = new Button { Content = T("Uncheck All") };
        btnUncheckAll.Click += (_, _) => Model.GetType().GetMethod("SetAll")?.Invoke(Model, [false, null]);

        var btnResetAll = new Button { Content = T("Reset to Default") };
        btnResetAll.Click += (_, _) => Model.GetType().GetMethod("SetAll")?.Invoke(Model, [null, null]);

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 4, 0, 0),
            Children = { btnCheckAll, btnUncheckAll, btnResetAll }
        };

        var customContainer = new StackPanel { Spacing = 6, Children = { treeView, buttonRow } };
        var advancedSection = Section("Advanced", customContainer);
        advancedSection.Bind(IsVisibleProperty, new Binding("HasCustomSettings") { Source = Model, Mode = BindingMode.OneWay });
        Add(advancedSection);
    }

    private Control CreateScriptPathRow()
    {
        var box = new TextBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        Bind(box, TextBox.TextProperty, "ScriptPath");

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
        browse.Click += async (_, _) =>
        {
            var provider = TopLevel.GetTopLevel(box)?.StorageProvider;
            if (provider == null) return;
            string existing = Read("ScriptPath")?.ToString();
            existing = PathCompat.Resolve(existing ?? "");
            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = T("Select Auto Split Script"),
                SuggestedStartLocation = File.Exists(existing) ? await provider.TryGetFolderFromPathAsync(Path.GetDirectoryName(existing)) : null,
                SuggestedFileName = File.Exists(existing) ? Path.GetFileName(existing) : null,
                FileTypeFilter =
                [
                    new FilePickerFileType(T("Auto Split Script (*.asl)")) { Patterns = ["*.asl"] },
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

    private Control CreateOptionsRow()
    {
        var panel = new DockPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

        var versionBlock = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = Avalonia.Media.TextAlignment.Right
        };
        Bind(versionBlock, TextBlock.TextProperty, "GameVersionLabel", BindingMode.OneWay);
        DockPanel.SetDock(versionBlock, Dock.Right);
        panel.Children.Add(versionBlock);

        var basicPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var basicList = Read("BasicSettings") as IEnumerable;
        if (basicList != null)
        {
            foreach (var item in basicList)
            {
                var check = new CheckBox();
                check.Bind(CheckBox.IsCheckedProperty, new Binding("Value") { Source = item, Mode = BindingMode.TwoWay });
                check.Bind(IsEnabledProperty, new Binding("IsEnabled") { Source = item, Mode = BindingMode.OneWay });
                string label = item.GetType().GetProperty("Label")?.GetValue(item)?.ToString() ?? "";
                check.Content = new TextBlock { Text = T(label) };
                basicPanel.Children.Add(check);
            }
        }
        panel.Children.Add(basicPanel);

        return Row("Options:", panel);
    }

    private Control CreateNodeControl(object node)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        var check = new CheckBox();
        check.Bind(CheckBox.IsCheckedProperty, new Binding("Value") { Source = node, Mode = BindingMode.TwoWay });
        check.Bind(IsEnabledProperty, new Binding("IsEnabled") { Source = node, Mode = BindingMode.OneWay });

        string label = node.GetType().GetProperty("Label")?.GetValue(node)?.ToString() ?? "";
        string tooltip = node.GetType().GetProperty("Tooltip")?.GetValue(node)?.ToString();

        var text = new TextBlock { Text = T(label), VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(check);
        stack.Children.Add(text);

        if (!string.IsNullOrEmpty(tooltip)) ToolTip.SetTip(stack, T(tooltip));

        var menu = new ContextMenu();
        void AddItem(string title, Action action)
        {
            var item = new MenuItem { Header = T(title) };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        AddItem("Expand Tree", () => SetAllExpanded(true));
        AddItem("Collapse Tree", () => SetAllExpanded(false));
        menu.Items.Add(new Separator());
        AddItem("Expand Branch", () => SetBranchExpanded(node, true));
        AddItem("Collapse Branch", () => SetBranchExpanded(node, false));
        menu.Items.Add(new Separator());
        AddItem("Check Branch", () => Model.GetType().GetMethod("SetAll")?.Invoke(Model, [true, node]));
        AddItem("Uncheck Branch", () => Model.GetType().GetMethod("SetAll")?.Invoke(Model, [false, node]));
        AddItem("Reset Branch to Default", () => Model.GetType().GetMethod("SetAll")?.Invoke(Model, [null, node]));
        AddItem("Reset Setting to Default", () =>
        {
            bool def = (bool)(node.GetType().GetProperty("DefaultValue")?.GetValue(node) ?? false);
            node.GetType().GetProperty("Value")?.SetValue(node, def);
        });

        stack.ContextMenu = menu;
        return stack;
    }

    private void SetAllExpanded(bool expanded)
    {
        var custom = Read("CustomSettings") as IEnumerable;
        if (custom != null)
        {
            foreach (var n in custom) SetBranchExpanded(n, expanded);
        }
    }

    private void SetBranchExpanded(object n, bool expanded)
    {
        if (n == null) return;
        n.GetType().GetProperty("IsExpanded")?.SetValue(n, expanded);
        var children = n.GetType().GetProperty("Children")?.GetValue(n) as IEnumerable;
        if (children != null)
        {
            foreach (var c in children) SetBranchExpanded(c, expanded);
        }
    }
}
