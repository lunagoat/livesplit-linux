# Title settings UI spec

Model: `LiveSplit.UI.Components.TitleSettings` (`SettingsBase`, `INotifyPropertyChanged`). `GetSettingsControl(mode)` returns the settings object (no mode dependency). Fixed 4-column table (151/29/29/fill px), rows 29px unless noted. All text is a translation key. All bindings two-way, immediate.

Controls in order:

0. Label "Background Color:" | swatch #1 (`BackgroundColor`, visible `IsColor1Visible`) | swatch #2 (`BackgroundSwatch2Color`, always visible) | combo (spans last column) bound to `GradientString`, items `TitleSettings.GradientOptions` = `Plain`, `Vertical`, `Horizontal` (enum `GradientType`).
   - Old DataBindings trick: swatch #2 edits `BackgroundColor2` when swatch #1 is visible (type is not Plain), otherwise it edits `BackgroundColor`. `BackgroundSwatch2Color` implements this; bind swatch #1 -> `BackgroundColor`, swatch #2 -> `BackgroundSwatch2Color`.
1. GroupBox "Title Color" (row height 85, spans all columns):
   - Checkbox "Override Layout Settings" -> `OverrideTitleColor`
   - Label "Color:" + swatch -> `TitleColor`; both enabled only when `OverrideTitleColor` (`IsTitleColorEnabled`).
2. Checkbox "Show Game Name" -> `ShowGameName` (column 0, spans 3) | Checkbox "Show Category Name" -> `ShowCategoryName` (column 3)
3. Checkbox "Show Finished Runs Count" -> `ShowFinishedRunsCount` (col 0, spans 3) | Checkbox "Show Attempt Count" -> `ShowAttemptCount` (col 3)
4. Checkbox "Display Game Icon" -> `DisplayGameIcon` (col 0, spans 3) | Checkbox "Display Game and Category in Single Line" -> `SingleLine` (col 3)
5. (row height 34) Label "Text Alignment:" | combo (spans 3), items in order `Automatic (Based on Icon)`, `Left`, `Center`; bound to `TextAlignment` (enum `AlignmentType`: Auto=0, Left=1, Center=2; use the index as the value, `TextAlignmentIndex` int property is provided).
6. GroupBox "Additional Category Info" (spans all): 2x2 grid of checkboxes:
   - (0,0) "Show Region" -> `ShowRegion`; (0,1) "Show Platform" -> `ShowPlatform`; (1,0) "Show Variables" -> `ShowVariables`.

No tooltips, no tabs. Never shown in UI (legacy, read-only from old files): `OverrideTitleFont`, `TitleFont`; the component migrates them to the layout component's `FontOverrides` (`MigrateFontOverrides`).
