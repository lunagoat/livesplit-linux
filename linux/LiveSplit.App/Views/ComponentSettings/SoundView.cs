using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using System.Collections;
using System.Linq;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class SoundView : SettingsPage
{
    public SoundView(object model) : base(model)
    {
        var files = new SettingsPage(model);
        files.Add(files.Section("Drag and Drop Mode:", files.Radios("IsClearAddDragDrop", ("Clear and Add Files", "True"), ("Append Files", "False"))));
        var fileRows = new ItemsControl { ItemTemplate = new FuncDataTemplate<object>((pair, _) => new SoundFileView(pair.GetType().GetProperty("Value").GetValue(pair))) };
        files.Bind(fileRows, ItemsControl.ItemsSourceProperty, "FileSettings", BindingMode.OneWay);
        files.Add(fileRows);
        var mixer = new SettingsPage(model);
        var device = new ComboBox { ItemsSource = new[] { "Default output" }, SelectedIndex = 0, IsEnabled = false };
        mixer.Add(mixer.Row("Output Device:", device, "IsOutputDeviceEnabled"), mixer.Slider("General Volume:", "GeneralVolume", 0, 100, volume: true));
        var volumeRows = new ItemsControl { ItemTemplate = new FuncDataTemplate<object>((pair, _) => new SoundVolumeView(pair.GetType().GetProperty("Value").GetValue(pair))) };
        mixer.Bind(volumeRows, ItemsControl.ItemsSourceProperty, "VolumeSettings", BindingMode.OneWay);
        mixer.Add(volumeRows);
        Add(Tabs(("Sound Files", files), ("Volume Mixer", mixer)));
    }
}

public sealed class SoundFileView : SettingsPage
{
    public SoundFileView(object model) : base(model)
    {
        MinWidth = 0;
        Add(Row("", MediaFiles.Editor(this, "FilePaths", audio: true), labelProperty: "Label"));
    }
}

public sealed class SoundVolumeView : SettingsPage
{
    public SoundVolumeView(object model) : base(model)
    {
        MinWidth = 0;
        Add(Slider(Read("Label").ToString(), "Volume", 0, 100, volume: true));
    }
}
