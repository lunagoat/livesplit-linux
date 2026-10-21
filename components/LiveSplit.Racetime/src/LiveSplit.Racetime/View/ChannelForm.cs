using LiveSplit.UI;
using LiveSplit.Hosting;
using LiveSplit.Racetime.Controller;
using LiveSplit.Racetime.Model;
using System;
using System.Windows.Forms;
namespace LiveSplit.Racetime.View;

// The host subscribes to RacetimeAPI.ChannelOpened and displays this state.
public sealed class ChannelForm : SettingsBase, IDisposable
{
    public RacetimeChannel Channel { get; }
    public bool AlwaysOnTop { get; }
    private string title;
    public string Title => title;
    public string PageUrl => Channel.Race == null ? null : Channel.FullWebRoot + Channel.Race.Id + "/livesplit";
    public string LoadingMessage { get; private set; } = "Loading...";
    public bool IsPageVisible { get; private set; }
    public ChannelForm(RacetimeChannel channel, string channelId, bool alwaysOnTop = true)
    {
        Channel = channel; AlwaysOnTop = alwaysOnTop;
        title = "Connecting to " + channelId[(channelId.IndexOf('/') + 1)..];
        Channel.RaceChanged += RaceChanged;
        Channel.Disconnected += Disconnected;
    }
    public void Connect(string id) => Channel.Connect(id);
    private void RaceChanged(object sender, EventArgs args)
    {
        title = $"{Channel.Race.Goal} [{Channel.Race.GameName}] - {Channel.Race.ChannelName}";
        OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(PageUrl));
    }
    private void Disconnected(object sender, EventArgs args) { title = "Disconnected"; OnPropertyChanged(nameof(Title)); }
    public void PageLoaded(bool success)
    {
        IsPageVisible = success; LoadingMessage = success ? "" : "Error loading page.";
        OnPropertyChanged(nameof(IsPageVisible)); OnPropertyChanged(nameof(LoadingMessage));
    }
    public bool RequestClose()
    {
        if (Channel.Race?.State == RaceState.Started && Channel.PersonalStatus == UserStatus.Racing)
        {
            var result = PlatformServices.Current.ShowMessage(this, "Do you want to FORFEIT before closing the window?", "", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (result == DialogResult.Cancel) return false;
            if (result == DialogResult.Yes) Channel.Forfeit();
        }
        Dispose(); return true;
    }
    public void Dispose()
    {
        Channel.RaceChanged -= RaceChanged; Channel.Disconnected -= Disconnected;
        Channel.RemoveRaceComparisons(); Channel.Disconnect();
    }
}
