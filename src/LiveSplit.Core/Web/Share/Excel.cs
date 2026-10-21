using LiveSplit.Model;
using LiveSplit.Model.RunSavers;
using LiveSplit.Options;
using LiveSplit.Hosting;
using System;
using System.Drawing;
using System.IO;

namespace LiveSplit.Web.Share;

public class Excel : IRunUploadPlatform
{
    protected static Excel _Instance = new();

    public static Excel Instance => _Instance;

    public string PlatformName => "Excel";

    public string Description => """
        Export your splits as an Excel Sheet to analyze your splits.
        This includes your whole history of all the runs you ever did.
        """;

    public ISettings Settings { get; set; }

    protected Excel() { }

    #region Not supported

    bool IRunUploadPlatform.VerifyLogin()
    {
        return true;
    }

    #endregion

    public bool SubmitRun(IRun run, Func<Image> screenShotFunction = null, TimingMethod method = TimingMethod.RealTime, string comment = "", params string[] additionalParams)
    {
        string path = PlatformServices.Current.ChooseSaveFile(null, "Export Excel Sheet", "Excel Sheet (*.xlsx)|*.xlsx", "splits.xlsx");
        if (path != null)
        {
            if (!File.Exists(path))
            {
                File.Create(path).Close();
            }

            using FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write);
            var runSaver = new ExcelRunSaver();
            runSaver.Save(run, stream);
            return true;
        }

        return false;
    }
}
