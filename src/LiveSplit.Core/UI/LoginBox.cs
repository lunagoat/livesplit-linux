using LiveSplit.Hosting;
using System.Windows.Forms;

namespace LiveSplit.UI;

public static class LoginBox
{
    public static DialogResult Show(string title, string usernamePrompt, string passwordPrompt, ref string username, ref string password, ref bool remember)
    {
        return Show(null, title, usernamePrompt, passwordPrompt, ref username, ref password, ref remember);
    }

    public static DialogResult Show(object owner, string title, string usernamePrompt, string passwordPrompt, ref string username, ref string password, ref bool remember)
    {
        return PlatformServices.Current.Login(owner, title, usernamePrompt, passwordPrompt, ref username, ref password, ref remember);
    }
}
