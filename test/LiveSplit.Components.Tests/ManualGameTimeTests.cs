using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;
using Xunit;

namespace LiveSplit.Components.Tests;

public class ManualGameTimeTests
{
    [Fact]
    public void PreserveSettingsAndRadioSelection()
    {
        var settings = new ManualGameTimeSettings();
        Assert.True(settings.UseSegmentTimes);
        Assert.False(settings.UseSplitTimes);
        settings.UseSplitTimes = true;
        var xml = settings.GetSettings(new XmlDocument());
        var copy = new ManualGameTimeSettings();
        copy.SetSettings(xml);
        Assert.Equal(xml.OuterXml, copy.GetSettings(new XmlDocument()).OuterXml);
        Assert.Equal(settings.GetSettingsHashCode(), copy.GetSettingsHashCode());
        Assert.False(copy.UseSegmentTimes);
        var old = new XmlDocument();
        old.LoadXml("<Settings><Version>1.0</Version></Settings>");
        copy.SetSettings(old.DocumentElement);
        Assert.False(copy.UseSegmentTimes);
    }

    [Theory]
    [InlineData(true, 35)]
    [InlineData(false, 15)]
    public void SubmitSummedTimesAndRestoreGameTimeOnUndo(bool segmentTimes, int expected)
    {
        var state = TestSupport.CreateState();
        var timer = new TimerModel { CurrentState = state };
        using var component = new ManualGameTimeComponent(state);
        component.AppendText("ignored");
        Assert.Equal("", component.InputText);
        timer.Start();
        Assert.True(state.IsGameTimePaused);
        Assert.Equal(TimeSpan.Zero, state.CurrentTime.GameTime);
        // Use deterministic positive real time for TimerModel.Split.
        state.AdjustedStartTime = TimeStamp.Now - TimeSpan.FromSeconds(60);
        state.SetGameTime(TimeSpan.FromSeconds(20));
        component.Settings.UseSegmentTimes = segmentTimes;
        component.AppendText("10 + 5");
        var enter = new KeyEventArgs(Keys.Enter);
        component.HandleKeyDown(enter);
        Assert.True(enter.Handled && enter.SuppressKeyPress);
        Assert.Equal(1, state.CurrentSplitIndex);
        Assert.Equal(TimeSpan.FromSeconds(expected), state.Run[0].SplitTime.GameTime);
        Assert.Equal("", component.InputText);
        timer.UndoSplit();
        Assert.Equal(TimeSpan.Zero, state.CurrentTime.GameTime);
        component.AppendText("bad");
        Assert.False(component.SubmitInput());
        Assert.Equal("bad", component.InputText);
        Assert.NotNull(component.InputError);
        Assert.Equal(0, state.CurrentSplitIndex);
        Assert.Equal(TimeSpan.Zero, state.CurrentTime.GameTime);
        component.HandleKeyDown(new KeyEventArgs(Keys.Escape));
        Assert.Equal("", component.InputText);
        component.AppendText("abc👩");
        component.HandleKeyDown(new KeyEventArgs(Keys.Back));
        Assert.Equal("abc", component.InputText);
        timer.Reset(false);
        Assert.Equal("", component.InputText);
        component.InputText = "retained after disposal";
        component.Dispose();
        timer.Start();
        Assert.Equal("retained after disposal", component.InputText);
    }

    [Fact]
    public void RejectOverflowingSumsWithoutChangingTimeOrSplitting()
    {
        var state = TestSupport.CreateState();
        using var component = new ManualGameTimeComponent(state);
        new TimerModel { CurrentState = state }.Start();
        component.InputText = "922337203685 + 922337203685";
        Assert.False(component.SubmitInput());
        Assert.NotNull(component.InputError);
        Assert.Equal(0, state.CurrentSplitIndex);
        Assert.Equal(TimeSpan.Zero, state.CurrentTime.GameTime);
    }

    [Theory]
    [InlineData(LayoutMode.Vertical)]
    [InlineData(LayoutMode.Horizontal)]
    public void LoadFactoryAndRenderInput(LayoutMode mode)
    {
        var state = TestSupport.CreateState();
        using var component = Assert.IsType<ManualGameTimeComponent>(ComponentManager.LoadLayoutComponent("LiveSplit.ManualGameTime.dll", state).Component);
        Assert.IsType<ManualGameTimeSettings>(component.GetSettingsControl(mode));
        var timer = new TimerModel { CurrentState = state };
        timer.Start();
        component.AppendText("1:23.45 + 5");
        using Bitmap bitmap = TestSupport.Render(state, [component], mode, mode == LayoutMode.Vertical ? 360 : 60, "manualgametime-" + mode + ".png");
        Assert.NotNull(TestSupport.ContentBounds(bitmap, Color.FromArgb(15, 15, 15), new Rectangle(0, 0, bitmap.Width, bitmap.Height)));
    }
}
