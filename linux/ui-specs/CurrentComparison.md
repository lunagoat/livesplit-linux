# CurrentComparison settings UI spec

Model: `LiveSplit.UI.Components.CurrentComparisonSettings` (SettingsBase / INotifyPropertyChanged). `GetSettingsControl(mode)` assigns Mode and returns this model. One page, no tabs, tooltips, numeric inputs, or font pickers. Labels below are translation keys; translate through UiLocalizer. All controls commit immediately.

Controls in visual row order:

1. "Background Color:" — swatch #1 binds BackgroundColor, visible IsColor1Visible; swatch #2 binds BackgroundSwatch2Color, always visible; non-editable combo binds GradientString, items GradientOptions = Plain, Vertical, Horizontal. Plain hides #1 and #2 edits BackgroundColor; gradients show both and #2 edits BackgroundColor2.
- "Display 2 Rows" — checkbox binds Display2RowsShown; enabled IsDisplay2RowsEnabled. Horizontal mode shows checked and disabled without changing persisted Display2Rows; Vertical mode edits Display2Rows.
- Group "Comparing Against Label", nested group "Color": checkbox "Override Layout Settings" binds OverrideTextColor; label "Color:" + color swatch bind TextColor, enabled IsTextColorEnabled.
- Group "Comparison Text", nested group "Color": checkbox "Override Layout Settings" binds OverrideTimeColor; label "Color:" + color swatch bind TimeColor, enabled IsTimeColorEnabled.

Defaults: white text/time colors, all color overrides false, transparent background colors, Plain gradient, Display2Rows false. No additional defaults.

Hidden/runtime: Mode and CurrentState are supplied by the host and not persisted. No legacy-only persisted settings. XML serialization, defaults for missing elements, version and hash remain compatible with Windows.
