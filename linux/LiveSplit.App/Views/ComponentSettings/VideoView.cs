using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class VideoView : SettingsPage
{
    public VideoView(object model) : base(model)
    {
        Add(Row("Video Path:", MediaFiles.Editor(this, "VideoPath")), Text("Run Starts At:", "OffsetString", validation: true), Slider("", "SizeValue", 100, 400, size: true));
    }
}
