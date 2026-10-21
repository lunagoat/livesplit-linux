# ComparisonTime settings UI spec

Model: `LiveSplit.UI.Components.ComparisonTimeSettings` (SettingsBase / INotifyPropertyChanged). `GetSettingsControl(mode)` assigns Mode and returns this model. One page, no tabs, tooltips, numeric inputs, or font pickers. Labels below are translation keys; translate through UiLocalizer. All controls commit immediately.

Controls in visual row order:

1. "Background Color:" — swatch #1 binds BackgroundColor, visible IsColor1Visible; swatch #2 binds BackgroundSwatch2Color, always visible; non-editable combo binds GradientString, items GradientOptions = Plain, Vertical, Horizontal. Plain hides #1 and #2 edits BackgroundColor; gradients show both and #2 edits BackgroundColor2.
2. "Comparison:" — non-editable combo binds Comparison; items ComparisonOptions: Current Comparison, then CurrentState.Run.Comparisons excluding Best Split Times and None, then the persisted selection if absent (including a filtered comparison). Re-read options when opening the view; changes to CurrentState/Comparison notify ComparisonOptions.
3. "Timing Method:" — non-editable combo binds TimingMethod; TimingMethodOptions = Current Timing Method, Real Time, Game Time.
- "Display 2 Rows" — checkbox binds Display2RowsShown; enabled IsDisplay2RowsEnabled. Horizontal mode shows checked and disabled without changing persisted Display2Rows; Vertical mode edits Display2Rows.
- Group "Text Color": checkbox "Override Layout Settings" binds OverrideTextColor; label "Color:" + color swatch bind TextColor, enabled IsTextColorEnabled.
- Group "Time", nested group "Color": checkbox "Override Layout Settings" binds OverrideTimeColor; label "Color:" + color swatch bind TimeColor, enabled IsTimeColorEnabled.
- Group "Time", nested group "Accuracy": radio buttons Seconds, Tenths, Hundredths, Milliseconds, bound to the Accuracy (TimeAccuracy) enum values. All four are supported, including persisted Milliseconds.
- Group "Time", nested group "Type": radio buttons "Final Time", "Split Time", "Segment Time" bind Type (TimeType.FinalTime / SplitTime / SegmentTime).

Defaults: white text/time colors, all color overrides false, transparent background colors, Plain gradient, Display2Rows false. Accuracy Seconds; Type FinalTime; Comparison Current Comparison; TimingMethod Current Timing Method.

Hidden/runtime: Mode and CurrentState are supplied by the host and not persisted. No legacy-only persisted settings. XML serialization, defaults for missing elements, version and hash remain compatible with Windows.
