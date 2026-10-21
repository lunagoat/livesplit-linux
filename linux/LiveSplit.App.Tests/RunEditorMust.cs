using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.View.RunEditor;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace LiveSplit.AppTests;

public class RunEditorMust
{
    static RunEditorMust()
    {
        LiveSplitCoreFactory.LoadLiveSplitCore();
    }

    internal static IRun CreateRun()
    {
        var run = new Run(new StandardComparisonGeneratorsFactory());
        run.AddSegment("First", new Time(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(8)), new Time(TimeSpan.FromSeconds(9), TimeSpan.FromSeconds(7)));
        run.AddSegment("Second", new Time(TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(20)), new Time(TimeSpan.FromSeconds(14), TimeSpan.FromSeconds(11)));
        run.AddSegment("Final", new Time(TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(40)), new Time(TimeSpan.FromSeconds(19), TimeSpan.FromSeconds(19)));
        run.AttemptHistory.Add(new Attempt(1, new Time(TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(43)), null, null, null));
        for (int i = 0; i < 3; i++) run[i].SegmentHistory[1] = new Time(TimeSpan.FromSeconds(10 + i * 5), TimeSpan.FromSeconds(9 + i * 4));
        return run;
    }

    [Fact]
    public void EditSegmentTimePreservesOtherMethodAndRecalculatesFollowingSplits()
    {
        IRun run = CreateRun();
        var editor = new RunEditorModel(run, true);
        int edits = 0; editor.Edited += () => edits++;
        Assert.True(editor.EditCell(1, 3, "18.250"));
        Assert.Equal(TimeSpan.FromSeconds(28.25), run[1].PersonalBestSplitTime.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(48.25), run[2].PersonalBestSplitTime.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(20), run[1].PersonalBestSplitTime.GameTime);
        Assert.Equal(1, edits);
        Assert.False(editor.EditCell(1, 3, "bad"));
        Assert.False(editor.EditCell(1, 3, "-4"));
    }

    [Fact]
    public void BlankSplitKeepsSkippedSegmentAndFinalAssociationIsCleared()
    {
        IRun run = CreateRun(); var editor = new RunEditorModel(run, true);
        run.Metadata.RunID = "example";
        editor.EditCell(1, 2, "");
        Assert.Null(run[1].PersonalBestSplitTime.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(35), editor.SegmentTimes()[2]);
        editor.EditCell(2, 2, "50");
        Assert.Null(run.Metadata.RunID);
    }

    [Fact]
    public void ReorderingPreservesDurationsAcrossBothMethodsAndComparisons()
    {
        IRun run = CreateRun(); var editor = new RunEditorModel(run, true);
        editor.AddComparison("Goal", Run.PersonalBestComparisonName);
        editor.Move(0, 2);
        Assert.Equal(new[] { "Second", "Final", "First" }, run.Select(s => s.Name));
        Assert.Equal(TimeSpan.FromSeconds(15), run[0].PersonalBestSplitTime.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(45), run[2].Comparisons["Goal"].RealTime);
        Assert.Equal(TimeSpan.FromSeconds(40), run[2].PersonalBestSplitTime.GameTime);
    }

    [Fact]
    public void DeletingSegmentMergesAttemptHistoryAndKeepsAtLeastOneSegment()
    {
        IRun run = CreateRun(); var editor = new RunEditorModel(run, true);
        int structural = 0; editor.SegmentsChanged += () => structural++;
        editor.Remove([0]);
        Assert.Equal(TimeSpan.FromSeconds(25), run[0].SegmentHistory[1].RealTime);
        Assert.Equal(TimeSpan.FromSeconds(22), run[0].SegmentHistory[1].GameTime);
        Assert.Equal(TimeSpan.FromSeconds(25), run[0].PersonalBestSplitTime.RealTime);
        editor.Remove([0, 1]);
        Assert.Single(run); Assert.Equal(2, structural);
    }

    [Fact]
    public void RejectStructuralChangesWhenRunning()
    {
        IRun run = CreateRun(); var editor = new RunEditorModel(run, false);
        editor.Insert(0); editor.Remove([0]); editor.Move(0, 1); editor.Group([0, 1], "Group");
        Assert.Equal(3, run.Count); Assert.Equal("First", run[0].Name);
        Assert.True(editor.EditCell(0, 2, "11"));
    }

    [Fact]
    public void ManageComparisonsAndNotifyRenames()
    {
        IRun run = CreateRun(); var editor = new RunEditorModel(run, true);
        string renamed = null; editor.ComparisonRenamed += e => renamed = e.NewName;
        editor.AddComparison("Goal", Run.PersonalBestComparisonName);
        editor.RenameComparison("Goal", "Target");
        Assert.Equal("Target", renamed);
        Assert.Equal(run[2].PersonalBestSplitTime, run[2].Comparisons["Target"]);
        Assert.Throws<ArgumentException>(() => editor.AddComparison("Target"));
        Assert.Throws<ArgumentException>(() => editor.AddComparison("[Race] runner"));
        Assert.Throws<ArgumentException>(() => editor.RenameComparison(Run.PersonalBestComparisonName, "Other"));
        editor.RemoveComparison("Target");
        Assert.DoesNotContain("Target", run.CustomComparisons);
        Assert.Equal("Current Comparison", renamed);
    }

    [Fact]
    public void EditAttemptCellsWithoutChangingPersonalBestOrOtherTimingMethod()
    {
        IRun run = CreateRun(); var editor = new RunEditorModel(run, true) { AttemptIndex = 1 };
        editor.EditCell(1, 2, "30");
        Assert.Equal(TimeSpan.FromSeconds(20), run[1].SegmentHistory[1].RealTime);
        Assert.Equal(TimeSpan.FromSeconds(13), run[1].SegmentHistory[1].GameTime);
        Assert.Equal(TimeSpan.FromSeconds(25), run[1].PersonalBestSplitTime.RealTime);
        Assert.False(editor.EditCell(1, 4, "40"));
        Assert.False(editor.EditCell(1, 1, "Rename"));
        editor.DeleteAttempt(1);
        Assert.Empty(run.AttemptHistory);
        Assert.All(run, s => Assert.False(s.SegmentHistory.ContainsKey(1)));
    }

    [Fact]
    public void GroupAndUngroupUsingPersistedSubsplitNames()
    {
        IRun run = CreateRun(); var editor = new RunEditorModel(run, true);
        editor.Group([0, 1], "Chapter");
        Assert.Equal("-First", run[0].Name); Assert.Equal("{Chapter} Second", run[1].Name);
        Assert.Equal("Second", RunEditorModel.UngroupName(run[1].Name));
        Assert.Throws<ArgumentException>(() => editor.Group([0, 2], "Invalid"));
    }

    [Fact]
    public void PastePrefersSplitColumnsAndRecalculatesSegmentOnlyPastes()
    {
        IRun run = CreateRun(); var editor = new RunEditorModel(run, true);
        Assert.Equal(0, editor.Paste(0, 1, "One\t12\t999\t9\nTwo\t30\t999\t14"));
        Assert.Equal(TimeSpan.FromSeconds(30), run[1].PersonalBestSplitTime.RealTime);
        Assert.Equal("Two", run[1].Name);
        editor.Paste(0, 3, "11\n16");
        Assert.Equal(TimeSpan.FromSeconds(27), run[1].PersonalBestSplitTime.RealTime);
        Assert.Equal(1, editor.Paste(0, 2, "invalid"));
    }

    [Fact]
    public void ImageOwnershipKeepsLiveImagesAndTracksReplacementsForHostDisposal()
    {
        IRun run = CreateRun(); var editor = new RunEditorModel(run, true);
        using var old = new System.Drawing.Bitmap(2, 2);
        using var replacement = new System.Drawing.Bitmap(3, 3);
        run[0].Icon = old;
        editor.SetIcon(0, replacement);
        Assert.Contains(old, editor.ImagesToDispose);
        Assert.Contains(replacement, editor.ImagesToDispose);
        editor.KeepCurrentImages();
        Assert.Contains(old, editor.ImagesToDispose);
        Assert.DoesNotContain(replacement, editor.ImagesToDispose);
    }

    [Fact]
    public void GameIconHandlingTracksReplacements()
    {
        IRun run = CreateRun(); var editor = new RunEditorModel(run, true);
        using var oldIcon = new System.Drawing.Bitmap(4, 4);
        using var newIcon = new System.Drawing.Bitmap(5, 5);
        run.GameIcon = oldIcon;
        editor.SetIcon(-1, newIcon);
        Assert.Same(newIcon, run.GameIcon);
        Assert.Contains(oldIcon, editor.ImagesToDispose);
        Assert.Contains(newIcon, editor.ImagesToDispose);
        editor.KeepCurrentImages();
        Assert.Contains(oldIcon, editor.ImagesToDispose);
        Assert.DoesNotContain(newIcon, editor.ImagesToDispose);
    }

    [Fact]
    public void CleanSumOfBestRemovesInaccurateHistory()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Samples", "clean_sum_of_best.lss"));
        IRun run = new StandardFormatsRunFactory(stream, "clean_sum_of_best.lss").Create(new StandardComparisonGeneratorsFactory());
        int removedCount = 0;
        SumOfBest.Clean(run, _ => { removedCount++; return true; });
        Assert.True(removedCount > 0, "Expected clean_sum_of_best to identify inaccurate segments");
    }

    [Theory]
    [InlineData("Celeste - Any% (1.2.1.5).lss")]
    [InlineData("celeste_native_segment_groups.lss")]
    [InlineData("clean_sum_of_best.lss")]
    [InlineData("livesplit1.0.lss")]
    [InlineData("livesplit1.4.lss")]
    [InlineData("livesplit1.5.lss")]
    [InlineData("livesplit1.6.lss")]
    [InlineData("livesplit1.6_gametime.lss")]
    [InlineData("livesplit_attempt_ended_bug.lss")]
    public void LoadAndEditRealWindowsFiles(string file)
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Samples", file));
        IRun run = new StandardFormatsRunFactory(stream, file).Create(new StandardComparisonGeneratorsFactory());
        Assert.NotEmpty(run);
        var editor = new RunEditorModel(run, true);
        int original = run.Count;
        editor.AddComparison("Test clone", Run.PersonalBestComparisonName);
        editor.Insert(0); Assert.Equal(original + 1, run.Count);
        editor.Remove([0]); Assert.Equal(original, run.Count);
        Assert.True(editor.EditCell(0, 1, "Edited segment"));
        editor.Method = TimingMethod.GameTime;
        Assert.True(editor.EditCell(0, 2, "1:00"));
        Assert.Equal(TimeSpan.FromMinutes(1), run[0].PersonalBestSplitTime.GameTime);
    }

    [Theory]
    [InlineData("livesplit_fuzz_crash.lss")]
    [InlineData("livesplit_fuzz_crash_utf8.lss")]
    public void RejectMalformedFuzzCrashFiles(string file)
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Samples", file));
        Assert.ThrowsAny<Exception>(() => new StandardFormatsRunFactory(stream, file).Create(new StandardComparisonGeneratorsFactory()));
    }
}
