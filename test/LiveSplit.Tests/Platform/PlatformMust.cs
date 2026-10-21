using LiveSplit.Hosting;
using LiveSplit.Web;
using System;
using System.IO;
using Xunit;

namespace LiveSplit.Tests.Platform;

[Collection("Environment")]
public class PlatformMust : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "livesplit-test-" + Guid.NewGuid().ToString("N"));

    public PlatformMust()
    {
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        Paths.SetDirectories(null, null);
        Paths.SetPortable(null);
        Directory.Delete(directory, true);
    }

    [Theory]
    [InlineData(@"Z:\home\user\file.lss", "/home/user/file.lss")]
    [InlineData(@"z:\home\user\file.lss", "/home/user/file.lss")]
    [InlineData(@"Splits\run.lss", "Splits/run.lss")]
    [InlineData("/home/user/file.lss", "/home/user/file.lss")]
    [InlineData("relative/file.lss", "relative/file.lss")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void NormalizeWindowsPaths(string path, string expected)
    {
        Assert.Equal(expected, PathCompat.Normalize(path));
    }

    [Fact]
    public void MapOtherWineDrivesThroughDosdevices()
    {
        string prefix = Path.Combine(directory, "prefix");
        string target = Path.Combine(directory, "games");
        Directory.CreateDirectory(Path.Combine(prefix, "dosdevices"));
        Directory.CreateDirectory(target);
        Directory.CreateSymbolicLink(Path.Combine(prefix, "dosdevices", "d:"), target);
        string previous = Environment.GetEnvironmentVariable("WINEPREFIX");
        Environment.SetEnvironmentVariable("WINEPREFIX", prefix);
        try
        {
            Assert.Equal(Path.Combine(target, "run/a.lss"), PathCompat.Normalize(@"D:\run\a.lss"));
            Assert.Equal("/run/a.lss", PathCompat.Normalize(@"X:\run\a.lss"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("WINEPREFIX", previous);
        }
    }

    [Fact]
    public void ResolveCaseInsensitiveFileNames()
    {
        File.WriteAllText(Path.Combine(directory, "Sound.WAV"), "");

        string resolved = PathCompat.Resolve(@"sound.wav", directory);

        Assert.Equal(Path.Combine(directory, "Sound.WAV"), resolved);
    }

    [Fact]
    public void PreferXdgDirectories()
    {
        Paths.SetPortable(false);
        string config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        string data = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", Path.Combine(directory, "cfg"));
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", Path.Combine(directory, "dat"));
        try
        {
            Assert.Equal(Path.Combine(directory, "cfg", "LiveSplit"), Paths.ConfigDirectory);
            Assert.Equal(Path.Combine(directory, "cfg", "LiveSplit", "settings.cfg"), Paths.SettingsFile);
            Assert.Equal(Path.Combine(directory, "dat", "LiveSplit"), Paths.DataDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", config);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", data);
        }
    }

    [Fact]
    public void FallBackToDotDirectoriesInHome()
    {
        Paths.SetPortable(false);
        string config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        string data = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        string home = Environment.GetEnvironmentVariable("HOME");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", null);
        Environment.SetEnvironmentVariable("HOME", directory);
        try
        {
            Assert.Equal(Path.Combine(directory, ".config", "LiveSplit"), Paths.ConfigDirectory);
            Assert.Equal(Path.Combine(directory, ".local", "share", "LiveSplit"), Paths.DataDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", config);
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", data);
            Environment.SetEnvironmentVariable("HOME", home);
        }
    }

    [Fact]
    public void UseTheExecutableDirectoryInPortableMode()
    {
        Paths.SetPortable(true);

        Assert.Equal(Paths.ExecutableDirectory, Paths.ConfigDirectory);
        Assert.Equal(Paths.ExecutableDirectory, Paths.DataDirectory);
    }

    [Fact]
    public void StoreCredentialsInAPrivateFile()
    {
        string file = Path.Combine(directory, "credentials.json");
        ICredentialStore store = new FileCredentialStore(file);

        Assert.Null(store.Read("app"));
        Assert.True(store.Write("app", "alice", "s3cret"));

        Credential credential = store.Read("app");
        Assert.Equal("alice", credential.UserName);
        Assert.Equal("s3cret", credential.Password);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));

        Assert.True(store.Delete("app"));
        Assert.Null(store.Read("app"));
    }

    [Fact]
    public void ExposeCredentialsThroughTheCredentialManager()
    {
        ICredentialStore previous = CredentialManager.Store;
        CredentialManager.Store = new FileCredentialStore(Path.Combine(directory, "credentials.json"));
        try
        {
            WebCredentials.TwitchAccessToken = "token";
            WebCredentials.BlueskyCredentials = new BlueskyCredentials("me.bsky.social", "pw");

            Assert.Equal("token", WebCredentials.TwitchAccessToken);
            Assert.Equal("me.bsky.social", WebCredentials.BlueskyCredentials.Username);
            Assert.True(WebCredentials.AnyCredentialsExist());

            WebCredentials.DeleteAllCredentials();
            Assert.False(WebCredentials.AnyCredentialsExist());
        }
        finally
        {
            CredentialManager.Store = previous;
        }
    }
}
