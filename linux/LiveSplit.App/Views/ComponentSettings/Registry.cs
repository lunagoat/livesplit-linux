using Avalonia.Controls;
using LiveSplit.Services;
using LiveSplit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace LiveSplit.View.ComponentSettings;

public sealed class Registry : IComponentSettingsViewFactory
{
    private readonly Dictionary<Type, Func<object, Control>> views = [];
    public static Registry Default { get; } = new();
    public IReadOnlyCollection<Type> SettingsTypes => views.Keys;
    public IReadOnlyCollection<Assembly> ComponentAssemblies { get; }

    public Registry()
    {
        var assemblies = new List<Assembly>();
        void Add(string component, string name, Func<object, Control> create, string ns = "LiveSplit.UI.Components")
        {
            string assemblyName = "LiveSplit." + component;
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (assembly == null)
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Components", assemblyName + ".dll");
                assembly = File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : Assembly.Load(assemblyName);
            }
            if (!assemblies.Contains(assembly)) assemblies.Add(assembly);
            views.Add(assembly.GetType(ns + "." + name, throwOnError: true), create);
        }
        Add("Timer", "TimerSettings", m => new TimerView(m));
        Add("Title", "TitleSettings", m => new TitleView(m));
        Add("Text", "TextComponentSettings", m => new TextView(m));
        Add("BlankSpace", "BlankSpaceSettings", m => new BlankSpaceView(m));
        Add("Splits", "SplitsSettings", m => new SplitsView(m));
        Add("Splits", "ColumnSettings", m => new ColumnView(m));
        Add("Subsplits", "SplitsSettings", m => new SubsplitsView(m));
        Add("Subsplits", "ColumnSettings", m => new ColumnView(m));
        Add("Delta", "DeltaSettings", m => new DeltaView(m));
        Add("ComparisonTime", "ComparisonTimeSettings", m => new ComparisonTimeView(m));
        Add("CurrentComparison", "CurrentComparisonSettings", m => new CurrentComparisonView(m));
        Add("PreviousSegment", "PreviousSegmentSettings", m => new PreviousSegmentView(m));
        Add("PossibleTimeSave", "PossibleTimeSaveSettings", m => new PossibleTimeSaveView(m));
        Add("SumOfBest", "SumOfBestSettings", m => new SumOfBestView(m));
        Add("RunPrediction", "RunPredictionSettings", m => new RunPredictionView(m));
        Add("TotalPlaytime", "TotalPlaytimeSettings", m => new TotalPlaytimeView(m));
        Add("WorldRecord", "WorldRecordSettings", m => new WorldRecordView(m));
        Add("DetailedTimer", "DetailedTimerSettings", m => new DetailedTimerView(m));
        Add("Graph", "GraphSettings", m => new GraphView(m));
        Add("Counter", "CounterComponentSettings", m => new CounterView(m));
        Add("HotkeyIndicator", "HotkeyIndicatorSettings", m => new HotkeyIndicatorView(m));
        Add("ManualGameTime", "ManualGameTimeSettings", m => new ManualGameTimeView(m));
        Add("Sound", "SoundSettings", m => new SoundView(m));
        Add("Sound", "SoundFileSettings", m => new SoundFileView(m));
        Add("Sound", "SoundVolumeSettings", m => new SoundVolumeView(m));
        Add("Video", "VideoSettings", m => new VideoView(m), "LiveSplit.Video");
        ComponentAssemblies = assemblies;
    }

    public bool CanCreate(object settings) => settings != null && views.ContainsKey(settings.GetType());
    public bool HasView(Type type) => views.ContainsKey(type);
    public Control Create(object settings, LayoutMode mode)
    {
        var type = settings.GetType();
        type.GetProperty("Mode")?.SetValue(settings, mode);
        type.GetMethod("PrepareForDisplay", Type.EmptyTypes)?.Invoke(settings, null);
        return views.TryGetValue(type, out var create) ? create(settings) : throw new ArgumentException($"No settings view for {type.AssemblyQualifiedName}", nameof(settings));
    }
}
