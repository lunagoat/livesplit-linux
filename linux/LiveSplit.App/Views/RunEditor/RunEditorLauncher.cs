using LiveSplit.Services;
using System.Collections.Generic;
using System.Windows.Forms;

namespace LiveSplit.View.RunEditor;

public sealed class RunEditorLauncher : IRunEditorLauncher
{
    public RunEditorResult ShowRunEditor(RunEditorRequest request) => UiThread.RunSync(async () =>
    {
        var states = new Dictionary<string, bool>(request.State.Settings.ComparisonGeneratorStates);
        int historySize = request.State.Settings.HcpHistorySize;
        int nBest = request.State.Settings.HcpNBestRuns;
        string comparison = request.State.CurrentComparison;
        var dialog = new RunEditorWindow(request);
        DialogResult result = await dialog.ShowModalAsync(request.Host);
        if (result == DialogResult.OK) dialog.Editor.KeepCurrentImages();
        else
        {
            request.State.Settings.ComparisonGeneratorStates = states;
            request.State.Settings.HcpHistorySize = historySize;
            request.State.Settings.HcpNBestRuns = nBest;
            request.State.CurrentComparison = comparison;
        }
        return new RunEditorResult { Result = result, ImagesToDispose = dialog.Editor.ImagesToDispose };
    });
}
