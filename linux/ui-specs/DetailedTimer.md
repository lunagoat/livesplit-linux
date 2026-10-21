# Detailed Timer — settings view contract

Model: `LiveSplit.UI.Components.DetailedTimerSettings`, derives `SettingsBase`.
Single scrolling page; no tabs or tooltips. Labels below are translation keys.
All controls commit immediately. Combos are selection-only. Sliders use integer
steps of 1. Font pickers update live (`Choose...`), allowed size 7–26.

In page order:

| Group / label | Control | Binding / behavior |
|---|---|---|
| Background Color: | two color swatches + combo | `BackgroundColor`, `BackgroundSwatch2Color`, `GradientString` / `GradientOptions`; first swatch visible through `IsColor1Visible`, second through `IsColor2Visible`. Plain shows only the second swatch, editing BackgroundColor; vertical/horizontal show both, second editing BackgroundColor2; delta variants hide both. |
| Timing Method: | combo | `TimingMethod`, `TimingMethodOptions`: Current Timing Method, Real Time, Game Time |
| Segment Time Comparisons / Hide Comparison 2 | checkbox | `HideComparison` |
| Comparison 1: | combo | `Comparison`, `ComparisonOptions` |
| Comparison 2: | combo | `Comparison2`, `Comparison2Options`, enabled through `IsComparison2Enabled` (including label) |
| Height: / Width: | slider | `SizeValue`, `SizeLabel`, `SizeMinimum`, `SizeMaximum`. Vertical: Height 20–150; horizontal: Width 50–500. |
| Segment Timer Size: | slider | `SegmentTimerSizeRatio`, 10–90 (percent) |
| Timer / Color / Override Layout Settings | checkbox | `OverrideTimerColors` |
| Timer / Color / Color: | swatch | `TimerColor`, enabled through `IsTimerColorEnabled` (including label) |
| Timer / Show Gradient | checkbox | `TimerShowGradient` |
| Timer / Format: | two combos | `DigitsFormat`, `Accuracy`; `DigitsFormatOptions` = 1, 00:01, 0:00:01, 00:00:01; `AccuracyOptions` = empty, .2, .23, .234 |
| Timer / Decimals Font Size: | slider | `DecimalsSize`, 10–50 |
| Segment Timer / Color: | swatch | `SegmentTimerColor` |
| Segment Timer / Show Gradient | checkbox | `SegmentTimerShowGradient` |
| Segment Timer / Format: | two combos | `SegmentDigitsFormat`, `SegmentAccuracy`; same options as Timer |
| Segment Timer / Decimals Font Size: | slider | `SegmentTimerDecimalsSize`, 10–50 |
| Segment Labels / Font: | font summary + Choose... button | `SegmentLabelsFontString`, `SegmentLabelsFont` |
| Segment Labels / Color: | swatch | `SegmentLabelsColor` |
| Segment Times / Font: | font summary + Choose... button | `SegmentTimesFontString`, `SegmentTimesFont` |
| Segment Times / Color: | swatch | `SegmentTimesColor` |
| Segment Times / Accuracy | radio group | `SegmentTimesAccuracy`: Seconds, Tenths, Hundredths, Milliseconds. Setter updates `SegmentTimesFormatter.Accuracy`. |
| Icon / Display Icon | checkbox | `DisplayIcon` |
| Icon / Size: | slider | `IconSize`, 20–100; label and slider enabled through `IsIconSizeEnabled` |
| Split Name / Show Split Name | checkbox | `ShowSplitName` |
| Split Name / Font: | font summary + Choose... button | `SplitNameFontString`, `SplitNameFont`; label, summary and button enabled through `IsSplitNameEnabled` |
| Split Name / Split Name Color: | swatch | `SplitNameColor`; swatch and label enabled through `IsSplitNameEnabled` |

Comparison options contain Current Comparison followed by the run's comparisons
excluding Best Split Times and None; preserve the selected value if unavailable.
`CurrentState` and `Mode` are supplied by the component. Refresh options when the
view opens if run comparisons changed. `Height`/`Width` retain both orientations.
`SegmentTimesFormatter` is a public rendering helper, not an editable control.
Defaults: height 75, width 200, ratio 40, gradients on, timer override off,
formats 1.23, comparison accuracy Hundredths, icon off/40, decimals sizes 35,
split name off, comparisons Current Comparison / Best Segments, second visible,
transparent Plain background. Fonts: Segoe UI 13 regular labels, 13 bold times,
15 regular split name, in pixels. Timer colors RGB 170; other text white.
XML version 1.5; legacy TimerUseSplitColors (inverted override), TimerAccuracy and
SegmentTimerAccuracy remain accepted through version migration. No hidden font
controls; all three persisted fonts are editable.
