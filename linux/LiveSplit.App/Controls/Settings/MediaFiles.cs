using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using LiveSplit.Hosting;
using System;
using System.IO;
using System.Linq;

namespace LiveSplit.Controls.Settings;

public static class MediaFiles
{
    public static Control Editor(SettingsPage page, string path, bool audio = false)
    {
        var box = new TextBox { IsReadOnly = audio, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        page.Bind(box, TextBox.TextProperty, audio ? "FilePathsText" : path, audio ? Avalonia.Data.BindingMode.OneWay : Avalonia.Data.BindingMode.TwoWay);
        if (audio)
        {
            page.Bind(box, ToolTip.TipProperty, "PathsTooltip", Avalonia.Data.BindingMode.OneWay);
            box.GotFocus += (_, _) => ToolTip.SetIsOpen(box, true);
            box.LostFocus += (_, _) => ToolTip.SetIsOpen(box, false);
        }
        var browse = new Button { Content = SettingsPage.T("Browse...") };
        browse.Click += async (_, _) =>
        {
            var provider = TopLevel.GetTopLevel(box)?.StorageProvider;
            if (provider == null) return;
            string existing = audio ? ((System.Collections.Generic.IEnumerable<string>)page.Read("FilePaths")).FirstOrDefault() : page.Read(path)?.ToString();
            existing = PathCompat.Resolve(existing ?? "");
            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = audio,
                SuggestedStartLocation = File.Exists(existing) ? await provider.TryGetFolderFromPathAsync(Path.GetDirectoryName(existing)) : null,
                SuggestedFileName = File.Exists(existing) ? Path.GetFileName(existing) : null,
                FileTypeFilter = new[] { new FilePickerFileType(audio ? "Audio Files" : "Video Files") { Patterns = audio ? new[] { "*.mp3", "*.wav", "*.aiff", "*.wma" } : new[] { "*.avi", "*.mpeg", "*.mpg", "*.mp4", "*.mov", "*.wmv", "*.m4v", "*.flv", "*.mkv", "*.ogg" } }, FilePickerFileTypes.All }
            });
            string[] paths = files.Select(f => f.TryGetLocalPath()).Where(p => p != null).ToArray();
            if (paths.Length > 0) page.Model.GetType().GetProperty(audio ? "FilePaths" : path).SetValue(page.Model, audio ? paths : paths[0]);
        };
        DragDrop.SetAllowDrop(box, true);
        box.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        box.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var paths = e.Data.GetFiles()?.Select(f => f.TryGetLocalPath()).Where(p => p != null).ToArray();
            if (paths == null || paths.Length == 0) return;
            if (audio) page.Model.GetType().GetMethod("DropFiles").Invoke(page.Model, new object[] { paths });
            else page.Model.GetType().GetProperty(path).SetValue(page.Model, paths[0]);
            e.Handled = true;
        });
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(audio ? "*,Auto,Auto" : "*,Auto"), ColumnSpacing = 6 };
        grid.Children.Add(box);
        Grid.SetColumn(browse, 1); grid.Children.Add(browse);
        if (audio) { var clear = page.Action("Clear", "Clear"); Grid.SetColumn(clear, 2); grid.Children.Add(clear); }
        return grid;
    }
}
