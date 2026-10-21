# Text settings UI spec

Model: `LiveSplit.UI.Components.TextComponentSettings` (`SettingsBase`, `INotifyPropertyChanged`). `GetSettingsControl(mode)` sets `Settings.Mode` and returns the settings object. 4-column table (159/29/29/fill px), rows 29px unless noted. All text is a translation key. All bindings two-way and immediate, EXCEPT the two text boxes which only committed on lose-focus in WinForms (committing on every keystroke is fine).

Controls in order:

0. Label "Background Color:" | swatch #1 (`BackgroundColor`, visible `IsColor1Visible`) | swatch #2 (`BackgroundSwatch2Color`, always visible) | combo (last column) -> `GradientString`, items `TextComponentSettings.GradientOptions` = `Plain`, `Vertical`, `Horizontal` (enum `GradientType`).
   - Old DataBindings trick: swatch #2 edits `BackgroundColor2` when swatch #1 is visible (type is not Plain), otherwise `BackgroundColor`. `BackgroundSwatch2Color` implements that.
1. Checkbox "Display 2 Rows" -> `Display2Rows`. Enabled only in Vertical layout mode (`IsDisplay2RowsEnabled` == `Mode != Horizontal`). In Horizontal mode it is shown disabled and CHECKED (two rows is forced); use `Display2RowsShown` (read-only: `Mode == Horizontal || Display2Rows`) for the displayed checked state when disabled, e.g. bind `IsChecked` one-way to `Display2RowsShown` when disabled / two-way to `Display2Rows` when enabled (the model's `Display2Rows` is NOT changed by horizontal mode).
2. Checkbox "Custom Variable" -> `CustomVariable`. Tooltip title "Custom Variable", text "Specifies whether to use a custom variable to display a dynamic value from an autosplitter."
3. GroupBox "Left Text" (row height 141, spans all):
   - Label "Text:" | textbox -> `Text1`
   - GroupBox "Color": checkbox "Override Layout Settings" -> `OverrideTextColor`; label "Color:" + swatch -> `TextColor`, both enabled only when `OverrideTextColor` (`IsTextColorEnabled`).
4. GroupBox "Right Text" (row height 141, spans all):
   - Label `Text2Label` (text is "Custom Variable Name:" when `CustomVariable` else "Text:", already a model property) | textbox -> `Text2`
   - GroupBox "Color": checkbox "Override Layout Settings" -> `OverrideTimeColor`; label "Color:" + swatch -> `TimeColor`, both enabled only when `OverrideTimeColor` (`IsTimeColorEnabled`).

The left label of the left box is always "Text:". Never shown in UI (legacy, read-only from old files): `OverrideFont1/Font1/OverrideFont2/Font2` (migrated via `TextComponent.MigrateFontOverrides`). `CurrentState` is set by the component, not user data.
