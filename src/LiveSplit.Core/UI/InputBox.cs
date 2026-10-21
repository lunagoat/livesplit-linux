using LiveSplit.Hosting;
using System.Windows.Forms;

namespace LiveSplit.UI;

public static class InputBox
{
    public static DialogResult Show(string title, string promptText, ref string value)
    {
        return Show(null, title, promptText, ref value);
    }

    public static DialogResult Show(object owner, string title, string promptText, ref string value)
    {
        return PlatformServices.Current.Prompt(owner, title, promptText, ref value);
    }

    public static DialogResult Show(string title, string promptText, string promptText2, ref string value, ref string value2)
    {
        return Show(null, title, promptText, promptText2, ref value, ref value2);
    }

    public static DialogResult Show(object owner, string title, string promptText, string promptText2, ref string value, ref string value2)
    {
        return PlatformServices.Current.Prompt(owner, title, promptText, promptText2, ref value, ref value2);
    }
}
