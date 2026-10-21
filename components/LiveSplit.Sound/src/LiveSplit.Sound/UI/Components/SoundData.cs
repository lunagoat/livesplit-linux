using System.Collections.Generic;

namespace LiveSplit.UI.Components;

public class SoundData : SettingsBase
{
    private IList<string> filePaths;
    private int volume;
    public IList<string> FilePaths { get => filePaths; set => SetField(ref filePaths, value); }
    public int Volume { get => volume; set => SetField(ref volume, value); }
    public SoundData(IList<string> filePaths, int volume)
    {
        FilePaths = filePaths;
        Volume = volume;
    }
}
