using LiveSplit.Hosting;
using LiveSplit.UI;

namespace LiveSplit.Web.Share;

public class SpeedrunComApiKeyPrompt : ISpeedrunComAuthenticator
{
    public string GetAccessToken()
    {
        PlatformServices.OpenUrl("https://www.speedrun.com/settings/api");

        string accessToken = null;
        InputBox.Show("Speedrun.com Authentication", "Enter your Speedrun.com API Key:", ref accessToken);
        return accessToken;
    }
}
