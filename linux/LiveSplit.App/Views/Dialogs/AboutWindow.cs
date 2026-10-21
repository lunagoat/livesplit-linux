using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplit.Hosting;
using LiveSplit.Updates;
using System.Windows.Forms;

namespace LiveSplit.View.Dialogs;

public sealed class AboutWindow : DialogWindow
{
    public AboutWindow()
        : base("About LiveSplit")
    {
        string productName = "LiveSplit";
        if (Git.Branch is not null and not "master" and not "HEAD")
        {
            productName = $"LiveSplit ({Git.Branch})";
        }

        var body = new StackPanel { Spacing = 6, MinWidth = 300 };
        body.Children.Add(new TextBlock { Text = productName, FontSize = 20, FontWeight = FontWeight.SemiBold });

        Button version = LinkButton(Git.Version ?? "Unknown Version", Git.RevisionUri?.AbsoluteUri);
        body.Children.Add(version);
        body.Children.Add(LinkButton("livesplit.org", "https://livesplit.org"));
        body.Children.Add(new TextBlock { Text = "Made by:", Margin = new Thickness(0, 8, 0, 0) });
        body.Children.Add(LinkButton("CryZe", "https://twitter.com/CryZe107"));
        body.Children.Add(LinkButton("wooferzfg", "https://twitter.com/wooferzfg"));
        body.Children.Add(new TextBlock { Text = "Linux port: Avalonia + SkiaSharp", Margin = new Thickness(0, 8, 0, 0), Opacity = 0.7 });
        SetBody(body);

        AddButton("OK", DialogResult.OK, isDefault: true, isCancel: true);
    }

    private static Button LinkButton(string text, string url)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.Parse("#4DA3FF")),
            HorizontalAlignment = HorizontalAlignment.Left,
            Cursor = url != null ? new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) : null,
            IsHitTestVisible = url != null,
        };
        if (url != null)
        {
            button.Click += (_, _) => PlatformServices.OpenUrl(url);
        }

        return button;
    }
}
