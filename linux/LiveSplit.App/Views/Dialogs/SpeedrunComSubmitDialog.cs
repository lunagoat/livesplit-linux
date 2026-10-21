using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplit.Controls.Settings;
using LiveSplit.Model;
using LiveSplit.Web.Share;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace LiveSplit.View.Dialogs;

public sealed class SpeedrunComSubmitDialog : DialogWindow
{
    private readonly RunMetadata metadata;
    private readonly bool hasPersonalBestDateTime;
    private readonly DatePicker datePicker;
    private readonly TextBox txtWithoutLoads;
    private readonly TextBox txtGameTime;
    private readonly TextBox txtVideo;
    private readonly TextBox txtComment;

    public SpeedrunComSubmitDialog(RunMetadata metadata)
        : base(SettingsPage.T("Submit to Speedrun.com"))
    {
        this.metadata = metadata;

        Width = 540;
        Height = 480;
        MinWidth = 460;
        MinHeight = 360;
        CanResize = true;

        hasPersonalBestDateTime = SpeedrunCom.FindPersonalBestAttemptDate(metadata.LiveSplitRun).HasValue;

        var mainStack = new StackPanel { Spacing = 10, Margin = new Thickness(8) };

        // Game / Category info
        string gameName = metadata.Game?.Name ?? metadata.LiveSplitRun.GameName ?? SettingsPage.T("Unknown Game");
        string catName = metadata.Category?.Name ?? metadata.LiveSplitRun.CategoryName ?? SettingsPage.T("Unknown Category");

        var infoBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#555555")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = new TextBlock
            {
                Text = $"{gameName} - {catName}",
                FontWeight = FontWeight.SemiBold,
                FontSize = 14
            }
        };
        mainStack.Children.Add(infoBorder);

        var formGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("140,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto"),
            RowSpacing = 8,
            ColumnSpacing = 8
        };
        int currentRow = 0;

        // Date Picker if needed
        if (!hasPersonalBestDateTime)
        {
            var lblDate = new TextBlock { Text = SettingsPage.T("Date:"), VerticalAlignment = VerticalAlignment.Center };
            datePicker = new DatePicker
            {
                SelectedDate = DateTimeOffset.Now,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetRow(lblDate, currentRow);
            Grid.SetColumn(lblDate, 0);
            Grid.SetRow(datePicker, currentRow);
            Grid.SetColumn(datePicker, 1);
            formGrid.Children.Add(lblDate);
            formGrid.Children.Add(datePicker);
            currentRow++;
        }

        // Timing inputs based on ruleset
        Time runTime = metadata.LiveSplitRun.Count > 0 ? metadata.LiveSplitRun[^1].PersonalBestSplitTime : default;
        var timingMethods = metadata.Game?.Ruleset?.TimingMethods;
        bool usesGameTime = timingMethods != null && timingMethods.Contains(SpeedrunComSharp.TimingMethod.GameTime);
        bool usesWithoutLoads = timingMethods != null && timingMethods.Contains(SpeedrunComSharp.TimingMethod.RealTimeWithoutLoads);
        bool usesBoth = usesGameTime && usesWithoutLoads;

        if (!runTime.GameTime.HasValue || usesBoth)
        {
            if (usesWithoutLoads)
            {
                var lblWithoutLoads = new TextBlock { Text = SettingsPage.T("Without Loads:"), VerticalAlignment = VerticalAlignment.Center };
                txtWithoutLoads = new TextBox { Watermark = "0:00:00.00" };
                Grid.SetRow(lblWithoutLoads, currentRow);
                Grid.SetColumn(lblWithoutLoads, 0);
                Grid.SetRow(txtWithoutLoads, currentRow);
                Grid.SetColumn(txtWithoutLoads, 1);
                formGrid.Children.Add(lblWithoutLoads);
                formGrid.Children.Add(txtWithoutLoads);
                currentRow++;
            }

            if (usesGameTime)
            {
                var lblGameTime = new TextBlock { Text = SettingsPage.T("Game Time:"), VerticalAlignment = VerticalAlignment.Center };
                txtGameTime = new TextBox { Watermark = "0:00:00.00" };
                Grid.SetRow(lblGameTime, currentRow);
                Grid.SetColumn(lblGameTime, 0);
                Grid.SetRow(txtGameTime, currentRow);
                Grid.SetColumn(txtGameTime, 1);
                formGrid.Children.Add(lblGameTime);
                formGrid.Children.Add(txtGameTime);
                currentRow++;
            }
        }

        // Video URL
        var lblVideo = new TextBlock { Text = SettingsPage.T("Video URL:"), VerticalAlignment = VerticalAlignment.Center };
        txtVideo = new TextBox { Watermark = "https://www.youtube.com/watch?v=..." };
        Grid.SetRow(lblVideo, currentRow);
        Grid.SetColumn(lblVideo, 0);
        Grid.SetRow(txtVideo, currentRow);
        Grid.SetColumn(txtVideo, 1);
        formGrid.Children.Add(lblVideo);
        formGrid.Children.Add(txtVideo);
        currentRow++;

        // Comment
        var lblComment = new TextBlock { Text = SettingsPage.T("Comment:"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 0) };
        txtComment = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 90
        };
        Grid.SetRow(lblComment, currentRow);
        Grid.SetColumn(lblComment, 0);
        Grid.SetRow(txtComment, currentRow);
        Grid.SetColumn(txtComment, 1);
        formGrid.Children.Add(lblComment);
        formGrid.Children.Add(txtComment);

        mainStack.Children.Add(formGrid);

        SetBody(new ScrollViewer
        {
            Content = mainStack,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        });

        AddButton(SettingsPage.T("Submit"), OnSubmit, isDefault: true);
        AddButton(SettingsPage.T("Cancel"), DialogResult.Cancel, isCancel: true);
    }

    private async void OnSubmit()
    {
        Uri videoUri = null;
        if (!string.IsNullOrWhiteSpace(txtVideo.Text))
        {
            string videoText = txtVideo.Text.Trim();
            if (!videoText.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !videoText.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                videoText = "https://" + videoText;
            }

            if (Uri.IsWellFormedUriString(videoText, UriKind.Absolute))
            {
                videoUri = new Uri(videoText);
            }
            else
            {
                await new MessageBoxWindow(
                    SettingsPage.T("You didn't provide a valid Video URL."),
                    SettingsPage.T("Submitting Failed"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                ).ShowModalAsync(this);
                return;
            }
        }

        DateTime? date = null;
        if (!hasPersonalBestDateTime && datePicker?.SelectedDate != null)
        {
            var offset = datePicker.SelectedDate.Value;
            date = new DateTime(offset.Year, offset.Month, offset.Day, 0, 0, 0, DateTimeKind.Utc);
        }

        TimeSpan? withoutLoads = null;
        if (txtWithoutLoads != null && !string.IsNullOrWhiteSpace(txtWithoutLoads.Text))
        {
            try
            {
                withoutLoads = TimeSpanParser.ParseNullable(txtWithoutLoads.Text);
                PatchGameTime(withoutLoads);
            }
            catch
            {
                await new MessageBoxWindow(
                    SettingsPage.T("You didn't enter a valid Real Time without Loads."),
                    SettingsPage.T("Submitting Failed"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                ).ShowModalAsync(this);
                return;
            }
        }

        if (txtGameTime != null && !string.IsNullOrWhiteSpace(txtGameTime.Text))
        {
            try
            {
                TimeSpan? gameTime = TimeSpanParser.ParseNullable(txtGameTime.Text);
                PatchGameTime(gameTime);
            }
            catch
            {
                await new MessageBoxWindow(
                    SettingsPage.T("You didn't enter a valid Game Time."),
                    SettingsPage.T("Submitting Failed"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                ).ShowModalAsync(this);
                return;
            }
        }

        string comment = txtComment.Text ?? string.Empty;

        bool submitted = SpeedrunCom.SubmitRun(
            metadata.LiveSplitRun,
            out string reason,
            comment: comment,
            videoUri: videoUri,
            date: date,
            withoutLoads: withoutLoads
        );

        if (submitted)
        {
            try
            {
                if (metadata.Run?.WebLink != null)
                {
                    Process.Start(new ProcessStartInfo(metadata.Run.WebLink.AbsoluteUri) { UseShellExecute = true });
                }
            }
            catch { }

            Close(DialogResult.OK);
        }
        else
        {
            await new MessageBoxWindow(
                reason ?? SettingsPage.T("Unknown error submitting run."),
                SettingsPage.T("Submitting Failed"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            ).ShowModalAsync(this);
        }
    }

    private void PatchGameTime(TimeSpan? gameTime)
    {
        if (!gameTime.HasValue || metadata.LiveSplitRun.Count == 0) return;

        IRun run = metadata.LiveSplitRun;
        ISegment lastSplit = run[^1];
        Time runTime = lastSplit.PersonalBestSplitTime;

        if (runTime.GameTime.HasValue) return;

        Attempt attempt = run.AttemptHistory.FirstOrDefault(x =>
            x.Time.GameTime == runTime.GameTime && x.Time.RealTime == runTime.RealTime);

        runTime.GameTime = gameTime;

        if (attempt.Time.RealTime.HasValue)
        {
            run.AttemptHistory.Remove(attempt);
            attempt.Time = runTime;
            run.AttemptHistory = [.. run.AttemptHistory.Append(attempt).OrderBy(x => x.Index)];
        }

        lastSplit.PersonalBestSplitTime = runTime;
    }
}
