using LiveSplit.Localization;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.Options.SettingsSavers;
using System;
using System.IO;

namespace LiveSplit.Services;

/// <summary>Loads and saves settings.cfg (the same XML as the Windows version).</summary>
public class SettingsStore
{
    private readonly ISettingsSaver saver = new XMLSettingsSaver();

    public string Path { get; }

    public SettingsStore(string path)
    {
        Path = path;
    }

    /// <summary>Settings from disk, or the defaults when the file is missing or unreadable.</summary>
    public ISettings Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                using FileStream stream = File.OpenRead(Path);
                ISettings settings = new XMLSettingsFactory(stream).Create();
                LanguageResolver.SetCurrentLanguageSetting(settings.UILanguage);
                return settings;
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }

        ISettings defaults = new StandardSettingsFactory().Create();
        LanguageResolver.SetCurrentLanguageSetting(defaults.UILanguage);
        return defaults;
    }

    /// <summary>Writes through a temporary file so a crash never leaves a half written settings.cfg.</summary>
    public bool Save(ISettings settings, out Exception error)
    {
        error = null;
        try
        {
            string directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var memoryStream = new MemoryStream();
            saver.Save(settings, memoryStream);

            string temp = Path + ".tmp";
            File.WriteAllBytes(temp, memoryStream.ToArray());
            File.Move(temp, Path, true);
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }
}
