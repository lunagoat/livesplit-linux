using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Hosting;
using System;

namespace LiveSplit.Web.Share;

public class Screenshot : IRunUploadPlatform
{
    public ISettings Settings { get; set; }
    protected static readonly Screenshot _Instance = new();

    public static Screenshot Instance => _Instance;

    protected Screenshot() { }

    public string PlatformName => "Screenshot";

    public string Description => "Sharing will save a screenshot of LiveSplit.";

    public bool VerifyLogin()
    {
        return true;
    }

    public bool SubmitRun(IRun run, Func<System.Drawing.Image> screenShotFunction = null, TimingMethod method = TimingMethod.RealTime, string comment = "", params string[] additionalParams)
    {
        try
        {
            System.Drawing.Image image = screenShotFunction();

            string path = PlatformServices.Current.ChooseSaveFile(null, "Save Screenshot", "PNG (*.png)|*.png|JPEG (*.jpeg)|*.jpeg|GIF (*.gif)|*.gif|Bitmap (*.bmp)|*.bmp|TIFF (*.tiff)|*.tiff", "screenshot.png");
            if (path != null)
            {
                image.Save(path);
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        return false;
    }
}
