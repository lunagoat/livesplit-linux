using LiveSplit.Hosting;
using System;
using System.IO;
using System.Text.Json;

namespace LiveSplit.Web.Share;

public sealed class ShareSettings
{
    private static readonly object SyncRoot = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static ShareSettings Default { get; } = Load();

    public static string FilePath => Path.Combine(Paths.ConfigDirectory, "share-settings.json");

    public string TwitterFormat { get; set; } = "";
    public string TwitchFormat { get; set; } = "";
    public string TwitterFormatRunning { get; set; } = "";
    public string BlueskyFormat { get; set; } = "";
    public string BlueskyFormatRunning { get; set; } = "";

    private static ShareSettings Load()
    {
        var settings = new ShareSettings();
        settings.Reload();
        return settings;
    }

    public void Reload()
    {
        lock (SyncRoot)
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    ShareSettings loaded = JsonSerializer.Deserialize<ShareSettings>(File.ReadAllText(FilePath));
                    if (loaded != null)
                    {
                        TwitterFormat = loaded.TwitterFormat ?? "";
                        TwitchFormat = loaded.TwitchFormat ?? "";
                        TwitterFormatRunning = loaded.TwitterFormatRunning ?? "";
                        BlueskyFormat = loaded.BlueskyFormat ?? "";
                        BlueskyFormatRunning = loaded.BlueskyFormatRunning ?? "";
                    }
                }
            }
            catch (Exception ex)
            {
                Options.Log.Error(ex);
            }
        }
    }

    public void Save()
    {
        lock (SyncRoot)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch (Exception ex)
            {
                Options.Log.Error(ex);
            }
        }
    }
}
