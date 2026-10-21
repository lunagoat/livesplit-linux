using LiveSplit.Model;
using LiveSplit.Hosting;
using System.Text;

namespace LiveSplit.Web.SRL.RaceViewers;

public class Kadgar : IRaceViewer
{
    public void ShowRace(IRaceInfo race)
    {
        var builder = new StringBuilder();
        builder.Append("http://kadgar.net/live/");
        foreach (string stream in race.LiveStreams)
        {
            builder.Append(stream);
            builder.Append(",");
        }

        builder.Length -= 1;
        PlatformServices.OpenUrl(builder.ToString());
    }

    public string Name => "Kadgar";
}
