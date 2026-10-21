# Blank Space settings UI spec

Model: `LiveSplit.UI.Components.BlankSpaceSettings` (`SettingsBase`, `INotifyPropertyChanged`). `GetSettingsControl(mode)` sets `Settings.Mode` and returns the settings object. 5-column table (157/29/29/95/fill px), rows 29px. All text is a translation key; all bindings two-way, immediate.

Controls in order:

0. Label "Background Color:" | swatch #1 (`BackgroundColor`, visible `IsColor1Visible`) | swatch #2 (`BackgroundSwatch2Color`, always visible) | combo (spans 2 columns) -> `GradientString`, items `BlankSpaceSettings.GradientOptions` = `Plain`, `Vertical`, `Horizontal` (enum `GradientType`).
   - Old DataBindings trick: swatch #2 edits `BackgroundColor2` when swatch #1 is visible (type is not Plain), otherwise it edits `BackgroundColor`. `BackgroundSwatch2Color` implements that.
1. Label `SizeLabel` ("Width:" in Horizontal layout mode, "Height:" in Vertical; model property) | slider (spans 4), no ticks, `SizeMinimum`..`SizeMaximum` = 1..1000 both modes, integer steps, bound to `SizeValue` (maps to `SpaceWidth` in Horizontal mode, `SpaceHeight` in Vertical). `Mode` changes raise PropertyChanged for the Size* properties.

No tooltips, no tabs, no hidden/legacy settings.
