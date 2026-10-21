using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LiveSplit.Options;
using LiveSplit.Services;
using LiveSplit.View;
using System;
using System.Drawing.Imaging;
using System.IO;

namespace LiveSplit;

public partial class App : Application
{
    public CommandLineOptions Options { get; internal set; } = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            PlatformServices_Install();

            var window = new MainWindow(Options);
            if (Options.ScreenshotPath != null)
            {
                // Nothing is shown and no loop is needed: render, write and leave.
                int exitCode = SaveScreenshot(window, Options);
                Environment.Exit(exitCode);
            }
            else
            {
                desktop.MainWindow = window;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void PlatformServices_Install()
    {
        LiveSplit.Hosting.PlatformServices.Current = new AvaloniaPlatformServices();
    }

    private static int SaveScreenshot(MainWindow window, CommandLineOptions options)
    {
        try
        {
            using System.Drawing.Image image = window.RenderSettledScreenShot(options.ScreenshotDemo);
            string directory = Path.GetDirectoryName(Path.GetFullPath(options.ScreenshotPath));
            Directory.CreateDirectory(directory);
            image.Save(options.ScreenshotPath, ImageFormat.Png);
            Console.Error.WriteLine($"Wrote {options.ScreenshotPath} ({image.Width}x{image.Height})");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            Console.Error.WriteLine("Screenshot failed: " + ex);
            return 1;
        }
    }
}
