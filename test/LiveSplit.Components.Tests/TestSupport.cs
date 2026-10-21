using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace LiveSplit.Components.Tests;

internal static class TestSupport
{
    private static readonly object Gate = new();
    private static bool factoriesLoaded;

    /// <summary>Directory for the PNGs the rendering tests produce. Override with LS_COMPONENT_TEST_OUT.</summary>
    public static string OutDir
    {
        get
        {
            string dir = Environment.GetEnvironmentVariable("LS_COMPONENT_TEST_OUT");
            if (string.IsNullOrEmpty(dir))
            {
                dir = Path.Combine(Path.GetTempPath(), "livesplit-component-tests");
            }

            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// Registers the component factories the same way the app does: a "Components" folder holding the component
    /// assemblies, found through <see cref="ComponentManager"/>. Only the component dlls are copied (not Core), like
    /// the real component output folder, so this also proves components load against the host's Core.
    /// </summary>
    public static void EnsureFactories()
    {
        lock (Gate)
        {
            if (factoriesLoaded)
            {
                return;
            }

            string baseDir = Path.Combine(Path.GetTempPath(), "livesplit-component-tests-host-" + Environment.ProcessId);
            string componentsDir = Path.Combine(baseDir, ComponentManager.PATH_COMPONENTS);
            Directory.CreateDirectory(componentsDir);
            foreach (string name in new[] { "LiveSplit.Timer", "LiveSplit.Title", "LiveSplit.Text", "LiveSplit.BlankSpace", "LiveSplit.SumOfBest", "LiveSplit.RunPrediction", "LiveSplit.TotalPlaytime", "LiveSplit.WorldRecord" })
            {
                File.Copy(Path.Combine(AppContext.BaseDirectory, name + ".dll"), Path.Combine(componentsDir, name + ".dll"), true);
            }

            ComponentManager.BasePath = baseDir;
            ComponentManager.RaceProviderFactories ??= new Dictionary<string, IRaceProviderFactory>(); // StandardSettingsFactory reads it
            _ = ComponentManager.LoadLayoutComponent("LiveSplit.Timer.dll", null);
            factoriesLoaded = true;
        }
    }

    public static string SamplePath(string name) => Path.Combine(AppContext.BaseDirectory, "Samples", name);

    public static IRun CreateRun()
    {
        var run = new Run(new StandardComparisonGeneratorsFactory())
        {
            GameName = "Super Metroid",
            CategoryName = "Any% (Japan)",
            AttemptCount = 1337,
        };

        run.Add(new Segment("Ceres Station", new Time(TimeSpan.FromSeconds(60)), new Time(TimeSpan.FromSeconds(58))));
        run.Add(new Segment("Morph Ball", new Time(TimeSpan.FromSeconds(150)), new Time(TimeSpan.FromSeconds(85))));
        run.Add(new Segment("Kraid", new Time(TimeSpan.FromSeconds(420)), new Time(TimeSpan.FromSeconds(250))));
        return run;
    }

    public static LiveSplitState CreateState(IRun run = null)
    {
        EnsureFactories();
        run ??= CreateRun();
        var layout = new Layout { Settings = new StandardLayoutSettingsFactory().Create() };
        var state = new LiveSplitState(run, null, layout, layout.Settings, new StandardSettingsFactory().Create());
        state.CurrentComparison = Run.PersonalBestComparisonName;
        return state;
    }

    /// <summary>Puts the state into the running phase with the given elapsed time, as if it was split <paramref name="splitIndex"/> times.</summary>
    public static void MakeRunning(LiveSplitState state, TimeSpan elapsed, int splitIndex)
    {
        state.CurrentPhase = TimerPhase.Running;
        state.CurrentSplitIndex = splitIndex;
        state.AdjustedStartTime = state.StartTimeWithOffset = state.StartTime = TimeStamp.Now - elapsed;
        for (int i = 0; i < splitIndex; i++)
        {
            state.Run[i].SplitTime = new Time(TimeSpan.FromSeconds(55 * (i + 1)));
        }
    }

    public static Bitmap CreateIcon()
    {
        var icon = new Bitmap(32, 32);
        using Graphics g = Graphics.FromImage(icon);
        g.Clear(Color.FromArgb(200, 40, 40));
        g.FillRectangle(Brushes.Gold, 6, 6, 20, 20);
        return icon;
    }

    /// <summary>
    /// Renders the components through the real <see cref="ComponentRenderer"/> into a PNG and returns the bitmap.
    /// <paramref name="fixedSize"/> is the width in vertical mode and the height in horizontal mode; the other dimension
    /// is the layout's natural size (what a freshly opened window would have), measured after warm-up passes because
    /// components size themselves from text they measured while drawing.
    /// </summary>
    public static Bitmap Render(LiveSplitState state, IEnumerable<IComponent> components, LayoutMode mode, int fixedSize, string pngName)
    {
        List<IComponent> list = components.ToList();
        state.Layout.LayoutComponents = [.. list.Select(x => (ILayoutComponent)new LayoutComponent("test.dll", x))];
        var renderer = new ComponentRenderer { VisibleComponents = list };

        using (var scratch = new Bitmap(1000, 1000))
        {
            for (int pass = 0; pass < 3; pass++)
            {
                renderer.CalculateOverallSize(mode);
                DrawPass(renderer, state, scratch, mode, mode == LayoutMode.Vertical ? fixedSize : (int)renderer.OverallSize, mode == LayoutMode.Vertical ? (int)renderer.OverallSize : fixedSize);
            }
        }

        renderer.CalculateOverallSize(mode);
        int size = (int)Math.Ceiling(renderer.OverallSize);
        int width = mode == LayoutMode.Vertical ? fixedSize : size;
        int height = mode == LayoutMode.Vertical ? size : fixedSize;
        var bitmap = new Bitmap(width, height);
        DrawPass(renderer, state, bitmap, mode, width, height);
        bitmap.Save(Path.Combine(OutDir, pngName), System.Drawing.Imaging.ImageFormat.Png);
        return bitmap;
    }

    private static void DrawPass(ComponentRenderer renderer, LiveSplitState state, Bitmap bitmap, LayoutMode mode, int width, int height)
    {
        var invalidator = new Invalidator(null);
        renderer.Update(invalidator, state, width, height, mode);
        using Graphics g = Graphics.FromImage(bitmap);
        g.Clear(Color.FromArgb(15, 15, 15));
        float scale = mode == LayoutMode.Vertical ? height / renderer.OverallSize : width / renderer.OverallSize;
        g.ScaleTransform(scale, scale);
        renderer.Render(g, state, width / scale, height / scale, mode, new Region(new RectangleF(0, 0, width, height)));
    }

    public static float OverallSize(IEnumerable<IComponent> components, LayoutMode mode)
    {
        var renderer = new ComponentRenderer { VisibleComponents = components.ToList() };
        renderer.CalculateOverallSize(mode);
        return renderer.OverallSize;
    }

    public static string Xml(XmlNode node) => node.OuterXml;

    public static XmlNode RoundTrip(Func<XmlDocument, XmlNode> save, Action<XmlNode> load)
    {
        var doc = new XmlDocument();
        XmlNode node = save(doc);
        doc.AppendChild(node);
        load(node);
        return node;
    }

    /// <summary>Pixel bounds of everything that is not the clear colour in the given rectangle.</summary>
    public static Rectangle? ContentBounds(Bitmap bitmap, Color background, Rectangle area)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = area.Top; y < area.Bottom; y++)
        {
            for (int x = area.Left; x < area.Right; x++)
            {
                Color c = bitmap.GetPixel(x, y);
                if (Math.Abs(c.R - background.R) + Math.Abs(c.G - background.G) + Math.Abs(c.B - background.B) > 24)
                {
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        return maxX < 0 ? null : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }

    public static IEnumerable<PropertyInfo> SettableProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.GetSetMethod() != null && x.GetIndexParameters().Length == 0);
}
