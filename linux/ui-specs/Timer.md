# Timer settings UI spec

Model: `LiveSplit.UI.Components.TimerSettings` (`SettingsBase`, `INotifyPropertyChanged`). `GetSettingsControl(mode)` sets `Settings.Mode` and returns the settings object. Fixed-size dialog page in WinForms (~476x301), 5-column table, rows 29px unless noted. All labels are translation keys (`UiLocalizer.Translate`); the old `UiLocalizer.Apply(Form)` translated every label/checkbox/groupbox/tooltip text, the view should translate the same strings.
All controls bind two-way, update on every change (OnPropertyChanged).

Controls in order (row: control):

0. Label "Background Color:" | swatch #1 (color, `BackgroundColor`, visible: `IsColor1Visible`) | swatch #2 (color, `BackgroundSwatch2Color`, visible: `IsColor2Visible`) | combo "gradient type" (spans the rest), items `TimerSettings.GradientOptions` (see below); bound to `GradientString` (string, display text == value).
   - Swatch semantics are the old DataBindings trick: swatch #2 edits `BackgroundColor2` when `IsColor1Visible`, otherwise (Plain) it edits `BackgroundColor`. That mapping is already in `BackgroundSwatch2Color`, so just bind swatch #1 to `BackgroundColor` and swatch #2 to `BackgroundSwatch2Color`.
   - Gradient item list (value strings, in this order, enum `DeltasGradientType`): `Plain`, `Vertical`, `Horizontal`, `Plain With Delta Color`, `Vertical With Delta Color`, `Horizontal With Delta Color`. (Old combo showed `GetBackgroundTypeString` of the enum in enum order.) `GradientOptions` provides exactly the display strings; `GradientString` accepts them.
   - Visibility: `IsColor1Visible` = type is not Plain and does not contain "Delta" (Vertical, Horizontal). `IsColor2Visible` = type does not contain "Delta" (Plain, Vertical, Horizontal). For the "...With Delta Color" types both swatches are hidden (colour is derived from the timer colour).
1. Label "Timing Method:" | combo (spans 4), items `TimerSettings.TimingMethodOptions` = `Current Timing Method`, `Real Time`, `Game Time`; bound to `TimingMethod` (string).
2. Label `SizeLabel` ("Width:" in horizontal layout mode, "Height:" in vertical; model property, already translated) | slider (spans 4), no ticks, range `SizeMinimum`..`SizeMaximum` (Horizontal: 50..500, Vertical: 20..150), integer steps, bound to `SizeValue` (float; maps to `TimerWidth` in Horizontal mode, `TimerHeight` in Vertical mode). `Mode` is set by `GetSettingsControl`, and `SizeLabel/SizeMinimum/SizeMaximum/SizeValue` raise PropertyChanged when `Mode` changes.
3. GroupBox "Timer Color" (spans all 5 columns):
   - Checkbox "Override Layout Settings" -> `OverrideSplitColors`
   - Label "Color:" + swatch -> `TimerColor`; both enabled only when `OverrideSplitColors` (`IsTimerColorEnabled`).
4. Checkbox "Show Gradient" -> `ShowGradient`
5. Checkbox "Align to Center" -> `CenterTimer`
6. Label "Timer Format:" | combo `DigitsFormat` (items `DigitsFormatOptions`: `1`, `00:01`, `0:00:01`, `00:00:01`; WinForms used right-to-left text alignment) | combo `Accuracy` (items `AccuracyOptions`: empty string "", `.2`, `.23`, `.234`; the empty item displays as blank).
7. Label "Decimals Font Size:" | slider (spans 4) 10..50, no ticks -> `DecimalsSize` (float).

No tooltips. No tabs. Non-UI settings (never shown): none.
