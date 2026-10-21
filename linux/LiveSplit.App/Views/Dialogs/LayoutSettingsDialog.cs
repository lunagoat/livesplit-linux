using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplit.Controls.Settings;
using LiveSplit.Options;
using LiveSplit.Services;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml;
using Image = System.Drawing.Image;

namespace LiveSplit.View.Dialogs;

public sealed class LayoutSettingsDialog : DialogWindow
{
    public LayoutSettings Settings { get; }
    public ILayout Layout { get; }
    public LayoutSettings OriginalSettingsSnapshot { get; }

    public List<XmlNode> ComponentSettings { get; } = [];
    public List<IComponent> Components { get; } = [];
    public List<FontOverrides> FontOverrideSnapshots { get; } = [];
    public List<LayoutComponent> LayoutComponents { get; } = [];

    private readonly TabControl tabControl;
    private readonly LayoutSettingsControl layoutControl;

    public LayoutSettingsDialog(LayoutSettings settings, ILayout layout, IComponent tabComponent = null)
        : base(SettingsPage.T("Layout Settings"))
    {
        settings ??= layout?.Settings ?? new Options.SettingsFactories.StandardLayoutSettingsFactory().Create();
        if (layout != null && layout.Settings == null)
        {
            layout.Settings = settings;
        }

        Settings = settings;
        Layout = layout;
        OriginalSettingsSnapshot = (LayoutSettings)settings.Clone();

        Width = 860;
        Height = 700;
        MinWidth = 640;
        MinHeight = 440;
        CanResize = true;

        tabControl = new TabControl();

        // 1. "Layout" Tab
        layoutControl = new LayoutSettingsControl(settings, layout);
        var layoutTab = new TabItem
        {
            Header = SettingsPage.T("Layout"),
            Content = layoutControl
        };
        tabControl.Items.Add(layoutTab);

        // 2. Component Tabs
        TabItem selectedTab = null;

        foreach (ILayoutComponent layoutComponent in layout.LayoutComponents)
        {
            IComponent component = layoutComponent.Component;
            var lc = layoutComponent as LayoutComponent;

            Control settingsControl = null;
            try
            {
                settingsControl = ComponentSettingsViews.Create(component, layout.Mode);
            }
            catch { }

            object[] fontAttr = component.GetType().GetCustomAttributes(typeof(GlobalFontConsumerAttribute), true);
            GlobalFont usedFonts = fontAttr.Length > 0
                ? ((GlobalFontConsumerAttribute)fontAttr[0]).UsedGlobalFonts
                : GlobalFont.None;

            bool hasFontOverrides = lc != null && usedFonts != GlobalFont.None;
            bool hasSettings = settingsControl != null;

            if (hasSettings || hasFontOverrides)
            {
                ComponentSettings.Add(component.GetSettings(new XmlDocument()));
                Components.Add(component);

                if (lc != null)
                {
                    FontOverrideSnapshots.Add((FontOverrides)lc.FontOverrides?.Clone());
                    LayoutComponents.Add(lc);
                }

                Control tabBody;
                if (hasFontOverrides && hasSettings)
                {
                    var fontPanel = new FontOverridePanel();
                    fontPanel.Bind(lc.FontOverrides, settings, usedFonts);

                    var stack = new StackPanel { Spacing = 8, Margin = new Thickness(4) };
                    stack.Children.Add(fontPanel);
                    stack.Children.Add(settingsControl);

                    tabBody = new ScrollViewer
                    {
                        Content = stack,
                        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
                    };
                }
                else if (hasFontOverrides)
                {
                    var fontPanel = new FontOverridePanel();
                    fontPanel.Bind(lc.FontOverrides, settings, usedFonts);
                    tabBody = new ScrollViewer
                    {
                        Content = fontPanel,
                        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
                    };
                }
                else
                {
                    tabBody = new ScrollViewer
                    {
                        Content = settingsControl,
                        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
                    };
                }

                var compTab = new TabItem
                {
                    Header = component.ComponentName,
                    Content = tabBody
                };
                tabControl.Items.Add(compTab);

                if (component == tabComponent)
                {
                    selectedTab = compTab;
                }
            }
        }

        if (selectedTab != null)
        {
            tabControl.SelectedItem = selectedTab;
        }

        SetBody(tabControl);

        AddButton(SettingsPage.T("OK"), DialogResult.OK, isDefault: true);
        AddButton(SettingsPage.T("Cancel"), DialogResult.Cancel, isCancel: true);

        Closed += (_, _) =>
        {
            if (Result != DialogResult.OK)
            {
                // Revert component XML settings
                for (int i = 0; i < Components.Count; i++)
                {
                    try
                    {
                        Components[i].SetSettings(ComponentSettings[i]);
                    }
                    catch { }
                }

                // Revert font overrides
                for (int i = 0; i < LayoutComponents.Count; i++)
                {
                    if (i < FontOverrideSnapshots.Count && FontOverrideSnapshots[i] != null)
                    {
                        LayoutComponents[i].FontOverrides = FontOverrideSnapshots[i];
                    }
                }

                // Dispose newly loaded background image if changed
                if (Settings.BackgroundImage != null && Settings.BackgroundImage != OriginalSettingsSnapshot.BackgroundImage)
                {
                    Settings.BackgroundImage.Dispose();
                }

                // Revert layout settings
                Settings.Assign(OriginalSettingsSnapshot);
            }
        };
    }
}
