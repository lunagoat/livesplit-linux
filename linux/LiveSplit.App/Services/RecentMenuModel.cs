using LiveSplit.Options;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LiveSplit.Services;

public sealed class RecentMenuNode
{
    /// <summary>Raw (unescaped) text.</summary>
    public string Text { get; set; }

    /// <summary>The file this entry opens, null for group nodes.</summary>
    public string Path { get; set; }

    public List<RecentMenuNode> Children { get; } = [];

    internal string Kind { get; set; }
}

/// <summary>
/// The toolkit independent part of the "Open Splits" / "Open Layout" menus: groups the recent files by game and
/// category exactly like TimerForm.UpdateRecentSplits. Lists are stored oldest first, menus show the newest first.
/// </summary>
public static class RecentMenuModel
{
    private const string CategoryKind = "Category";
    private const string FileNameKind = "FileName";

    public static List<RecentMenuNode> BuildSplits(IEnumerable<RecentSplitsFile> recentSplits)
    {
        var result = new List<RecentMenuNode>();

        foreach (IGrouping<string, RecentSplitsFile> game in recentSplits
            .Reverse()
            .Where(x => !string.IsNullOrEmpty(x.Path))
            .GroupBy(x => x.GameName ?? ""))
        {
            var gameNode = new RecentMenuNode();

            foreach (IGrouping<string, RecentSplitsFile> category in game.GroupBy(x => x.CategoryName ?? ""))
            {
                var categoryNode = new RecentMenuNode { Kind = CategoryKind };

                foreach (RecentSplitsFile splitsFile in category)
                {
                    categoryNode.Children.Add(new RecentMenuNode
                    {
                        Text = System.IO.Path.GetFileName(splitsFile.Path),
                        Path = splitsFile.Path,
                        Kind = FileNameKind,
                    });
                }

                if (categoryNode.Children.Count == 1)
                {
                    categoryNode = categoryNode.Children[0];
                    if (!string.IsNullOrEmpty(category.Key))
                    {
                        categoryNode.Text = category.Key;
                        categoryNode.Kind = CategoryKind;
                    }
                }
                else
                {
                    categoryNode.Text = string.IsNullOrEmpty(category.Key) ? "Unknown Category" : category.Key;
                }

                gameNode.Children.Add(categoryNode);
            }

            string gameName;
            if (string.IsNullOrEmpty(game.Key))
            {
                gameName = "Unknown Game";

                if (gameNode.Children.Count == 1)
                {
                    gameNode = gameNode.Children[0];
                    gameName = gameNode.Text;
                    if (gameNode.Text == "Unknown Category")
                    {
                        gameName = "Unknown";
                    }
                }
            }
            else
            {
                gameName = game.Key;

                if (gameNode.Children.Count == 1)
                {
                    gameNode = gameNode.Children[0];
                    if (gameNode.Kind == CategoryKind)
                    {
                        if (!gameNode.Text.StartsWith("Unknown Category"))
                        {
                            gameName += " - " + gameNode.Text;
                        }
                    }
                    else
                    {
                        gameName += " (" + gameNode.Text + ")";
                    }
                }
            }

            gameNode.Text = gameName;
            result.Add(gameNode);
        }

        return result;
    }

    public static List<RecentMenuNode> BuildLayouts(IEnumerable<string> recentLayouts)
    {
        return [.. recentLayouts
            .Reverse()
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => new RecentMenuNode { Text = System.IO.Path.GetFileNameWithoutExtension(x), Path = x })];
    }
}
