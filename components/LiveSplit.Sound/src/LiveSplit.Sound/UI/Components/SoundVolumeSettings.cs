namespace LiveSplit.UI.Components;

public class SoundVolumeSettings : SettingsBase
{
    private SoundData data;
    public string Label { get; }
    public SoundData Data
    {
        get => data;
        set
        {
            if (ReferenceEquals(data, value)) return;
            if (data != null) data.PropertyChanged -= DataChanged;
            SetField(ref data, value);
            if (data != null) data.PropertyChanged += DataChanged;
            OnPropertyChanged(nameof(Volume));
        }
    }
    public int Volume { get => Data.Volume; set => Data.Volume = value; }
    public SoundVolumeSettings(EventType eventType, SoundData data)
    {
        Label = eventType.GetName();
        Data = data;
    }
    private void DataChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SoundData.Volume)) OnPropertyChanged(nameof(Volume));
    }
}
