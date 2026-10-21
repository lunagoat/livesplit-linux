# TotalPlaytime settings UI spec

Model: `LiveSplit.UI.Components.TotalPlaytimeSettings`, an observable `SettingsBase`. `GetSettingsControl(mode)` sets `Mode` and returns this model. Single page, no tabs, four-column table (159 / 29 / 29 / remaining width). All labels are translation keys. All edits commit immediately. No tooltips, numeric controls, or font pickers.

Controls in row order:

- "Background Color:": first color swatch binds `BackgroundColor`, visible only when `IsColor1Visible`; second swatch always visible, binds `BackgroundSwatch2Color` (edits `BackgroundColor` for Plain, otherwise `BackgroundColor2`). Drop-down list binds `GradientString`; `GradientOptions`: Plain, Vertical, Horizontal.
- "Display 2 Rows": checkbox edits `Display2Rows`, enabled by `IsDisplay2RowsEnabled` (vertical mode). Show `Display2RowsShown`: horizontal mode forces a disabled checked presentation without overwriting the saved value.
- Same row: "Show Total Hours" checkbox binds `ShowTotalHours` (false shows days).
- Group "Text Color": "Override Layout Settings" checkbox binds `OverrideTextColor`; "Color:" label and swatch (`TextColor`) enabled by `IsTextColorEnabled`.
- Group "Time Color": "Override Layout Settings" checkbox binds `OverrideTimeColor`; "Color:" label and swatch (`TimeColor`) enabled by `IsTimeColorEnabled`.

Defaults: white text/time colors, overrides off, transparent background colors, Plain gradient, Display2Rows false. ShowTotalHours false. Mode is runtime view context (initially Horizontal); CurrentState, where present, is runtime context. Neither is serialized. No hidden or legacy settings beyond these context properties; all persisted settings remain in the model.
