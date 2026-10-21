# Delta settings UI spec

Model: `LiveSplit.UI.Components.DeltaSettings` (SettingsBase / INotifyPropertyChanged). `GetSettingsControl(mode)` assigns Mode and returns this model. One page, no tabs, tooltips, numeric inputs, or font pickers. Labels below are translation keys; translate through UiLocalizer. All controls commit immediately except the Delta custom text boxes, which historically commit on validation (focus loss).

Controls in visual row order:

1. "Background Color:" — swatch #1 binds BackgroundColor, visible IsColor1Visible; swatch #2 binds BackgroundSwatch2Color, always visible; non-editable combo binds GradientString, items GradientOptions = Plain, Vertical, Horizontal. Plain hides #1 and #2 edits BackgroundColor; gradients show both and #2 edits BackgroundColor2.
2. "Comparison:" — non-editable combo binds Comparison; items ComparisonOptions: Current Comparison, then CurrentState.Run.Comparisons excluding None, then the persisted selection if absent (including a filtered comparison). Re-read options when opening the view; changes to CurrentState/Comparison notify ComparisonOptions.
- "Display 2 Rows" — checkbox binds Display2RowsShown; enabled IsDisplay2RowsEnabled. Horizontal mode shows checked and disabled without changing persisted Display2Rows; Vertical mode edits Display2Rows.
- Group "Delta Accuracy": checkbox "Drop Decimals When More Than 1 Minute" binds DropDecimals; radio buttons Seconds, Tenths, Hundredths, Milliseconds, bound to the Accuracy (TimeAccuracy) enum values. All four are supported, including persisted Milliseconds.
- Group "Text": nested group "Color": checkbox "Override Layout Settings" binds OverrideTextColor; label "Color:" + swatch bind TextColor, enabled IsTextColorEnabled.
- Within "Text": checkbox "Override Default Label" binds OverrideText; checkbox "Different Label When Ahead" binds DifferentialText, enabled IsCustomTextEnabled. Next row: label CustomTextLabel ("Label:" or "Label When Behind:") and textbox CustomText, enabled IsCustomTextEnabled. Last row: "Label When Ahead:" + textbox CustomTextAhead, enabled IsCustomTextAheadEnabled = OverrideText && DifferentialText. DifferentialText determines CustomTextLabel even while override is off.

Defaults: white text/time colors, all color overrides false, transparent background colors, Plain gradient, Display2Rows false. Accuracy Tenths; DropDecimals true; Comparison Current Comparison; OverrideText and DifferentialText false; CustomText and CustomTextAhead empty.

Hidden/runtime: Mode and CurrentState are supplied by the host and not persisted. No legacy-only persisted settings. XML serialization, defaults for missing elements, version and hash remain compatible with Windows.
