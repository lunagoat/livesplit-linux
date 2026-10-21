using Avalonia.Controls;
using LiveSplit.Controls.Settings;

namespace LiveSplit.View.ComponentSettings;

public sealed class GraphView : SettingsPage
{
    public GraphView(object model) : base(model)
    {
        Add(Combo("Comparison:", "Comparison", translate: false), Slider("", "SizeValue", 3, 40, size: true),
    Color("Behind Background Color:", "BehindGraphColor"), Color("Partial Fill Color (Behind):", "PartialFillColorBehind"),
    Color("Ahead Background Color:", "AheadGraphColor"), Color("Complete Fill Color (Behind):", "CompleteFillColorBehind"),
    Color("Gridlines Color:", "GridlinesColor"), Color("Partial Fill Color (Ahead):", "PartialFillColorAhead"),
    Color("Main Graph Color:", "GraphColor"), Color("Complete Fill Color (Ahead):", "CompleteFillColorAhead"),
    Color("Shadows Color:", "ShadowsColor"), Color("Separators Color:", "GraphLinesColor"),
    Check("Show Best Segments", "ShowBestSegments"), Color("Best Segment Color:", "GraphGoldColor", "IsBestSegmentColorEnabled"),
    Check("Live Graph", "IsLiveGraph"), Check("Flip Graph", "FlipGraph"));
    }
}
