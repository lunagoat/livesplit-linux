# Graph — settings view contract

Model: `LiveSplit.UI.Components.GraphSettings`, derives `SettingsBase`.
Single page, no tabs/groups/tooltips. Selection-only comparison combo; all edits
commit immediately. Labels are translation keys. In row order (left/right columns):

| Label | Control | Binding |
|---|---|---|
| Comparison: | combo | `Comparison`, `ComparisonOptions`: Current Comparison, run comparisons excluding Best Split Times and None, plus saved selection if unavailable |
| Height: / Width: | slider, 3–40, step 1 | `SizeValue`, `SizeLabel`, `SizeMinimum`, `SizeMaximum`; vertical maps GraphHeightScaled = GraphHeight / 5 (15–200 pixels); horizontal maps GraphWidthScaled = GraphWidth / 10 (30–400 pixels) |
| Behind Background Color: | swatch | `BehindGraphColor` |
| Partial Fill Color (Behind): | swatch | `PartialFillColorBehind` |
| Ahead Background Color: | swatch | `AheadGraphColor` |
| Complete Fill Color (Behind): | swatch | `CompleteFillColorBehind` |
| Gridlines Color: | swatch | `GridlinesColor` |
| Partial Fill Color (Ahead): | swatch | `PartialFillColorAhead` |
| Main Graph Color: | swatch | `GraphColor` |
| Complete Fill Color (Ahead): | swatch | `CompleteFillColorAhead` |
| Shadows Color: | swatch | `ShadowsColor` |
| Separators Color: | swatch | `GraphLinesColor` |
| Show Best Segments | checkbox | `ShowBestSegments` |
| Best Segment Color: | swatch | `GraphGoldColor`; swatch and label enabled through `IsBestSegmentColorEnabled` |
| Live Graph | checkbox | `IsLiveGraph` |
| Flip Graph | checkbox | `FlipGraph` |

`Mode`/`CurrentState` are supplied by the component. Refresh comparison options when
opening the view if run comparisons changed. Both dimensions are retained. The
old Load handler clamped GraphHeightScaled to 3–40 even in horizontal mode; the
model leaves persisted dimensions intact and the view constrains slider input.
Defaults: height 120, width 180; behind RGB 115/40/40, ahead 40/115/52;
grid ARGB 80/0/0/0, partial fills 25/255/255/255, complete fills 50/255/255/255,
main/separators white, best RGB 216/175/31, shadow ARGB 56/0/0/0.
Live on; flip/best segments off; Current Comparison selected.
XML version 1.5; pre-1.2 PartialFillColor and CompleteFillColor map to both sides.
No hidden UI settings. `Clone` retains its legacy subset-copy semantics and is
not a settings-view action.
