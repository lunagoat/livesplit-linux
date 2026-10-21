using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Drawing;
using System.IO;
using Xunit;

namespace LiveSplit.Components.Tests;

/// <summary>
/// Renders each component with real data through ComponentRenderer into PNGs (see TestSupport.OutDir) and checks
/// coarse pixel facts. The PNGs are meant to be looked at.
/// </summary>
public class RenderingMust
{
    private static readonly Color Background = Color.FromArgb(15, 15, 15);

    public RenderingMust()
    {
        TestSupport.EnsureFactories();
    }

    [Fact]
    public void DrawTimerDigits()
    {
        LiveSplitState state = TestSupport.CreateState();
        TestSupport.MakeRunning(state, TimeSpan.FromSeconds(754.32), 1);
        var timer = new Timer();

        using Bitmap bitmap = TestSupport.Render(state, [timer], LayoutMode.Vertical, 300, "timer-vertical-running.png");
        Assert.Equal(50, bitmap.Height); // default TimerHeight
        Rectangle? bounds = TestSupport.ContentBounds(bitmap, Background, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
        Assert.NotNull(bounds);
        Assert.True(bounds.Value.Width > 100, "digits should span a good part of the width");
        Assert.True(bounds.Value.Right > 250, "digits are right aligned");
    }

    [Fact]
    public void DrawTimerInEveryStyle()
    {
        var timer = new Timer();
        timer.Settings.BackgroundColor = Color.FromArgb(255, 40, 40, 90);
        timer.Settings.BackgroundColor2 = Color.FromArgb(255, 10, 10, 30);
        timer.Settings.BackgroundGradient = DeltasGradientType.Vertical;
        timer.Settings.DigitsFormat = "00:00:01";
        timer.Settings.Accuracy = ".234";
        timer.Settings.OverrideSplitColors = true;
        timer.Settings.TimerColor = Color.FromArgb(255, 255, 140, 0);
        timer.Settings.CenterTimer = true;

        LiveSplitState state = TestSupport.CreateState();
        TestSupport.MakeRunning(state, TimeSpan.FromSeconds(3725.789), 2);
        using Bitmap custom = TestSupport.Render(state, [timer], LayoutMode.Vertical, 360, "timer-vertical-custom.png");
        Assert.NotEqual(Background, custom.GetPixel(2, 2));

        var plain = new Timer();
        using Bitmap horizontal = TestSupport.Render(TestSupport.CreateState(), [plain], LayoutMode.Horizontal, 60, "timer-horizontal-notrunning.png");
        Assert.Equal(225, horizontal.Width); // default TimerWidth
    }

    [Fact]
    public void DrawTitleWithIconAndAttempts()
    {
        LiveSplitState state = TestSupport.CreateState();
        state.Run.GameIcon = TestSupport.CreateIcon();
        var title = new Title();

        using Bitmap bitmap = TestSupport.Render(state, [title], LayoutMode.Vertical, 300, "title-vertical-icon.png");
        int h = bitmap.Height;
        Color icon = bitmap.GetPixel(7 + (h / 2), h / 2);
        Assert.True(icon.R > 150 && icon.B < 100, $"game icon expected at the left, got {icon}");
        Rectangle? text = TestSupport.ContentBounds(bitmap, bitmap.GetPixel(h + 20, 2), new Rectangle(h + 4, 0, 300 - h - 4, h));
        Assert.NotNull(text);
    }

    [Fact]
    public void DrawTitleSingleLineAndHorizontal()
    {
        LiveSplitState state = TestSupport.CreateState();
        var title = new Title();
        title.Settings.SingleLine = true;
        title.Settings.ShowFinishedRunsCount = true;
        title.Settings.BackgroundGradient = GradientType.Horizontal;
        title.Settings.BackgroundColor = Color.FromArgb(255, 70, 20, 20);
        using Bitmap vertical = TestSupport.Render(state, [title], LayoutMode.Vertical, 420, "title-vertical-singleline.png");
        Assert.True(vertical.GetPixel(2, 2).R > vertical.GetPixel(417, 2).R, "horizontal gradient goes from the first to the second colour");

        var horizontalTitle = new Title();
        using Bitmap horizontal = TestSupport.Render(TestSupport.CreateState(), [horizontalTitle], LayoutMode.Horizontal, 50, "title-horizontal.png");
        Assert.True(horizontal.Width > 60);
    }

    [Fact]
    public void DrawTextComponent()
    {
        LiveSplitState state = TestSupport.CreateState();
        state.Run.Metadata.SetCustomVariable("Deaths", "17");
        var left = new TextComponent(state);
        left.Settings.Text1 = "Deaths";
        left.Settings.Text2 = "Deaths";
        left.Settings.CustomVariable = true;
        left.Settings.OverrideTextColor = true;
        left.Settings.TextColor = Color.FromArgb(255, 255, 160, 0);
        left.Settings.BackgroundGradient = GradientType.Vertical;
        left.Settings.BackgroundColor = Color.FromArgb(255, 30, 30, 60);
        left.Settings.BackgroundColor2 = Color.FromArgb(255, 10, 10, 20);

        var centered = new TextComponent(state);
        centered.Settings.Text1 = "Just some centered text";
        centered.Settings.Text2 = "";

        var twoRows = new TextComponent(state);
        twoRows.Settings.Text1 = "Two rows";
        twoRows.Settings.Text2 = "value";
        twoRows.Settings.Display2Rows = true;

        using Bitmap bitmap = TestSupport.Render(state, [left, centered, twoRows], LayoutMode.Vertical, 300, "text-vertical.png");
        Assert.True(bitmap.Height > 31 * 2, "two rows component is taller than a single row");
        Assert.NotNull(TestSupport.ContentBounds(bitmap, Background, new Rectangle(0, 0, 300, bitmap.Height)));

        // Horizontal layouts always show two rows.
        using Bitmap horizontal = TestSupport.Render(state, [new TextComponent(state) { Settings = { Text1 = "Attempts", Text2 = "1337" } }], LayoutMode.Horizontal, 50, "text-horizontal.png");
        Assert.True(horizontal.Width > 40);
    }

    [Fact]
    public void DrawBlankSpaceBackground()
    {
        var blank = new BlankSpace();
        blank.Settings.SpaceHeight = 40;
        blank.Settings.BackgroundGradient = GradientType.Vertical;
        blank.Settings.BackgroundColor = Color.FromArgb(255, 200, 0, 0);
        blank.Settings.BackgroundColor2 = Color.FromArgb(255, 0, 0, 200);

        LiveSplitState state = TestSupport.CreateState();
        using Bitmap bitmap = TestSupport.Render(state, [blank], LayoutMode.Vertical, 200, "blankspace-vertical.png");
        Assert.Equal(40, bitmap.Height);
        Color top = bitmap.GetPixel(100, 1);
        Color bottom = bitmap.GetPixel(100, 38);
        Assert.True(top.R > 150 && top.B < 60, $"top {top}");
        Assert.True(bottom.B > 150 && bottom.R < 60, $"bottom {bottom}");

        // Transparent blank space leaves the layout background alone.
        using Bitmap emptyBitmap = TestSupport.Render(state, [new BlankSpace()], LayoutMode.Vertical, 100, "blankspace-empty.png");
        Assert.Equal(Background, emptyBitmap.GetPixel(50, 50));
    }

    [Fact]
    public void DrawAllFourInOneLayout()
    {
        LiveSplitState state = TestSupport.CreateState();
        state.Run.GameIcon = TestSupport.CreateIcon();
        TestSupport.MakeRunning(state, TimeSpan.FromSeconds(95.4), 1);
        var text = new TextComponent(state) { Settings = { Text1 = "Route", Text2 = "Any% Japan" } };
        using Bitmap bitmap = TestSupport.Render(state, [new Title(), new Timer(), text, new BlankSpace { Settings = { SpaceHeight = 20 } }], LayoutMode.Vertical, 320, "all-four-vertical.png");
        Assert.NotNull(TestSupport.ContentBounds(bitmap, Background, new Rectangle(0, 0, 320, bitmap.Height)));
    }
}
