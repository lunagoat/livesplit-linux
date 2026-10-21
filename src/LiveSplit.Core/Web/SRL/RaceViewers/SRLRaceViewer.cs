using LiveSplit.Model;
using LiveSplit.Hosting;

namespace LiveSplit.Web.SRL.RaceViewers;

public class SRLRaceViewer : IRaceViewer
{
    public void ShowRace(IRaceInfo race)
    {
        string url = string.Format("http://speedrunslive.com/race/{0}", race.Id);
        PlatformServices.OpenUrl(url);
    }

    public string Name => "SpeedRunsLive";
}
