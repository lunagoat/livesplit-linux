using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI.Components;
using LiveSplit.UI.LayoutFactories;
using System.Drawing;
using LiveSplit.Options;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace LiveSplit.UI.LayoutFactories
{
    /// <summary>The default layout shipped with LiveSplit (embedded DefaultLayout.lsl).</summary>
    public class StandardLayoutFactory : ILayoutFactory
    {
        public ILayout Create(LiveSplitState state)
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DefaultLayout.lsl")
                ?? throw new FileNotFoundException("The embedded default layout is missing.");
            using Stream available = KeepAvailableComponents(stream);
            ILayout layout = new XMLLayoutFactory(available).Create(state);

            layout.X = layout.Y = 100;
            CenturyGothicFix(layout);

            return layout;
        }

        /// <summary>
        /// While components are still being ported not all of them exist; the default layout then shows the ones that
        /// do instead of failing. Once every component is present this returns the layout unchanged.
        /// </summary>
        private static Stream KeepAvailableComponents(Stream layoutXml)
        {
            var document = new XmlDocument();
            document.Load(layoutXml);
            bool changed = false;
            XmlNodeList components = document.SelectNodes("/Layout/Components/Component");
            foreach (XmlNode component in components.Cast<XmlNode>().ToList())
            {
                string path = component["Path"]?.InnerText;
                if (!string.IsNullOrEmpty(path) && !(ComponentManager.ComponentFactories?.ContainsKey(path) ?? false))
                {
                    Log.Warning($"The default layout component {path} is not available; leaving it out.");
                    component.ParentNode.RemoveChild(component);
                    changed = true;
                }
            }

            var result = new MemoryStream();
            if (changed)
            {
                document.Save(result);
            }
            else
            {
                layoutXml.Position = 0;
                layoutXml.CopyTo(result);
            }

            result.Position = 0;
            return result;
        }

        public static void CenturyGothicFix(ILayout layout)
        {
            if (layout.Settings.TimerFont.Name != "Century Gothic")
            {
                layout.Settings.TimerFont = new Font("Calibri", layout.Settings.TimerFont.Size, FontStyle.Bold, GraphicsUnit.Pixel);
            }
        }
    }

    /// <summary>The layout used while no splits file is open: just a timer.</summary>
    public class TimerOnlyLayoutFactory : ILayoutFactory
    {
        public ILayout Create(LiveSplitState state)
        {
            var layout = new Layout();
            ILayoutComponent component = ComponentManager.LoadLayoutComponent("LiveSplit.Timer.dll", state);
            if (component != null)
            {
                layout.LayoutComponents.Add(component);
            }

            layout.VerticalWidth = 252;
            layout.VerticalHeight = 50;
            layout.HorizontalWidth = 252;
            layout.HorizontalHeight = 50;
            layout.X = 100;
            layout.Y = 100;
            layout.Mode = LayoutMode.Vertical;
            layout.Settings = new StandardLayoutSettingsFactory().Create();
            StandardLayoutFactory.CenturyGothicFix(layout);
            return layout;
        }
    }
}

namespace LiveSplit.Model.RunFactories
{
    /// <summary>An empty run with a single unnamed segment (timer only mode).</summary>
    public class StandardRunFactory : IRunFactory
    {
        public IRun Create(IComparisonGeneratorsFactory factory)
        {
            var run = new Run(factory)
            {
                GameName = "",
                CategoryName = ""
            };
            run.AddSegment("");
            return run;
        }
    }
}
