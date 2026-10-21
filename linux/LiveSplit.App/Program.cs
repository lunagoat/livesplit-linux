using Avalonia;
using LiveSplit.Hosting;
using LiveSplit.Localization;
using LiveSplit.Options;
using LiveSplit.Services;
using System;
using System.IO;

namespace LiveSplit;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        CommandLineOptions options = CommandLineOptions.Parse(args);
        if (options.ShowHelp || options.Errors.Count > 0)
        {
            foreach (string error in options.Errors)
            {
                Console.Error.WriteLine(error);
            }

            Console.WriteLine(CommandLineOptions.Usage);
            return options.Errors.Count > 0 ? 2 : 0;
        }

        if (!string.IsNullOrEmpty(options.ConfigDirectory))
        {
            string config = Path.GetFullPath(options.ConfigDirectory);
            Paths.SetPortable(false);
            Paths.SetDirectories(config, Path.Combine(config, "data"), Path.Combine(config, "cache"));
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                Log.Error(exception);
            }
        };

        try
        {
            UiTextCatalog.Initialize(AppContext.BaseDirectory);
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        try
        {
            return BuildAvaloniaApp(options).StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnMainWindowClose);
        }
        catch (Exception e)
        {
            Log.Error(e);
            string message = string.Format(
                UiLocalizer.TranslateKey(LocalizationKeys.CrashReason, "LiveSplit has crashed due to the following reason:\n\n{0}"),
                e.Message);
            Console.Error.WriteLine(message);
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp(CommandLineOptions options)
    {
        return AppBuilder.Configure(() => new App { Options = options })
            .UsePlatformDetect()
            .With(new X11PlatformOptions
            {
                WmClass = "LiveSplit",
            })
            .LogToTrace();
    }
}
