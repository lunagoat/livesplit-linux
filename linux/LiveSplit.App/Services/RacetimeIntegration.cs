using Avalonia.Threading;
using LiveSplit.Hosting;
using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.View;
using LiveSplit.View.Dialogs;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace LiveSplit.Services;

public sealed class RacetimeRaceDialogLauncher : IRaceDialogLauncher
{
    public void JoinRace(MainWindow host, ITimerModel model, string raceId)
    {
        RacetimeIntegration.JoinRace(model, raceId);
    }

    public void NewRace(MainWindow host, ITimerModel model)
    {
        RacetimeIntegration.NewRace(model);
    }
}

public static class RacetimeIntegration
{
    private static bool initialized;
    private static readonly object sync = new();

    public static void Initialize()
    {
        lock (sync)
        {
            if (initialized) return;
            initialized = true;

            HookChannelOpened();
            AppServices.RaceDialog = new RacetimeRaceDialogLauncher();
        }
    }

    private static object GetRacetimeApi()
    {
        string assemblyName = "LiveSplit.Racetime";
        var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
        if (assembly == null)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Components", assemblyName + ".dll");
            if (File.Exists(path))
            {
                assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            }
        }
        if (assembly == null) return null;

        var apiType = assembly.GetType("LiveSplit.Racetime.RacetimeAPI");
        var instanceProp = apiType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        return instanceProp?.GetValue(null);
    }

    private static void HookChannelOpened()
    {
        try
        {
            var api = GetRacetimeApi();
            if (api == null) return;

            var evt = api.GetType().GetEvent("ChannelOpened", BindingFlags.Public | BindingFlags.Instance);
            if (evt == null) return;

            var handler = typeof(RacetimeIntegration).GetMethod(nameof(OnChannelOpened), BindingFlags.NonPublic | BindingFlags.Static);
            var del = Delegate.CreateDelegate(evt.EventHandlerType, handler);
            evt.AddEventHandler(api, del);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private static void OnChannelOpened(object channelForm)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var window = new RacetimeChannelWindow(channelForm);
            window.Show();
        });
    }

    public static void JoinRace(ITimerModel model, string raceId)
    {
        Initialize();
        var api = GetRacetimeApi();
        api?.GetType().GetMethod("Join")?.Invoke(api, [model, raceId]);
    }

    public static void NewRace(ITimerModel model)
    {
        Initialize();
        var api = GetRacetimeApi();
        api?.GetType().GetMethod("Create")?.Invoke(api, [model]);
    }
}
