using LiveSplit.Hosting;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
namespace LiveSplit.UI.Components;

public class CollectorSettings : SettingsBase
{
    private readonly HttpClient client;
    private readonly string keyFile;
    public CollectorSettings() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }) { }
    public CollectorSettings(HttpClient client, string keyFile = null)
    {
        this.client = client;
        this.keyFile = keyFile ?? System.IO.Path.Combine(Paths.ConfigDirectory, "TheRun", "uploadkey.txt");
        if (File.Exists(this.keyFile)) UploadKey = File.ReadAllText(this.keyFile).Trim();
    }
    private LayoutMode mode = default;
    public LayoutMode Mode { get => mode; set => SetField(ref mode, value); }
    private string path = "";
    public string Path { get => path; set => SetField(ref path, value); }
    private bool isStatsUploadingEnabled = true;
    public bool IsStatsUploadingEnabled { get => isStatsUploadingEnabled; set => SetField(ref isStatsUploadingEnabled, value); }
    private bool isUploadOnResetEnabled = true;
    public bool IsUploadOnResetEnabled { get => isUploadOnResetEnabled; set => SetField(ref isUploadOnResetEnabled, value); }
    private bool isLiveTrackingEnabled = true;
    public bool IsLiveTrackingEnabled { get => isLiveTrackingEnabled; set => SetField(ref isLiveTrackingEnabled, value); }
    private bool isLayoutPathUploadEnabled = false;
    public bool IsLayoutPathUploadEnabled { get => isLayoutPathUploadEnabled; set => SetField(ref isLayoutPathUploadEnabled, value); }
    private string uploadKey = "";
    public string UploadKey { get => uploadKey; set => SetField(ref uploadKey, value); }

    public enum ConnectionStatus { None, Validating, Connected, Error }
    private ConnectionStatus status;
    public ConnectionStatus Status => status;
    public string ValidatedUsername { get; private set; }
    public string PictureUrl { get; private set; }
    public string StatusMessage { get; private set; }
    public bool CanValidate => Status != ConnectionStatus.Validating;
    public string TestLabel => CanValidate ? "Test" : "...";
    public string ProfileUrl => ValidatedUsername == null ? null : "https://therun.gg/" + Uri.EscapeDataString(ValidatedUsername);
    public string LiveUrl => ValidatedUsername == null ? null : "https://therun.gg/live/" + Uri.EscapeDataString(ValidatedUsername);
    private void SetStatus(ConnectionStatus value)
    {
        status = value;
        foreach (string name in new[] { nameof(Status), nameof(CanValidate), nameof(TestLabel), nameof(ValidatedUsername), nameof(PictureUrl), nameof(StatusMessage), nameof(ProfileUrl), nameof(LiveUrl) }) OnPropertyChanged(name);
    }
    public void SaveUploadKey()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(keyFile));
        File.WriteAllText(keyFile, UploadKey);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(keyFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Path = "";
    }
    public async Task ValidateKeyAsync(CancellationToken token = default)
    {
        if (!CanValidate) return;
        ValidatedUsername = PictureUrl = StatusMessage = null;
        if (string.IsNullOrWhiteSpace(UploadKey)) { SetStatus(ConnectionStatus.None); return; }
        SetStatus(ConnectionStatus.Validating);
        try
        {
            using var response = await client.GetAsync("https://api.therun.gg/users/uploadKey/validate/" + Uri.EscapeDataString(UploadKey), token);
            if (!response.IsSuccessStatusCode) { StatusMessage = "Invalid upload key."; SetStatus(ConnectionStatus.Error); return; }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            var data = json.RootElement.GetProperty("result").GetProperty("data");
            ValidatedUsername = data.GetProperty("username").GetString();
            PictureUrl = data.TryGetProperty("picture", out var picture) ? picture.GetString() : null;
            SetStatus(ConnectionStatus.Connected);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { SetStatus(ConnectionStatus.None); throw; }
        catch { StatusMessage = "Could not reach therun.gg."; SetStatus(ConnectionStatus.Error); }
    }
    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        Path = SettingsHelper.ParseString(element["Path"], "");
        if (!string.IsNullOrEmpty(Path)) { UploadKey = Path; SaveUploadKey(); }
        IsStatsUploadingEnabled = SettingsHelper.ParseBool(element["IsStatsUploadingEnabled"], true);
        IsUploadOnResetEnabled = SettingsHelper.ParseBool(element["IsUploadOnResetEnabled"], true);
        IsLiveTrackingEnabled = SettingsHelper.ParseBool(element["IsLiveTrackingEnabled"], true);
        IsLayoutPathUploadEnabled = SettingsHelper.ParseBool(element["IsLayoutPathUploadEnabled"], false);
    }
    public XmlNode GetSettings(XmlDocument document)
    {
        XmlElement parent = document.CreateElement("Settings");
        CreateSettingsNode(document, parent);
        return parent;
    }

    public int GetSettingsHashCode()
    {
        return CreateSettingsNode(null, null);
    }

    private int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.0.0") ^
            SettingsHelper.CreateSetting(document, parent,
                "IsStatsUploadingEnabled", IsStatsUploadingEnabled) ^
            SettingsHelper.CreateSetting(document, parent,
                "IsUploadOnResetEnabled", IsUploadOnResetEnabled) ^
            SettingsHelper.CreateSetting(document, parent,
                "IsLiveTrackingEnabled", IsLiveTrackingEnabled) ^
            SettingsHelper.CreateSetting(document, parent,
                "IsLayoutPathUploadEnabled", IsLayoutPathUploadEnabled);
    }

}
