using Avalonia.Controls;
using LiveSplit.Controls.Settings;
using LiveSplit.Services;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System.Xml;
using System.Windows.Forms;

namespace LiveSplit.View.Dialogs;

public class SettingsHostWindow : DialogWindow
{
    public Control SettingsView { get; }
    public SettingsHostWindow(string title, Control view) : base(title)
    {
        SettingsView = view;
        CanResize = true;
        SizeToContent = SizeToContent.Manual;
        Width = 640;
        Height = 720;
        MinWidth = 560;
        MinHeight = 250;
        SetBody(new ScrollViewer { Content = view, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        AddButton(SettingsPage.T("OK"), DialogResult.OK, isDefault: true);
        AddButton(SettingsPage.T("Cancel"), DialogResult.Cancel, isCancel: true);
    }
}

public sealed class ComponentSettingsDialog : SettingsHostWindow
{
    public XmlNode ComponentSettings { get; }
    public IComponent Component { get; }
    public ComponentSettingsDialog(IComponent component, LayoutMode mode = LayoutMode.Vertical)
        : base(component.ComponentName + " " + SettingsPage.T("Settings"), ComponentSettingsViews.Create(component, mode))
    {
        Component = component;
        ComponentSettings = component.GetSettings(new XmlDocument());
        Closed += (_, _) => { if (Result != DialogResult.OK) Component.SetSettings(ComponentSettings); };
    }
}
