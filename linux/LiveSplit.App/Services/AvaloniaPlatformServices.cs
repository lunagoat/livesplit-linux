using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LiveSplit.Hosting;
using LiveSplit.View;
using LiveSplit.View.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LiveSplit.Services;

/// <summary>
/// <see cref="IPlatformServices"/> on top of Avalonia windows and the storage provider. Every dialog blocks the caller
/// with a nested dispatcher loop (see <see cref="UiThread.RunSync{T}"/>), so it can be called from any thread.
/// </summary>
public class AvaloniaPlatformServices : IPlatformServices
{
    private readonly ConsolePlatformServices fallback = new();

    /// <summary>Directory the next open/save picker starts in (consumed by the next call).</summary>
    public string InitialDirectory { get; set; }

    private static Window ResolveOwner(object owner)
    {
        if (owner is Window window)
        {
            return window.IsVisible ? window : null;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Prefer the topmost open dialog so a dialog can open a child dialog.
            Window active = desktop.Windows.LastOrDefault(w => w.IsVisible && w.IsActive)
                ?? desktop.MainWindow;
            return active is { IsVisible: true } ? active : null;
        }

        return null;
    }

    public DialogResult ShowMessage(object owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        return UiThread.RunSync(() =>
        {
            var dialog = new MessageBoxWindow(text, caption, buttons, icon);
            return dialog.ShowModalAsync(ResolveOwner(owner));
        });
    }

    public DialogResult ShowScrollableMessage(object owner, string text, string caption, MessageBoxButtons buttons)
    {
        return UiThread.RunSync(() =>
        {
            var dialog = new MessageBoxWindow(text, caption, buttons, MessageBoxIcon.None, scrollable: true);
            return dialog.ShowModalAsync(ResolveOwner(owner));
        });
    }

    public DialogResult Prompt(object owner, string title, string promptText, ref string value)
    {
        string initial = value;
        (DialogResult result, string text) = UiThread.RunSync(async () =>
        {
            var dialog = new PromptWindow(title, promptText, initial);
            DialogResult r = await dialog.ShowModalAsync(ResolveOwner(owner));
            return (r, dialog.Value1);
        });

        if (result == DialogResult.OK)
        {
            value = text;
        }

        return result;
    }

    public DialogResult Prompt(object owner, string title, string promptText, string promptText2, ref string value, ref string value2)
    {
        string initial1 = value;
        string initial2 = value2;
        (DialogResult result, string text1, string text2) = UiThread.RunSync(async () =>
        {
            var dialog = new PromptWindow(title, promptText, initial1, promptText2, initial2);
            DialogResult r = await dialog.ShowModalAsync(ResolveOwner(owner));
            return (r, dialog.Value1, dialog.Value2);
        });

        if (result == DialogResult.OK)
        {
            value = text1;
            value2 = text2;
        }

        return result;
    }

    public DialogResult Login(object owner, string title, string usernamePrompt, string passwordPrompt, ref string username, ref string password, ref bool remember)
    {
        string initialUser = username;
        string initialPassword = password;
        bool initialRemember = remember;
        (DialogResult result, string user, string pass, bool rem) = UiThread.RunSync(async () =>
        {
            var dialog = new LoginWindow(title, usernamePrompt, passwordPrompt, initialUser, initialPassword, initialRemember);
            DialogResult r = await dialog.ShowModalAsync(ResolveOwner(owner));
            return (r, dialog.Username, dialog.Password, dialog.Remember);
        });

        if (result == DialogResult.OK)
        {
            username = user;
            password = pass;
            remember = rem;
        }

        return result;
    }

    public DialogResult ChooseFromSearch(object owner, string title, string text, string initialQuery, Func<string, IReadOnlyList<string>> search, out int selectedIndex)
    {
        (DialogResult result, int index) = UiThread.RunSync(async () =>
        {
            var dialog = new SearchChooseWindow(title, text, initialQuery, search);
            DialogResult r = await dialog.ShowModalAsync(ResolveOwner(owner));
            return (r, dialog.SelectedIndex);
        });

        selectedIndex = result == DialogResult.OK ? index : -1;
        return result;
    }

    public string ChooseSaveFile(object owner, string title, string filter, string suggestedName)
    {
        string startDirectory = TakeInitialDirectory();
        return UiThread.RunSync(async () =>
        {
            Window window = ResolveOwner(owner);
            IStorageProvider storage = window?.StorageProvider ?? GetAnyStorageProvider();
            if (storage == null)
            {
                return null;
            }

            List<FilePickerFileType> types = ParseFilter(filter);
            string defaultExtension = types.Count > 0 ? ExtensionOf(types[0]) : null;
            IStorageFolder startFolder = await TryGetFolder(storage, startDirectory);
            IStorageFile file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title ?? "Save",
                SuggestedFileName = suggestedName,
                FileTypeChoices = types,
                DefaultExtension = defaultExtension,
                ShowOverwritePrompt = true,
                SuggestedStartLocation = startFolder,
            });

            string path = file?.TryGetLocalPath();
            if (path != null && defaultExtension != null && !Path.HasExtension(path))
            {
                path += "." + defaultExtension;
            }

            return path;
        });
    }

    public string ChooseOpenFile(object owner, string title, string filter)
    {
        string startDirectory = TakeInitialDirectory();
        return UiThread.RunSync(async () =>
        {
            Window window = ResolveOwner(owner);
            IStorageProvider storage = window?.StorageProvider ?? GetAnyStorageProvider();
            if (storage == null)
            {
                return null;
            }

            IStorageFolder startFolder = await TryGetFolder(storage, startDirectory);
            IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title ?? "Open",
                AllowMultiple = false,
                FileTypeFilter = ParseFilter(filter),
                SuggestedStartLocation = startFolder,
            });

            return files.FirstOrDefault()?.TryGetLocalPath();
        });
    }

    public void SetClipboardImage(System.Drawing.Image image)
    {
        byte[] png;
        using (var stream = new MemoryStream())
        {
            image.Save(stream, ImageFormat.Png);
            png = stream.ToArray();
        }

        UiThread.RunSync(async () =>
        {
            IClipboard clipboard = GetClipboard();
            if (clipboard == null)
            {
                return;
            }

            var data = new DataObject();
            data.Set("image/png", png);
            await clipboard.SetDataObjectAsync(data);
        });
    }

    public void SetClipboardText(string text)
    {
        UiThread.RunSync(async () =>
        {
            IClipboard clipboard = GetClipboard();
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(text);
            }
        });
    }

    public void OpenUrl(string url)
    {
        fallback.OpenUrl(url);
    }

    private string TakeInitialDirectory()
    {
        string directory = InitialDirectory;
        InitialDirectory = null;
        return directory;
    }

    private static async Task<IStorageFolder> TryGetFolder(IStorageProvider storage, string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        try
        {
            return await storage.TryGetFolderFromPathAsync(directory);
        }
        catch
        {
            return null;
        }
    }

    private static IClipboard GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return TopLevel.GetTopLevel(desktop.MainWindow)?.Clipboard;
        }

        return null;
    }

    private static IStorageProvider GetAnyStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow?.StorageProvider;
        }

        return null;
    }

    private static string ExtensionOf(FilePickerFileType type)
    {
        string pattern = type.Patterns?.FirstOrDefault();
        if (pattern != null && pattern.StartsWith("*.") && pattern != "*.*")
        {
            return pattern[2..];
        }

        return null;
    }

    /// <summary>Parses the WinForms filter syntax: "Name (*.x)|*.x|All files (*.*)|*.*".</summary>
    public static List<FilePickerFileType> ParseFilter(string filter)
    {
        var types = new List<FilePickerFileType>();
        if (string.IsNullOrEmpty(filter))
        {
            return types;
        }

        string[] parts = filter.Split('|');
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            string[] patterns = parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            types.Add(new FilePickerFileType(parts[i])
            {
                Patterns = patterns.Length == 0 ? ["*"] : patterns.Select(p => p == "*.*" ? "*" : p).ToArray(),
            });
        }

        return types;
    }
}
