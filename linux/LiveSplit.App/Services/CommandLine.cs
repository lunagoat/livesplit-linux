using System;
using System.Collections.Generic;
using System.IO;

namespace LiveSplit.Services;

/// <summary>Parsed command line. Compatible with the Windows flags (-s splits, -l layout) plus positional files.</summary>
public sealed class CommandLineOptions
{
    public string SplitsPath { get; set; }
    public string LayoutPath { get; set; }

    /// <summary>Render one frame offscreen to this PNG and exit (no window is shown).</summary>
    public string ScreenshotPath { get; set; }

    /// <summary>Run the timer for a moment before the screenshot so the frame shows a running state.</summary>
    public bool ScreenshotDemo { get; set; }

    /// <summary>Overrides the config directory (settings.cfg, logs...).</summary>
    public string ConfigDirectory { get; set; }

    /// <summary>Do not start the Unix socket / TCP / WebSocket command servers.</summary>
    public bool NoServer { get; set; }

    public bool ShowHelp { get; set; }

    public List<string> Errors { get; } = [];

    public const string Usage = """
        Usage: livesplit [options] [file.lss] [file.lsl]

          -s, --splits <file>       open this splits file
          -l, --layout <file>       open this layout file
          --screenshot <out.png>    render one frame to a PNG and exit
          --screenshot-demo         with --screenshot: start the timer and split first
          --config-dir <dir>        use <dir> for settings.cfg instead of ~/.config/LiveSplit
          --no-server               do not start the command servers
          -h, --help                show this text

        Files given without an option are classified by extension (.lsl is a layout, anything else is a splits file).
        """;

    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        var options = new CommandLineOptions();
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            string value = null;
            bool hasInlineValue = false;
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                int equals = arg.IndexOf('=');
                if (equals > 0)
                {
                    value = arg[(equals + 1)..];
                    arg = arg[..equals];
                    hasInlineValue = true;
                }
            }

            bool TakeValue(out string result)
            {
                if (hasInlineValue)
                {
                    result = value;
                    return true;
                }

                if (i + 1 < args.Count)
                {
                    result = args[++i];
                    return true;
                }

                options.Errors.Add($"Option {arg} needs a value.");
                result = null;
                return false;
            }

            switch (arg)
            {
                case "-s":
                case "--splits":
                    if (TakeValue(out string splits))
                    {
                        options.SplitsPath = splits;
                    }

                    break;
                case "-l":
                case "--layout":
                    if (TakeValue(out string layout))
                    {
                        options.LayoutPath = layout;
                    }

                    break;
                case "--screenshot":
                    if (TakeValue(out string shot))
                    {
                        options.ScreenshotPath = shot;
                    }

                    break;
                case "--screenshot-demo":
                    options.ScreenshotDemo = true;
                    break;
                case "--config-dir":
                    if (TakeValue(out string configDirectory))
                    {
                        options.ConfigDirectory = configDirectory;
                    }

                    break;
                case "--no-server":
                    options.NoServer = true;
                    break;
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;
                default:
                    if (arg.StartsWith('-') && arg.Length > 1)
                    {
                        options.Errors.Add($"Unknown option {arg}.");
                    }
                    else
                    {
                        options.AddFile(arg);
                    }

                    break;
            }
        }

        return options;
    }

    private void AddFile(string path)
    {
        // file:// URIs are what some file managers pass for %U.
        if (Uri.TryCreate(path, UriKind.Absolute, out Uri uri) && uri.IsFile)
        {
            path = uri.LocalPath;
        }

        if (string.Equals(Path.GetExtension(path), ".lsl", StringComparison.OrdinalIgnoreCase))
        {
            LayoutPath ??= path;
        }
        else
        {
            SplitsPath ??= path;
        }
    }
}
