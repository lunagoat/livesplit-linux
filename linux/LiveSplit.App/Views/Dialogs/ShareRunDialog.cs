using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplit.Controls.Settings;
using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Services;
using LiveSplit.TimeFormatters;
using LiveSplit.Web.Share;
using System;
using System.Linq;
using System.Windows.Forms;
using Image = System.Drawing.Image;

namespace LiveSplit.View.Dialogs;

public sealed class ShareRunDialog : DialogWindow
{
    public IRun Run { get; }
    public LiveSplitState State { get; }
    public ISettings Settings { get; }
    public Func<Image> ScreenShotFunction { get; }

    private IRunUploadPlatform currentPlatform;
    private bool isSpeedrunCom;

    private readonly ComboBox cbxPlatform;
    private readonly TextBlock lblDescription;
    private readonly TextBox txtNotes;
    private readonly Button btnInsertGame;
    private readonly Button btnInsertCategory;
    private readonly Button btnInsertTitle;
    private readonly Button btnInsertPB;
    private readonly Button btnInsertSplitName;
    private readonly Button btnInsertSplitTime;
    private readonly Button btnInsertDeltaTime;
    private readonly Button btnInsertStreamLink;
    private readonly Button btnPreview;
    private readonly StackPanel insertButtonsPanel;

    public ShareRunDialog(ShareDialogRequest request)
        : this(request.State, request.Settings, request.MakeScreenShot)
    {
    }

    public ShareRunDialog(LiveSplitState state, ISettings settings, Func<Image> screenShotFunction)
        : base(SettingsPage.T("Share Run"))
    {
        State = state;
        Settings = settings;
        ScreenShotFunction = screenShotFunction;

        if (State.CurrentPhase != TimerPhase.Ended)
        {
            Run = state.Run;
        }
        else
        {
            var model = new TimerModel { CurrentState = State };
            model.ResetAndSetAttemptAsPB();
            Run = State.Run;
        }

        Width = 580;
        Height = 520;
        MinWidth = 480;
        MinHeight = 400;
        CanResize = true;

        var mainStack = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        // Platform row
        var platRow = new Grid { ColumnDefinitions = new ColumnDefinitions("120,*"), ColumnSpacing = 8 };
        platRow.Children.Add(new TextBlock { Text = SettingsPage.T("Platform:"), VerticalAlignment = VerticalAlignment.Center });

        cbxPlatform = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        cbxPlatform.Items.Add("X (Twitter)");
        cbxPlatform.Items.Add("Bluesky");

        if (State.CurrentPhase is TimerPhase.NotRunning or TimerPhase.Ended)
        {
            if (Run != null && Run.Count > 0 && Run[^1].PersonalBestSplitTime.RealTime.HasValue)
            {
                cbxPlatform.Items.Add("Speedrun.com");
            }
        }

        cbxPlatform.Items.Add("Screenshot");
        cbxPlatform.Items.Add("Imgur");
        cbxPlatform.Items.Add("Excel");
        try
        {
            if (Twitch.Instance != null)
                cbxPlatform.Items.Add("Twitch");
        }
        catch { }

        Grid.SetColumn(cbxPlatform, 1);
        platRow.Children.Add(cbxPlatform);
        mainStack.Children.Add(platRow);

        // Description Card
        lblDescription = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC"))
        };
        var descBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#555555")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = lblDescription
        };
        mainStack.Children.Add(descBorder);

        // Notes and Insert Buttons Card
        var notesCard = new StackPanel { Spacing = 8 };
        notesCard.Children.Add(new TextBlock { Text = SettingsPage.T("Message / Notes:"), FontWeight = FontWeight.SemiBold });

        txtNotes = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 100
        };
        notesCard.Children.Add(txtNotes);

        insertButtonsPanel = new StackPanel { Spacing = 4 };

        var row1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        btnInsertGame = SmallButton("$game", () => Insert("$game"));
        btnInsertCategory = SmallButton("$category", () => Insert("$category"));
        btnInsertTitle = SmallButton("$title", () => Insert("$title"));
        btnInsertPB = SmallButton("$pb", () => Insert("$pb"));
        row1.Children.Add(btnInsertGame);
        row1.Children.Add(btnInsertCategory);
        row1.Children.Add(btnInsertTitle);
        row1.Children.Add(btnInsertPB);
        insertButtonsPanel.Children.Add(row1);

        var row2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        btnInsertSplitName = SmallButton("$splitname", () => Insert("$splitname"));
        btnInsertSplitTime = SmallButton("$splittime", () => Insert("$splittime"));
        btnInsertDeltaTime = SmallButton("$delta", () => Insert("$delta"));
        btnInsertStreamLink = SmallButton("$stream", () => Insert("$stream"));
        row2.Children.Add(btnInsertSplitName);
        row2.Children.Add(btnInsertSplitTime);
        row2.Children.Add(btnInsertDeltaTime);
        row2.Children.Add(btnInsertStreamLink);
        insertButtonsPanel.Children.Add(row2);

        btnPreview = new Button { Content = SettingsPage.T("Preview"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
        btnPreview.Click += async (_, _) =>
        {
            await new MessageBoxWindow(
                FormatNotes(txtNotes.Text ?? string.Empty),
                SettingsPage.T("Preview"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            ).ShowModalAsync(this);
        };
        insertButtonsPanel.Children.Add(btnPreview);

        notesCard.Children.Add(insertButtonsPanel);

        var notesBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#555555")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = notesCard
        };
        mainStack.Children.Add(notesBorder);

        SetBody(new ScrollViewer
        {
            Content = mainStack,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        });

        AddButton(SettingsPage.T("Share"), OnSubmit, isDefault: true);
        AddButton(SettingsPage.T("Close"), DialogResult.Cancel, isCancel: true);

        cbxPlatform.SelectionChanged += (_, _) => OnPlatformChanged();

        if (cbxPlatform.ItemCount > 0)
        {
            cbxPlatform.SelectedIndex = 0;
        }
    }

    private static Button SmallButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Content = text,
            Padding = new Thickness(6, 2)
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private void Insert(string text)
    {
        int start = txtNotes.SelectionStart;
        string current = txtNotes.Text ?? string.Empty;
        if (start < 0 || start > current.Length) start = current.Length;

        txtNotes.Text = current.Insert(start, text);
        txtNotes.SelectionStart = start + text.Length;
        txtNotes.Focus();
    }

    private void OnPlatformChanged()
    {
        string selected = cbxPlatform.SelectedItem?.ToString();
        isSpeedrunCom = selected == "Speedrun.com";

        switch (selected)
        {
            case "X (Twitter)": currentPlatform = Twitter.Instance; break;
            case "Bluesky": currentPlatform = Bluesky.Instance; break;
            case "Screenshot": currentPlatform = Screenshot.Instance; break;
            case "Imgur": currentPlatform = Imgur.Instance; break;
            case "Excel": currentPlatform = Excel.Instance; break;
            case "Twitch": currentPlatform = Twitch.Instance; break;
            case "Speedrun.com":
                currentPlatform = null;
                lblDescription.Text = SettingsPage.T("Speedrun.com is a site intended to provide centralized leaderboards for speedrunning.");
                break;
            default:
                currentPlatform = null;
                break;
        }

        if (currentPlatform != null)
        {
            currentPlatform.Settings = Settings;
            lblDescription.Text = SettingsPage.T(currentPlatform.Description);
        }

        bool allowsNotes = currentPlatform == Twitter.Instance ||
                           currentPlatform == Bluesky.Instance ||
                           currentPlatform == Twitch.Instance ||
                           currentPlatform == Imgur.Instance;

        txtNotes.IsEnabled = allowsNotes;
        insertButtonsPanel.IsEnabled = allowsNotes;

        if (allowsNotes)
        {
            bool hasSplits = State.CurrentPhase is TimerPhase.Running or TimerPhase.Paused && State.CurrentSplitIndex > 0;
            btnInsertDeltaTime.IsEnabled = hasSplits;
            btnInsertSplitName.IsEnabled = hasSplits;
            btnInsertSplitTime.IsEnabled = hasSplits;

            btnInsertPB.IsEnabled = Run != null && Run.Count > 0 && Run[^1].PersonalBestSplitTime[State.CurrentTimingMethod] != null;
            btnInsertGame.IsEnabled = !string.IsNullOrEmpty(Run?.GameName);
            btnInsertCategory.IsEnabled = !string.IsNullOrEmpty(Run?.CategoryName);
            btnInsertTitle.IsEnabled = !string.IsNullOrEmpty(Run?.GameName) || !string.IsNullOrEmpty(Run?.CategoryName);
        }

        RefreshNotes();
    }

    private void RefreshNotes()
    {
        if (currentPlatform == Twitter.Instance || currentPlatform == Bluesky.Instance)
        {
            ShareSettings.Default.Reload();
            if (State.CurrentPhase is TimerPhase.NotRunning or TimerPhase.Ended)
            {
                txtNotes.Text = currentPlatform == Twitter.Instance
                    ? ShareSettings.Default.TwitterFormat
                    : ShareSettings.Default.BlueskyFormat;
                if (string.IsNullOrEmpty(txtNotes.Text))
                    txtNotes.Text = "I got a $pb in $title.";
            }
            else
            {
                txtNotes.Text = currentPlatform == Twitter.Instance
                    ? ShareSettings.Default.TwitterFormatRunning
                    : ShareSettings.Default.BlueskyFormatRunning;
                if (string.IsNullOrEmpty(txtNotes.Text))
                    txtNotes.Text = "I'm $delta in $title.";
            }
        }
        else if (currentPlatform == Twitch.Instance)
        {
            ShareSettings.Default.Reload();
            txtNotes.Text = ShareSettings.Default.TwitchFormat;
            if (string.IsNullOrEmpty(txtNotes.Text))
                txtNotes.Text = "$title Speedrun";
        }
        else
        {
            txtNotes.Text = string.Empty;
        }
    }

    private void SaveNotesFormat()
    {
        if (currentPlatform == Twitter.Instance || currentPlatform == Bluesky.Instance)
        {
            if (State.CurrentPhase is TimerPhase.NotRunning or TimerPhase.Ended)
            {
                if (currentPlatform == Twitter.Instance)
                    ShareSettings.Default.TwitterFormat = txtNotes.Text;
                else
                    ShareSettings.Default.BlueskyFormat = txtNotes.Text;
            }
            else
            {
                if (currentPlatform == Twitter.Instance)
                    ShareSettings.Default.TwitterFormatRunning = txtNotes.Text;
                else
                    ShareSettings.Default.BlueskyFormatRunning = txtNotes.Text;
            }
            ShareSettings.Default.Save();
        }
        else if (currentPlatform == Twitch.Instance)
        {
            ShareSettings.Default.TwitchFormat = txtNotes.Text;
            ShareSettings.Default.Save();
        }
    }

    private string FormatNotes(string template)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        var timeFormatter = new RegularTimeFormatter(TimeAccuracy.Seconds);
        var deltaTimeFormatter = new DeltaTimeFormatter();

        string game = Run?.GameName ?? "";
        string category = Run?.GetExtendedCategoryName() ?? "";
        string pb = Run != null && Run.Count > 0 ? timeFormatter.Format(Run[^1].PersonalBestSplitTime[State.CurrentTimingMethod]) ?? "" : "";
        string title = Run?.GetExtendedName() ?? "";

        string splitName = "";
        string splitTime = "-";
        string deltaTime = "-";

        if ((State.CurrentPhase == TimerPhase.Running || State.CurrentPhase == TimerPhase.Paused) && State.CurrentSplitIndex > 0 && Run != null)
        {
            ISegment lastSplit = Run[State.CurrentSplitIndex - 1];
            splitName = lastSplit.Name ?? "";
            splitTime = timeFormatter.Format(lastSplit.SplitTime[State.CurrentTimingMethod]);
            deltaTime = deltaTimeFormatter.Format(lastSplit.SplitTime[State.CurrentTimingMethod] - lastSplit.PersonalBestSplitTime[State.CurrentTimingMethod]);
        }

        string streamLink = "";
        if (template.Contains("$stream"))
        {
            try
            {
                if (Twitch.Instance != null && (Twitch.Instance.IsLoggedIn || Twitch.Instance.VerifyLogin(true)))
                {
                    string userName = Twitch.Instance.ChannelName;
                    streamLink = $"https://twitch.tv/{userName}";
                }
            }
            catch { }
        }

        return template
            .Replace("$game", game)
            .Replace("$category", category)
            .Replace("$title", title)
            .Replace("$pb", pb)
            .Replace("$splitname", splitName)
            .Replace("$splittime", splitTime)
            .Replace("$delta", deltaTime)
            .Replace("$stream", streamLink);
    }

    private async void OnSubmit()
    {
        if (isSpeedrunCom)
        {
            if (Run?.Metadata == null)
            {
                await new MessageBoxWindow(
                    SettingsPage.T("No run metadata available."),
                    SettingsPage.T("Submitting Failed"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                ).ShowModalAsync(this);
                return;
            }

            bool isValid = SpeedrunCom.ValidateRun(Run.Metadata.LiveSplitRun, out string reason);
            if (!isValid)
            {
                await new MessageBoxWindow(
                    reason ?? SettingsPage.T("Validation failed."),
                    SettingsPage.T("Submitting Failed"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                ).ShowModalAsync(this);
                return;
            }

            var submitDlg = new SpeedrunComSubmitDialog(Run.Metadata);
            if (await submitDlg.ShowModalAsync(this) == DialogResult.OK)
            {
                Close(DialogResult.OK);
            }
            return;
        }

        if (currentPlatform == null) return;

        string notes = FormatNotes(txtNotes.Text ?? string.Empty);
        SaveNotesFormat();

        try
        {
            if (!currentPlatform.VerifyLogin())
            {
                await new MessageBoxWindow(
                    SettingsPage.T("Your login information seems to be incorrect."),
                    SettingsPage.T("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                ).ShowModalAsync(this);
                return;
            }

            bool submitted = currentPlatform.SubmitRun(
                Run,
                ScreenShotFunction,
                State.CurrentTimingMethod,
                notes
            );

            if (submitted)
            {
                await new MessageBoxWindow(
                    string.Format(SettingsPage.T("Your run was successfully shared to {0}."), currentPlatform.PlatformName),
                    SettingsPage.T("Run Shared"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                ).ShowModalAsync(this);
                Close(DialogResult.OK);
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        await new MessageBoxWindow(
            SettingsPage.T("The run could not be shared."),
            SettingsPage.T("Error"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Error
        ).ShowModalAsync(this);
    }
}
