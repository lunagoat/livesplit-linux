using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Hosting;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace LiveSplit.Web.Share;

public class Twitter : IRunUploadPlatform
{
    public ISettings Settings { get; set; }

    protected static readonly Twitter _Instance = new();
    public static Twitter Instance => _Instance;

    public static readonly Uri BaseUri = new("https://twitter.com/intent/tweet");

    protected Twitter() { }

    public string PlatformName => "X (Twitter)";

    public string Description => """
        X (Twitter) allows you to share your run with the world.
        When sharing, a screenshot of LiveSplit is automatically copied to the clipboard.
        When you click share, LiveSplit opens a Tweet composition window in your default browser.
        """;

    public bool VerifyLogin()
    {
        return true;
    }

    public bool SubmitRun(IRun run, Func<Image> screenShotFunction = null, TimingMethod method = TimingMethod.RealTime, string comment = "", params string[] additionalParams)
    {
        ImageToClipboard(screenShotFunction());
        string uri = MakeUri(comment);
        PlatformServices.OpenUrl(uri);

        return true;
    }

    private void ImageToClipboard(Image image)
    {
        if (image is null)
        {
            return;
        }

        try
        {
            PlatformServices.Current.SetClipboardImage(image);
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }
    private string MakeUri(string text)
    {
        string intentText = "";
        if (!string.IsNullOrEmpty(text))
        {
            intentText = "?text=" + Uri.EscapeDataString(text);
        }

        return BaseUri + intentText;
    }
}
