using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace LiveSplit.Hosting;

public interface IPlatformServices
{
    DialogResult ShowMessage(object owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon);

    DialogResult ShowScrollableMessage(object owner, string text, string caption, MessageBoxButtons buttons);

    DialogResult Prompt(object owner, string title, string promptText, ref string value);

    DialogResult Prompt(object owner, string title, string promptText, string promptText2, ref string value, ref string value2);

    DialogResult Login(object owner, string title, string usernamePrompt, string passwordPrompt, ref string username, ref string password, ref bool remember);

    DialogResult ChooseFromSearch(object owner, string title, string text, string initialQuery, Func<string, IReadOnlyList<string>> search, out int selectedIndex);

    string ChooseSaveFile(object owner, string title, string filter, string suggestedName);

    string ChooseOpenFile(object owner, string title, string filter);

    void SetClipboardImage(Image image);

    void SetClipboardText(string text);

    void OpenUrl(string url);
}
