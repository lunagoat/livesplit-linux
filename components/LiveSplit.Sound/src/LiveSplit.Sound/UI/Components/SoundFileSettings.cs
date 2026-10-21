using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.UI.Components;

public class SoundFileSettings : SettingsBase
{
    private SoundData data;
    private bool isClearAddDragDrop = true;
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
            NotifyPaths();
        }
    }
    public IList<string> FilePaths { get => Data.FilePaths; set => Data.FilePaths = value; }
    public string FilePathsText => string.Join(", ", FilePaths);
    public string PathsTooltip => string.Join("\n", FilePaths);
    public bool IsClearAddDragDrop { get => isClearAddDragDrop; set => SetField(ref isClearAddDragDrop, value); }
    public SoundFileSettings(EventType eventType, SoundData data)
    {
        Label = eventType.GetName();
        Data = data;
    }
    private void DataChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SoundData.FilePaths)) NotifyPaths();
    }
    private void NotifyPaths()
    {
        OnPropertyChanged(nameof(FilePaths));
        OnPropertyChanged(nameof(FilePathsText));
        OnPropertyChanged(nameof(PathsTooltip));
    }
    public void DropFiles(IEnumerable<string> files) => FilePaths = IsClearAddDragDrop ? files.ToArray() : FilePaths.Concat(files).Distinct().ToArray();
    public void Clear() => FilePaths = [];
}
