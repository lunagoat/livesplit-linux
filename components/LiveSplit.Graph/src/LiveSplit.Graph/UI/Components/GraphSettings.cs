using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using System;
using System.Drawing;
using System.Linq;
using System.Xml;

namespace LiveSplit.UI.Components;

public class GraphSettings : SettingsBase, ICloneable
{
    private float graphHeight;
    public float GraphHeight
    {
        get => graphHeight;
        set
        {
            if (SetField(ref graphHeight, value))
            {
                OnPropertyChanged(nameof(GraphHeightScaled));
                OnPropertyChanged(nameof(SizeValue));
            }
        }
    }
    public float GraphHeightScaled { get => GraphHeight / 5; set => GraphHeight = value * 5; }
    private float graphWidth;
    public float GraphWidth
    {
        get => graphWidth;
        set
        {
            if (SetField(ref graphWidth, value))
            {
                OnPropertyChanged(nameof(GraphWidthScaled));
                OnPropertyChanged(nameof(SizeValue));
            }
        }
    }
    public float GraphWidthScaled { get => GraphWidth / 10; set => GraphWidth = value * 10; }

    private Color behindGraphColor;
    public Color BehindGraphColor
    {
        get => behindGraphColor;
        set => SetField(ref behindGraphColor, value);
    }
    private Color aheadGraphColor;
    public Color AheadGraphColor
    {
        get => aheadGraphColor;
        set => SetField(ref aheadGraphColor, value);
    }
    private Color gridlinesColor;
    public Color GridlinesColor
    {
        get => gridlinesColor;
        set => SetField(ref gridlinesColor, value);
    }
    private Color partialFillColorBehind;
    public Color PartialFillColorBehind
    {
        get => partialFillColorBehind;
        set => SetField(ref partialFillColorBehind, value);
    }
    private Color completeFillColorBehind;
    public Color CompleteFillColorBehind
    {
        get => completeFillColorBehind;
        set => SetField(ref completeFillColorBehind, value);
    }
    private Color partialFillColorAhead;
    public Color PartialFillColorAhead
    {
        get => partialFillColorAhead;
        set => SetField(ref partialFillColorAhead, value);
    }
    private Color completeFillColorAhead;
    public Color CompleteFillColorAhead
    {
        get => completeFillColorAhead;
        set => SetField(ref completeFillColorAhead, value);
    }
    private Color graphColor;
    public Color GraphColor
    {
        get => graphColor;
        set => SetField(ref graphColor, value);
    }
    private Color graphGoldColor;
    public Color GraphGoldColor
    {
        get => graphGoldColor;
        set => SetField(ref graphGoldColor, value);
    }
    private Color shadowsColor;
    public Color ShadowsColor
    {
        get => shadowsColor;
        set => SetField(ref shadowsColor, value);
    }
    private Color graphLinesColor;
    public Color GraphLinesColor
    {
        get => graphLinesColor;
        set => SetField(ref graphLinesColor, value);
    }

    private bool isLiveGraph;
    public bool IsLiveGraph
    {
        get => isLiveGraph;
        set => SetField(ref isLiveGraph, value);
    }
    private bool flipGraph;
    public bool FlipGraph
    {
        get => flipGraph;
        set => SetField(ref flipGraph, value);
    }
    private bool showBestSegments;
    public bool ShowBestSegments
    {
        get => showBestSegments;
        set
        {
            if (SetField(ref showBestSegments, value))
            {
                OnPropertyChanged(nameof(IsBestSegmentColorEnabled));
            }
        }
    }

    private LayoutMode mode;
    public LayoutMode Mode
    {
        get => mode;
        set
        {
            if (SetField(ref mode, value))
            {
                OnPropertyChanged(nameof(SizeValue));
                OnPropertyChanged(nameof(SizeMinimum));
                OnPropertyChanged(nameof(SizeMaximum));
                OnPropertyChanged(nameof(SizeLabel));
            }
        }
    }

    private string comparison;
    public string Comparison
    {
        get => comparison;
        set
        {
            if (SetField(ref comparison, value))
            {
                OnPropertyChanged(nameof(ComparisonOptions));
            }
        }
    }
    private LiveSplitState currentState;
    public LiveSplitState CurrentState
    {
        get => currentState;
        set
        {
            if (SetField(ref currentState, value))
            {
                OnPropertyChanged(nameof(ComparisonOptions));
            }
        }
    }

    public string[] ComparisonOptions => GetComparisonOptions(Comparison);
    private string[] GetComparisonOptions(string selected)
        => new[] { "Current Comparison" }
            .Concat(CurrentState?.Run.Comparisons.Where(x => x is not BestSplitTimesComparisonGenerator.ComparisonName and not NoneComparisonGenerator.ComparisonName) ?? Enumerable.Empty<string>())
            .Concat(string.IsNullOrEmpty(selected) ? [] : new[] { selected }).Distinct().ToArray();
    public bool IsBestSegmentColorEnabled => ShowBestSegments;
    public float SizeValue
    {
        get => Mode == LayoutMode.Vertical ? GraphHeightScaled : GraphWidthScaled;
        set { if (Mode == LayoutMode.Vertical) { GraphHeightScaled = value; } else { GraphWidthScaled = value; } }
    }
    public float SizeMinimum => 3;
    public float SizeMaximum => 40;
    public string SizeLabel => Mode == LayoutMode.Vertical ? "Height:" : "Width:";

    public GraphSettings()
    {
        GraphHeight = 120;
        GraphWidth = 180;
        BehindGraphColor = Color.FromArgb(115, 40, 40);
        AheadGraphColor = Color.FromArgb(40, 115, 52);
        GridlinesColor = Color.FromArgb(0x50, 0x0, 0x0, 0x0);
        PartialFillColorBehind = Color.FromArgb(25, 255, 255, 255);
        CompleteFillColorBehind = Color.FromArgb(50, 255, 255, 255);
        PartialFillColorAhead = Color.FromArgb(25, 255, 255, 255);
        CompleteFillColorAhead = Color.FromArgb(50, 255, 255, 255);
        GraphColor = Color.White;
        GraphGoldColor = Color.FromArgb(216, 175, 31);
        ShadowsColor = Color.FromArgb(0x38, 0x0, 0x0, 0x0);
        GraphLinesColor = Color.White;
        IsLiveGraph = true;
        FlipGraph = false;
        ShowBestSegments = false;
        Comparison = "Current Comparison";

    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);

        GraphHeight = SettingsHelper.ParseFloat(element["Height"]);
        GraphWidth = SettingsHelper.ParseFloat(element["Width"]);
        BehindGraphColor = SettingsHelper.ParseColor(element["BehindGraphColor"]);
        AheadGraphColor = SettingsHelper.ParseColor(element["AheadGraphColor"]);
        GridlinesColor = SettingsHelper.ParseColor(element["GridlinesColor"]);
        FlipGraph = SettingsHelper.ParseBool(element["FlipGraph"], false);
        Comparison = SettingsHelper.ParseString(element["Comparison"], "Current Comparison");
        ShowBestSegments = SettingsHelper.ParseBool(element["ShowBestSegments"], false);
        GraphGoldColor = SettingsHelper.ParseColor(element["GraphGoldColor"], Color.Gold);
        GraphColor = SettingsHelper.ParseColor(element["GraphColor"]);
        ShadowsColor = SettingsHelper.ParseColor(element["ShadowsColor"]);
        GraphLinesColor = SettingsHelper.ParseColor(element["GraphLinesColor"]);
        IsLiveGraph = SettingsHelper.ParseBool(element["LiveGraph"]);

        if (version >= new Version(1, 2))
        {
            PartialFillColorBehind = SettingsHelper.ParseColor(element["PartialFillColorBehind"]);
            CompleteFillColorBehind = SettingsHelper.ParseColor(element["CompleteFillColorBehind"]);
            PartialFillColorAhead = SettingsHelper.ParseColor(element["PartialFillColorAhead"]);
            CompleteFillColorAhead = SettingsHelper.ParseColor(element["CompleteFillColorAhead"]);
        }
        else
        {
            PartialFillColorAhead = SettingsHelper.ParseColor(element["PartialFillColor"]);
            PartialFillColorBehind = SettingsHelper.ParseColor(element["PartialFillColor"]);
            CompleteFillColorAhead = SettingsHelper.ParseColor(element["CompleteFillColor"]);
            CompleteFillColorBehind = SettingsHelper.ParseColor(element["CompleteFillColor"]);
        }
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        XmlElement parent = document.CreateElement("Settings");
        CreateSettingsNode(document, parent);
        return parent;
    }

    public int GetSettingsHashCode()
    {
        return CreateSettingsNode(null, null);
    }

    private int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.5") ^
        SettingsHelper.CreateSetting(document, parent, "Height", GraphHeight) ^
        SettingsHelper.CreateSetting(document, parent, "Width", GraphWidth) ^
        SettingsHelper.CreateSetting(document, parent, "BehindGraphColor", BehindGraphColor) ^
        SettingsHelper.CreateSetting(document, parent, "AheadGraphColor", AheadGraphColor) ^
        SettingsHelper.CreateSetting(document, parent, "GridlinesColor", GridlinesColor) ^
        SettingsHelper.CreateSetting(document, parent, "PartialFillColorBehind", PartialFillColorBehind) ^
        SettingsHelper.CreateSetting(document, parent, "CompleteFillColorBehind", CompleteFillColorBehind) ^
        SettingsHelper.CreateSetting(document, parent, "PartialFillColorAhead", PartialFillColorAhead) ^
        SettingsHelper.CreateSetting(document, parent, "CompleteFillColorAhead", CompleteFillColorAhead) ^
        SettingsHelper.CreateSetting(document, parent, "GraphColor", GraphColor) ^
        SettingsHelper.CreateSetting(document, parent, "ShadowsColor", ShadowsColor) ^
        SettingsHelper.CreateSetting(document, parent, "GraphLinesColor", GraphLinesColor) ^
        SettingsHelper.CreateSetting(document, parent, "LiveGraph", IsLiveGraph) ^
        SettingsHelper.CreateSetting(document, parent, "FlipGraph", FlipGraph) ^
        SettingsHelper.CreateSetting(document, parent, "Comparison", Comparison) ^
        SettingsHelper.CreateSetting(document, parent, "ShowBestSegments", ShowBestSegments) ^
        SettingsHelper.CreateSetting(document, parent, "GraphGoldColor", GraphGoldColor);
    }

    public object Clone()
    {
        return new GraphSettings()
        {
            GraphHeight = GraphHeight,
            BehindGraphColor = BehindGraphColor,
            AheadGraphColor = AheadGraphColor,
            GridlinesColor = GridlinesColor,
            PartialFillColorBehind = PartialFillColorBehind,
            CompleteFillColorBehind = CompleteFillColorBehind,
            PartialFillColorAhead = PartialFillColorAhead,
            CompleteFillColorAhead = CompleteFillColorAhead,
            GraphColor = GraphColor,
            ShadowsColor = ShadowsColor,
            GraphLinesColor = GraphLinesColor
        };
    }

}
