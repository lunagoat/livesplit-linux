using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplit.Controls.Settings;
using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Services;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Image = System.Drawing.Image;

namespace LiveSplit.View.Dialogs;

public class LayoutEditorDialog : DialogWindow
{
    protected readonly LayoutEditorRequest request;
    public IList<UI.Components.IComponent> ComponentsToDispose { get; } = [];
    public IList<Image> ImagesToDispose { get; } = [];

    private readonly ListBox listBox;
    private readonly Button btnAdd;
    private readonly Button btnRemove;
    private readonly Button btnMoveUp;
    private readonly Button btnMoveDown;
    private readonly RadioButton rbVertical;
    private readonly RadioButton rbHorizontal;
    private readonly Button btnLayoutSettings;
    private readonly TextBlock headerTitle;
    private readonly ScrollViewer settingsScrollViewer;
    private readonly ContentControl settingsHost;

    public LayoutEditorDialog(LayoutEditorRequest request)
        : base(SettingsPage.T("Layout Editor"))
    {
        this.request = request;

        Width = 920;
        Height = 680;
        MinWidth = 760;
        MinHeight = 460;
        CanResize = true;

        ComponentCatalog.Initialize();

        if (request.Layout != null && request.Layout.Settings == null)
        {
            request.Layout.Settings = new Options.SettingsFactories.StandardLayoutSettingsFactory().Create();
        }

        var rootGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("280,*"),
            ColumnSpacing = 12
        };

        // ---------------- LEFT PANEL ----------------
        var leftPanel = new DockPanel { LastChildFill = true };

        // 1. Top toolbar
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, 8)
        };

        btnAdd = new Button
        {
            Content = "+ " + SettingsPage.T("Add"),
            MinWidth = 65
        };
        btnRemove = new Button
        {
            Content = "- " + SettingsPage.T("Remove"),
            MinWidth = 75
        };
        btnMoveUp = new Button
        {
            Content = "▲",
            Width = 32
        };
        btnMoveDown = new Button
        {
            Content = "▼",
            Width = 32
        };

        toolbar.Children.Add(btnAdd);
        toolbar.Children.Add(btnRemove);
        toolbar.Children.Add(btnMoveUp);
        toolbar.Children.Add(btnMoveDown);
        DockPanel.SetDock(toolbar, Dock.Top);
        leftPanel.Children.Add(toolbar);

        // 2. Bottom section of left panel (Orientation + Layout Settings)
        var bottomStack = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var orientationBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#555555")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8)
        };
        var orientationStack = new StackPanel { Spacing = 4 };
        orientationStack.Children.Add(new TextBlock { Text = SettingsPage.T("Layout Orientation"), FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 2) });
        var radioWrap = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        rbVertical = new RadioButton { Content = SettingsPage.T("Vertical"), IsChecked = request.Layout.Mode == LayoutMode.Vertical };
        rbHorizontal = new RadioButton { Content = SettingsPage.T("Horizontal"), IsChecked = request.Layout.Mode == LayoutMode.Horizontal };
        radioWrap.Children.Add(rbVertical);
        radioWrap.Children.Add(rbHorizontal);
        orientationStack.Children.Add(radioWrap);
        orientationBorder.Child = orientationStack;
        bottomStack.Children.Add(orientationBorder);

        btnLayoutSettings = new Button
        {
            Content = SettingsPage.T("Layout Settings..."),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        bottomStack.Children.Add(btnLayoutSettings);

        DockPanel.SetDock(bottomStack, Dock.Bottom);
        leftPanel.Children.Add(bottomStack);

        // 3. Components list
        listBox = new ListBox
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        leftPanel.Children.Add(listBox);

        Grid.SetColumn(leftPanel, 0);
        rootGrid.Children.Add(leftPanel);

        // ---------------- RIGHT PANEL ----------------
        var rightPanel = new DockPanel { LastChildFill = true };

        headerTitle = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            FontSize = 16,
            Margin = new Thickness(4, 0, 0, 8)
        };
        DockPanel.SetDock(headerTitle, Dock.Top);
        rightPanel.Children.Add(headerTitle);

        settingsHost = new ContentControl();
        settingsScrollViewer = new ScrollViewer
        {
            Content = settingsHost,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };

        var rightBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#444444")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            Child = settingsScrollViewer
        };
        rightPanel.Children.Add(rightBorder);

        Grid.SetColumn(rightPanel, 1);
        rootGrid.Children.Add(rightPanel);

        SetBody(rootGrid);

        AddButton(SettingsPage.T("OK"), DialogResult.OK, isDefault: true);
        AddButton(SettingsPage.T("Cancel"), DialogResult.Cancel, isCancel: true);

        // Build Add Component menu
        BuildAddMenu();

        // Wire event handlers
        btnAdd.Click += (_, _) => btnAdd.ContextMenu?.Open(btnAdd);

        btnRemove.Click += (_, _) =>
        {
            int idx = listBox.SelectedIndex;
            if (request.Layout.LayoutComponents.Count > 1 && idx >= 0 && idx < request.Layout.LayoutComponents.Count)
            {
                var layoutComp = request.Layout.LayoutComponents[idx];
                if (layoutComp.Component is IDeactivatableComponent deact)
                {
                    deact.Activated = false;
                }

                ComponentsToDispose.Add(layoutComp.Component);
                request.Layout.LayoutComponents.RemoveAt(idx);
                request.Layout.HasChanged = true;
                RefreshList();
                listBox.SelectedIndex = Math.Clamp(idx, 0, request.Layout.LayoutComponents.Count - 1);
                request.Host?.NotifyLayoutResized();
            }
        };

        btnMoveUp.Click += (_, _) =>
        {
            int idx = listBox.SelectedIndex;
            if (idx > 0)
            {
                var item = request.Layout.LayoutComponents[idx];
                request.Layout.LayoutComponents.RemoveAt(idx);
                request.Layout.LayoutComponents.Insert(idx - 1, item);
                request.Layout.HasChanged = true;
                RefreshList();
                listBox.SelectedIndex = idx - 1;
                request.Host?.NotifyLayoutResized();
            }
        };

        btnMoveDown.Click += (_, _) =>
        {
            int idx = listBox.SelectedIndex;
            if (idx >= 0 && idx < request.Layout.LayoutComponents.Count - 1)
            {
                var item = request.Layout.LayoutComponents[idx];
                request.Layout.LayoutComponents.RemoveAt(idx);
                request.Layout.LayoutComponents.Insert(idx + 1, item);
                request.Layout.HasChanged = true;
                RefreshList();
                listBox.SelectedIndex = idx + 1;
                request.Host?.NotifyLayoutResized();
            }
        };

        rbVertical.IsCheckedChanged += (_, _) =>
        {
            if (rbVertical.IsChecked == true && request.Layout.Mode != LayoutMode.Vertical)
            {
                request.Layout.Mode = LayoutMode.Vertical;
                request.Layout.HasChanged = true;
                request.Host?.NotifyOrientationSwitched();
                UpdateSettingsView();
            }
        };

        rbHorizontal.IsCheckedChanged += (_, _) =>
        {
            if (rbHorizontal.IsChecked == true && request.Layout.Mode != LayoutMode.Horizontal)
            {
                request.Layout.Mode = LayoutMode.Horizontal;
                request.Layout.HasChanged = true;
                request.Host?.NotifyOrientationSwitched();
                UpdateSettingsView();
            }
        };

        btnLayoutSettings.Click += async (_, _) =>
        {
            var oldSettings = (LayoutSettings)request.Layout.Settings.Clone();
            var currentComp = listBox.SelectedIndex >= 0 && listBox.SelectedIndex < request.Layout.LayoutComponents.Count
                ? request.Layout.LayoutComponents[listBox.SelectedIndex].Component
                : null;

            var dialog = new LayoutSettingsDialog(request.Layout.Settings, request.Layout, currentComp);
            var res = await dialog.ShowModalAsync(this);

            if (res == DialogResult.OK)
            {
                if (oldSettings.BackgroundImage != null && oldSettings.BackgroundImage != request.Layout.Settings.BackgroundImage)
                {
                    ImagesToDispose.Add(oldSettings.BackgroundImage);
                }
                request.Layout.HasChanged = true;
                request.Host?.NotifyLayoutSettingsAssigned();
            }
            else
            {
                if (request.Layout.Settings.BackgroundImage != null && oldSettings.BackgroundImage != request.Layout.Settings.BackgroundImage)
                {
                    request.Layout.Settings.BackgroundImage.Dispose();
                }
                request.Layout.Settings.Assign(oldSettings);
                request.Host?.NotifyLayoutSettingsAssigned();
            }

            UpdateSettingsView();
        };

        listBox.SelectionChanged += (_, _) =>
        {
            UpdateToolbarButtons();
            UpdateSettingsView();
        };

        listBox.DoubleTapped += async (_, _) =>
        {
            if (listBox.SelectedIndex >= 0 && listBox.SelectedIndex < request.Layout.LayoutComponents.Count)
            {
                var comp = request.Layout.LayoutComponents[listBox.SelectedIndex].Component;
                var oldSettings = (LayoutSettings)request.Layout.Settings.Clone();
                var dialog = new LayoutSettingsDialog(request.Layout.Settings, request.Layout, comp);
                var res = await dialog.ShowModalAsync(this);
                if (res == DialogResult.OK)
                {
                    if (oldSettings.BackgroundImage != null && oldSettings.BackgroundImage != request.Layout.Settings.BackgroundImage)
                    {
                        ImagesToDispose.Add(oldSettings.BackgroundImage);
                    }
                    request.Layout.HasChanged = true;
                    request.Host?.NotifyLayoutSettingsAssigned();
                }
                else
                {
                    request.Layout.Settings.Assign(oldSettings);
                    request.Host?.NotifyLayoutSettingsAssigned();
                }
                UpdateSettingsView();
            }
        };

        RefreshList();
        if (request.Layout.LayoutComponents.Count > 0)
        {
            listBox.SelectedIndex = 0;
        }
        UpdateToolbarButtons();
        UpdateSettingsView();
    }

    private void RefreshList()
    {
        listBox.ItemsSource = request.Layout.LayoutComponents
            .Select(c => c.Component.ComponentName)
            .ToArray();
    }

    private void UpdateToolbarButtons()
    {
        int idx = listBox.SelectedIndex;
        int count = request.Layout.LayoutComponents.Count;
        btnRemove.IsEnabled = count > 1 && idx >= 0;
        btnMoveUp.IsEnabled = idx > 0;
        btnMoveDown.IsEnabled = idx >= 0 && idx < count - 1;
    }

    private void UpdateSettingsView()
    {
        int idx = listBox.SelectedIndex;
        if (idx >= 0 && idx < request.Layout.LayoutComponents.Count)
        {
            var layoutComp = request.Layout.LayoutComponents[idx];
            var lc = layoutComp as LayoutComponent;
            var comp = layoutComp.Component;
            headerTitle.Text = comp.ComponentName;

            object[] fontAttr = comp.GetType().GetCustomAttributes(typeof(GlobalFontConsumerAttribute), true);
            GlobalFont usedFonts = fontAttr.Length > 0
                ? ((GlobalFontConsumerAttribute)fontAttr[0]).UsedGlobalFonts
                : GlobalFont.None;

            bool showFontPanel = lc != null && usedFonts != GlobalFont.None;
            Control settingsCtrl = null;
            try
            {
                settingsCtrl = ComponentSettingsViews.Create(comp, request.Layout.Mode);
            }
            catch { }

            if (showFontPanel && settingsCtrl != null)
            {
                var fontPanel = new FontOverridePanel();
                fontPanel.Bind(lc.FontOverrides, request.Layout.Settings, usedFonts);
                var stack = new StackPanel { Spacing = 8, Margin = new Thickness(4) };
                stack.Children.Add(fontPanel);
                stack.Children.Add(settingsCtrl);
                settingsHost.Content = stack;
            }
            else if (showFontPanel)
            {
                var fontPanel = new FontOverridePanel();
                fontPanel.Bind(lc.FontOverrides, request.Layout.Settings, usedFonts);
                settingsHost.Content = fontPanel;
            }
            else
            {
                settingsHost.Content = settingsCtrl ?? new TextBlock
                {
                    Text = SettingsPage.T("This component has no settings."),
                    Margin = new Thickness(12),
                    Foreground = new SolidColorBrush(Color.Parse("#888888"))
                };
            }
        }
        else
        {
            headerTitle.Text = string.Empty;
            settingsHost.Content = new TextBlock
            {
                Text = SettingsPage.T("Select a component to view and edit its settings."),
                Margin = new Thickness(12),
                Foreground = new SolidColorBrush(Color.Parse("#888888"))
            };
        }
    }

    private void BuildAddMenu()
    {
        var menu = new ContextMenu();
        IEnumerable<string> autosplitters = AutoSplitterFactory.Instance.AutoSplitters != null
            ? AutoSplitterFactory.Instance.AutoSplitters.Where(x => !x.Value.ShowInLayoutEditor).Select(x => x.Value.FileName)
            : [];

        if (ComponentManager.ComponentFactories != null)
        {
            var groups = ComponentManager.ComponentFactories
                .Where(x => !autosplitters.Contains(x.Key))
                .Select(x => x.Value)
                .GroupBy(x => x.Category, x => x)
                .OrderBy(x => x.Key);

            foreach (var group in groups)
            {
                var category = group.Key;
                IEnumerable<IComponentFactory> factories = group;
                if (category == ComponentCategory.Other)
                {
                    factories = factories.Prepend(new SeparatorFactory()).OrderBy(x => x.ComponentName);
                }

                var groupItem = new MenuItem { Header = SettingsPage.T(category.ToString()) };
                foreach (var factory in factories)
                {
                    var item = new MenuItem { Header = SettingsPage.T(factory.ComponentName) };
                    item.Click += (_, _) => AddComponent(factory);
                    groupItem.Items.Add(item);
                }
                menu.Items.Add(groupItem);
            }
        }

        menu.Items.Add(new Separator());
        var downloadMore = new MenuItem { Header = SettingsPage.T("Download More...") };
        downloadMore.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://livesplit.org/components/") { UseShellExecute = true });
            }
            catch { }
        };
        menu.Items.Add(downloadMore);

        btnAdd.ContextMenu = menu;
    }

    private void AddComponent(IComponentFactory factory)
    {
        try
        {
            KeyValuePair<string, IComponentFactory> componentFactory = ComponentManager.ComponentFactories != null
                ? ComponentManager.ComponentFactories.FirstOrDefault(x => x.Value.ComponentName == factory.ComponentName)
                : default;

            LayoutComponent component = componentFactory.Value == null
                ? new LayoutComponent("", new SeparatorComponent())
                : new LayoutComponent(componentFactory.Key, componentFactory.Value.Create(request.State));

            request.Layout.LayoutComponents.Add(component);
            request.Layout.HasChanged = true;
            RefreshList();
            listBox.SelectedIndex = request.Layout.LayoutComponents.Count - 1;
            request.Host?.NotifyLayoutResized();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }
}
