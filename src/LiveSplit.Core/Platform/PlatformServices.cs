using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace LiveSplit.Hosting;

public static class PlatformServices
{
    private static IPlatformServices current = new ConsolePlatformServices();

    public static IPlatformServices Current
    {
        get => current;
        set => current = value ?? new ConsolePlatformServices();
    }

    public static void OpenUrl(string url)
    {
        Current.OpenUrl(url);
    }
}

public class ConsolePlatformServices : IPlatformServices
{
    public virtual DialogResult ShowMessage(object owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        Console.Error.WriteLine($"[{caption}] {text}");
        return buttons switch
        {
            MessageBoxButtons.YesNo or MessageBoxButtons.YesNoCancel => DialogResult.No,
            MessageBoxButtons.OKCancel => DialogResult.Cancel,
            MessageBoxButtons.RetryCancel => DialogResult.Cancel,
            MessageBoxButtons.AbortRetryIgnore => DialogResult.Ignore,
            _ => DialogResult.OK,
        };
    }

    public virtual DialogResult ShowScrollableMessage(object owner, string text, string caption, MessageBoxButtons buttons)
    {
        return ShowMessage(owner, text, caption, buttons, MessageBoxIcon.None);
    }

    public virtual DialogResult Prompt(object owner, string title, string promptText, ref string value)
    {
        return DialogResult.Cancel;
    }

    public virtual DialogResult Prompt(object owner, string title, string promptText, string promptText2, ref string value, ref string value2)
    {
        return DialogResult.Cancel;
    }

    public virtual DialogResult Login(object owner, string title, string usernamePrompt, string passwordPrompt, ref string username, ref string password, ref bool remember)
    {
        return DialogResult.Cancel;
    }

    public virtual DialogResult ChooseFromSearch(object owner, string title, string text, string initialQuery, Func<string, IReadOnlyList<string>> search, out int selectedIndex)
    {
        selectedIndex = -1;
        return DialogResult.Cancel;
    }

    public virtual string ChooseSaveFile(object owner, string title, string filter, string suggestedName)
    {
        return null;
    }

    public virtual string ChooseOpenFile(object owner, string title, string filter)
    {
        return null;
    }

    public virtual void SetClipboardImage(Image image)
    {
        throw new NotSupportedException("No clipboard available.");
    }

    public virtual void SetClipboardText(string text)
    {
        throw new NotSupportedException("No clipboard available.");
    }

    public virtual void OpenUrl(string url)
    {
        try
        {
            using Process process = Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unable to open {url}: {ex.Message}");
        }
    }
}
