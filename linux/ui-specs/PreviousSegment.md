# PreviousSegment settings UI spec

Model: `LiveSplit.UI.Components.PreviousSegmentSettings` (SettingsBase / INotifyPropertyChanged). `GetSettingsControl(mode)` assigns Mode and returns this model. One page, no tabs, tooltips, numeric inputs, or font pickers. Labels below are translation keys; translate through UiLocalizer. All controls commit immediately.

Controls in visual row order:

1. "Background Color:" — swatch #1 binds BackgroundColor, visible IsColor1Visible; swatch #2 binds BackgroundSwatch2Color, always visible; non-editable combo binds GradientString, items GradientOptions = Plain, Vertical, Horizontal. Plain hides #1 and #2 edits BackgroundColor; gradients show both and #2 edits BackgroundColor2.
2. "Comparison:" — non-editable combo binds Comparison; items ComparisonOptions: Current Comparison, then CurrentState.Run.Comparisons excluding Best Split Times and None, then the persisted selection if absent (including a filtered comparison). Re-read options when opening the view; changes to CurrentState/Comparison notify ComparisonOptions.
- "Display 2 Rows" — checkbox binds Display2RowsShown; enabled IsDisplay2RowsEnabled. Horizontal mode shows checked and disabled without changing persisted Display2Rows; Vertical mode edits Display2Rows.
- Group "Text Color": checkbox "Override Layout Settings" binds OverrideTextColor; label "Color:" + color swatch bind TextColor, enabled IsTextColorEnabled.
- Group "Delta Accuracy": checkbox "Drop Decimals When More Than 1 Minute" binds DropDecimals; nested group "Accuracy": radio buttons Seconds, Tenths, Hundredths, Milliseconds, bound to the DeltaAccuracy (TimeAccuracy) enum values. All four are supported, including persisted Milliseconds.
- Group "Possible Time Save": checkbox "Show Possible Time Save" binds ShowPossibleTimeSave. Nested group "Accuracy": radio buttons Seconds, Tenths, Hundredths, Milliseconds, bound to the TimeSaveAccuracy (TimeAccuracy) enum values. All four are supported, including persisted Milliseconds. The whole accuracy group is enabled IsTimeSaveAccuracyEnabled (ShowPossibleTimeSave), including Milliseconds.

Defaults: white text/time colors, all color overrides false, transparent background colors, Plain gradient, Display2Rows false. DeltaAccuracy and TimeSaveAccuracy Tenths; DropDecimals true; ShowPossibleTimeSave false; Comparison Current Comparison.

Hidden/runtime: Mode and CurrentState are supplied by the host and not persisted. No legacy-only persisted settings. XML serialization, defaults for missing elements, version and hash remain compatible with Windows.
