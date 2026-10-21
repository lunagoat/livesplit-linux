using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using LiveSplit.Hosting;
using LiveSplit.Model;
using LiveSplit.Services;
using LiveSplit.UI;
using LiveSplit.View.Dialogs;
using LiveSplit.Web;
using LiveSplit.Web.Share;
using SpeedrunComSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Image = System.Drawing.Image;

namespace LiveSplit.View.RunEditor;

public sealed partial class RunEditorWindow
{
    private sealed record MetadataInfo(string[] Categories, string[] Platforms, string[] Regions, Variable[] Variables, bool EmulatorAllowed, string Rules);

    private async Task LoadGameNamesAsync()
    {
        try
        {
            string[] names = await Task.Run(() => CompositeGameList.Instance.GetGameNames(true)?.ToArray());
            if (!closed && names != null) game.ItemsSource = names;
            names = await Task.Run(() => CompositeGameList.Instance.GetGameNames(false)?.ToArray());
            if (!closed && names != null) game.ItemsSource = names;
        }
        catch (Exception ex) { if (!closed) status.Text = "Game autocomplete unavailable: " + ex.Message; }
    }

    private async Task LoadAutoSplitterAsync()
    {
        if (string.IsNullOrWhiteSpace(Editor.Run.GameName)) return;
        string name = Editor.Run.GameName;
        try
        {
            AutoSplitter splitter = await Task.Run(() => AutoSplitterFactory.Instance?.Create(name));
            if (closed || Editor.Run.GameName != name) return;
            Editor.Run.AutoSplitter = splitter;
            if (splitter != null && request.State.Settings.ActiveAutoSplitters.Contains(name)) ActivateSplitter();
            RefreshAutoSplitter();
        }
        catch (Exception ex) { if (!closed) status.Text = "Auto splitter lookup failed: " + ex.Message; }
    }

    private async Task CommitNamesAsync()
    {
        bool gameChanged = Editor.Run.GameName != game.Text;
        bool categoryChanged = Editor.Run.CategoryName != category.Text;
        if (!gameChanged && !categoryChanged) return;
        if (gameChanged)
        {
            Editor.Run.AutoSplitter?.Deactivate();
            Editor.Run.GameName = game.Text ?? "";
            Editor.Run.AutoSplitter = null;
        }
        Editor.Run.CategoryName = category.Text ?? "";
        Editor.Changed(true);
        if (gameChanged && loadOnline)
        {
            await LoadAutoSplitterAsync();
        }
        if (loadOnline) await LoadMetadataAsync();
    }

    private async Task LoadMetadataAsync()
    {
        int version = ++lookupVersion;
        status.Text = "Loading speedrun.com metadata…";
        try
        {
            MetadataInfo info = await Task.Run(() =>
            {
                Game gameInfo = Editor.Run.Metadata.Game;
                if (gameInfo == null) return null;
                Category categoryInfo = Editor.Run.Metadata.Category;
                var rules = new List<string>();
                if (gameInfo.Ruleset.DefaultTimingMethod != SpeedrunComSharp.TimingMethod.RealTime) rules.Add("Timing: " + gameInfo.Ruleset.DefaultTimingMethod);
                if (gameInfo.Ruleset.RequiresVideo) rules.Add("Video proof is required.");
                rules.Add(categoryInfo?.Rules ?? "");
                return new MetadataInfo(gameInfo.FullGameCategories.Select(x => x.Name).ToArray(), gameInfo.Platforms.Select(x => x.Name).ToArray(), gameInfo.Regions.Select(x => x.Name).ToArray(), Editor.Run.Metadata.VariableValues.Keys.ToArray(), gameInfo.Ruleset.EmulatorsAllowed, string.Join("\n\n", rules));
            });
            if (closed || lookupVersion != version) return;
            RenderMetadata(info);
            status.Text = info == null ? "No speedrun.com metadata found. Local metadata remains editable." : "";
        }
        catch (Exception ex) { if (!closed && version == lookupVersion) status.Text = "Metadata lookup failed: " + ex.Message; }
    }

    private static string CleanBBCode(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        string cleaned = Regex.Replace(text, @"\[url=([^\]]+)\](.*?)\[/url\]", "$2 ($1)", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[/?(?:b|i|u|s|quote|code|color|size|list|\*)[^\]]*\]", "", RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }

    private void RenderMetadata(MetadataInfo info)
    {
        metadata.Children.Clear();
        metadata.Children.Add(Label("speedrun.com metadata"));
        if (info != null) category.ItemsSource = info.Categories;
        AddMetadataChoice("Platform", info?.Platforms, Editor.Run.Metadata.PlatformName, value => { Editor.Run.Metadata.PlatformName = value; Editor.Changed(true); });
        AddMetadataChoice("Region", info?.Regions, Editor.Run.Metadata.RegionName, value => { Editor.Run.Metadata.RegionName = value; Editor.Changed(true); });
        var emulator = new CheckBox { Content = "Uses emulator", IsChecked = Editor.Run.Metadata.UsesEmulator, IsEnabled = info == null || info.EmulatorAllowed };
        emulator.IsCheckedChanged += (_, _) => { Editor.Run.Metadata.UsesEmulator = emulator.IsChecked == true; Editor.Changed(true); };
        metadata.Children.Add(emulator);
        if (info != null)
        {
            foreach (Variable variable in info.Variables)
            {
                Editor.Run.Metadata.VariableValueNames.TryGetValue(variable.Name, out string value);
                if (variable.IsUserDefined)
                {
                    metadata.Children.Add(Label(variable.Name));
                    metadata.Children.Add(Field(value, text => { SetVariable(variable.Name, text); return true; }));
                }
                else AddMetadataChoice(variable.Name, variable.Values.Select(v => v.Value).ToArray(), value, text => SetVariable(variable.Name, text));
            }
        }
        else
        {
            foreach (var pair in Editor.Run.Metadata.VariableValueNames.ToArray())
            {
                metadata.Children.Add(Label(pair.Key));
                metadata.Children.Add(Field(pair.Value, text => { SetVariable(pair.Key, text); return true; }));
            }
            Button(metadata, "Add speedrun.com variable…", () =>
            {
                string name = "", val = "";
                if (PlatformServices.Current.Prompt(this, "Add variable", "Variable name:", "Value:", ref name, ref val) != DialogResult.OK || string.IsNullOrWhiteSpace(name)) return;
                SetVariable(name, val);
                RenderMetadata(info);
            });
        }
        metadata.Children.Add(Label("Custom variables"));
        foreach (var pair in Editor.Run.Metadata.CustomVariables.ToArray())
        {
            string key = pair.Key;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock { Text = key, Width = 160, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(Field(pair.Value.Value, text => { Editor.Run.Metadata.SetCustomVariable(key, text); Editor.Changed(); return true; }));
            var permanent = new CheckBox { Content = "Permanent", IsChecked = pair.Value.IsPermanent, IsEnabled = !pair.Value.IsPermanent };
            permanent.IsCheckedChanged += (_, _) => { if (permanent.IsChecked == true) { pair.Value.AsPermanent(); permanent.IsEnabled = false; Editor.Changed(); } };
            row.Children.Add(permanent);
            Button(row, "Remove", () => { Editor.Run.Metadata.CustomVariables.Remove(key); Editor.Changed(); RenderMetadata(info); });
            metadata.Children.Add(row);
        }
        Button(metadata, "Add custom variable…", () =>
        {
            string name = "", value = "";
            if (PlatformServices.Current.Prompt(this, "Custom variable", "Name:", "Value:", ref name, ref value) != DialogResult.OK || string.IsNullOrWhiteSpace(name)) return;
            Editor.Run.Metadata.GetOrAddCustomVariable(name).AsPermanent().Value = value;
            Editor.Changed(); RenderMetadata(info);
        });
        var actions = new WrapPanel();
        Button(actions, "Refresh lookups", () => _ = LoadMetadataAsync());
        Button(actions, "Associate run…", AssociateRun);
        Button(actions, "Show on speedrun.com", () =>
        {
            if (!string.IsNullOrEmpty(Editor.Run.Metadata.RunID)) PlatformServices.Current.OpenUrl("https://www.speedrun.com/run/" + Editor.Run.Metadata.RunID);
        });
        Button(actions, "Clear association", () => { Editor.Run.Metadata.RunID = null; Editor.Changed(); RenderMetadata(info); });
        metadata.Children.Add(actions);
        metadata.Children.Add(Label("Associated run: " + (Editor.Run.Metadata.RunID ?? "None")));
        metadata.Children.Add(Label("Rules"));
        metadata.Children.Add(new SelectableTextBlock
        {
            Text = CleanBBCode(info?.Rules) is { Length: > 0 } cleanRules ? cleanRules : "Load speedrun.com metadata to view category rules.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MaxWidth = 940
        });
    }

    private void SetVariable(string name, string value)
    {
        if (string.IsNullOrEmpty(value)) Editor.Run.Metadata.VariableValueNames.Remove(name);
        else Editor.Run.Metadata.VariableValueNames[name] = value;
        Editor.Changed(true);
    }

    private void AddMetadataChoice(string label, string[] choices, string value, Action<string> commit)
    {
        metadata.Children.Add(Label(label));
        if (choices == null) metadata.Children.Add(Field(value, text => { commit(text); return true; }));
        else
        {
            var items = new List<string> { "" };
            if (!string.IsNullOrEmpty(value) && !choices.Contains(value)) items.Add(value);
            items.AddRange(choices);
            var combo = new ComboBox { ItemsSource = items.ToArray(), SelectedItem = value ?? "", MinWidth = 280 };
            combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string text) commit(text); };
            metadata.Children.Add(combo);
        }
    }

    private async void AssociateRun()
    {
        string url = "";
        if (PlatformServices.Current.Prompt(this, "Associate speedrun.com run", "Run URL:", ref url) != DialogResult.OK) return;
        try
        {
            var run = await Task.Run(() => SpeedrunCom.Client.Runs.GetRunFromSiteUri(url));
            if (closed) return;
            if (run == null) throw new ArgumentException("The URL is not a valid speedrun.com run URL.");
            Editor.Run.PatchRun(run);
            game.Text = Editor.Run.GameName; category.Text = Editor.Run.CategoryName;
            Editor.Changed(); RefreshGrid(); await LoadMetadataAsync();
        }
        catch (Exception ex) { if (!closed) status.Text = "Association failed: " + ex.Message; }
    }

    private async void DownloadGameImage(bool boxart)
    {
        try
        {
            Image image = await Task.Run(() =>
            {
                Game info = Editor.Run.Metadata.Game;
                Image result = info == null ? null : boxart ? info.Assets.GetBoxartImage() : info.Assets.GetIconImage();
                return result ?? (boxart ? Twitch.Instance.GetGameBoxArt(Editor.Run.GameName) : null);
            });
            if (closed) { image?.Dispose(); return; }
            if (image == null) throw new InvalidOperationException("No game image was found.");
            Editor.SetIcon(-1, image);
            RefreshGameIcon();
        }
        catch (Exception ex) { if (!closed) status.Text = "Image download failed: " + ex.Message; }
    }

    private async void DownloadImageUrl(int targetRow)
    {
        string url = "";
        if (PlatformServices.Current.Prompt(this, "Download icon", "Image URL:", ref url) != DialogResult.OK) return;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            byte[] bytes = await client.GetByteArrayAsync(url);
            if (closed) return;
            using var stream = new MemoryStream(bytes);
            Editor.SetIcon(targetRow, Image.FromStream(stream));
            if (targetRow < 0) RefreshGameIcon();
            else RefreshGrid();
        }
        catch (Exception ex) { if (!closed) status.Text = "Image download failed: " + ex.Message; }
    }

    private void ActivateSplitter()
    {
        AutoSplitter splitter = Editor.Run.AutoSplitter;
        splitter.Activate(request.State);
        if (splitter.IsActivated)
        {
            if (!request.State.Settings.ActiveAutoSplitters.Contains(Editor.Run.GameName)) request.State.Settings.ActiveAutoSplitters.Add(Editor.Run.GameName);
            if (Editor.Run.AutoSplitterSettings?.GetAttribute("gameName") == Editor.Run.GameName) splitter.Component.SetSettings(Editor.Run.AutoSplitterSettings);
        }
    }

    private void RefreshAutoSplitter()
    {
        autoSplitter.Children.Clear();
        AutoSplitter splitter = Editor.Run.AutoSplitter;
        autoSplitter.Children.Add(Label(splitter?.Description ?? "There is no Auto Splitter available for this game."));
        if (splitter == null) return;
        autoSplitter.Children.Add(Label("Type: " + splitter.Type));
        autoSplitter.Children.Add(Label("Status: " + (splitter.IsActivated ? "Active" : "Inactive")));
        Button(autoSplitter, splitter.IsActivated ? "Deactivate" : "Activate", () => TryAction(() =>
        {
            if (splitter.IsActivated) { splitter.Deactivate(); request.State.Settings.ActiveAutoSplitters.Remove(Editor.Run.GameName); }
            else ActivateSplitter();
            Editor.Changed(); RefreshAutoSplitter();
        }));
        if (!string.IsNullOrEmpty(splitter.Website)) Button(autoSplitter, "Website", () => PlatformServices.Current.OpenUrl(splitter.Website));
        if (splitter.IsActivated)
        {
            Button(autoSplitter, "Settings…", () =>
            {
                var document = new XmlDocument();
                XmlNode original = splitter.Component.GetSettings(document);
                var dialog = new RunEditorChildWindow("Auto Splitter Settings", ComponentSettingsViews.Create(splitter.Component, LayoutMode.Vertical));
                if (UiThread.RunSync(() => dialog.ShowModalAsync(this)) == DialogResult.OK)
                {
                    XmlElement element = document.CreateElement("AutoSplitterSettings");
                    element.InnerXml = splitter.Component.GetSettings(document).InnerXml;
                    element.SetAttribute("gameName", Editor.Run.GameName);
                    Editor.Run.AutoSplitterSettings = element;
                    Editor.Changed();
                }
                else splitter.Component.SetSettings(original);
            });
        }
    }
}

internal sealed class RunEditorChildWindow : DialogWindow
{
    public RunEditorChildWindow(string title, Control content) : base(title)
    {
        SetBody(new ScrollViewer { Content = content, MaxHeight = 500, MinWidth = 380 });
        AddButton("OK", DialogResult.OK, isDefault: true);
        AddButton("Cancel", DialogResult.Cancel, isCancel: true);
    }
}
