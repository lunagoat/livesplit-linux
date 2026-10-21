using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class RacetimeSettingsView : SettingsPage
{
    public RacetimeSettingsView(object model) : base(ResolveModel(model))
    {
        string message = Read("Message")?.ToString() ?? "There are no options available for this racing service.";
        var text = new TextBlock
        {
            Text = T(message),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Foreground = Brushes.Gray,
            Margin = new Thickness(20, 40)
        };
        Add(text);
    }

    private static object ResolveModel(object model)
    {
        if (model != null && model.GetType().Name == "RacetimeSettings")
        {
            return model.GetType().GetMethod("GetSettingsControl")?.Invoke(model, null) ?? model;
        }
        return model;
    }
}
