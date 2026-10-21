using Avalonia;
using Avalonia.Controls;
using System.Windows.Forms;

namespace LiveSplit.View.Dialogs;

public sealed class LoginWindow : DialogWindow
{
    private readonly TextBox user;
    private readonly TextBox password;
    private readonly CheckBox remember;

    public string Username => user.Text ?? string.Empty;
    public string Password => password.Text ?? string.Empty;
    public bool Remember => remember.IsChecked == true;

    public LoginWindow(string title, string usernamePrompt, string passwordPrompt, string username, string pass, bool rememberMe)
        : base(title)
    {
        MinWidth = 380;
        var body = new StackPanel { Spacing = 2 };
        body.Children.Add(Label(usernamePrompt));
        user = new TextBox { Text = username ?? string.Empty, MinWidth = 340 };
        body.Children.Add(user);
        body.Children.Add(new Border { Height = 8 });
        body.Children.Add(Label(passwordPrompt));
        password = new TextBox { Text = pass ?? string.Empty, MinWidth = 340, PasswordChar = '•' };
        body.Children.Add(password);
        remember = new CheckBox { Content = "Remember", IsChecked = rememberMe, Margin = new Thickness(0, 8, 0, 0) };
        body.Children.Add(remember);
        SetBody(body);

        AddButton("OK", DialogResult.OK, isDefault: true);
        AddButton("Cancel", DialogResult.Cancel, isCancel: true);
        Opened += (_, _) => user.Focus();
    }
}
