using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using LiveSplit;
using System.Runtime.CompilerServices;

[assembly: AvaloniaTestApplication(typeof(LiveSplit.AppTests.TestApp))]

namespace LiveSplit.AppTests;

public class TestApp : Application
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    public override void Initialize()
    {
        LiveSplitCoreFactory.LoadLiveSplitCore();
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    }
}

internal static class TestInit
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        LiveSplitCoreFactory.LoadLiveSplitCore();
    }
}
