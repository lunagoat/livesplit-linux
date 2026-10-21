# Splits settings UI spec

Models: `LiveSplit.UI.Components.SplitsSettings` (page) and `LiveSplit.UI.Components.ColumnSettings` (one per entry of the columns list); both derive from `SettingsBase` (`INotifyPropertyChanged`).
`SplitsComponent.GetSettingsControl(mode)` sets `Settings.Mode`, calls `Settings.PrepareForDisplay()` (this is everything the old `SplitsSettings_Load` / `ColumnSettings_Load` did: clamps the split size to the slider range of the mode, translates the legacy "Time" column name, rebuilds the comparison items of each column, recomputes the move-button states) and returns the settings object. A view only has to bind.
All labels are translation keys (`UiLocalizer.Translate`); the old `UiLocalizer.Apply(Form)` translated every label/checkbox/groupbox/button/combo item text, the view should translate the same strings (combo items: translate for display, bind the untranslated value).
All controls bind two-way and commit on every change (the old bindings used `OnPropertyChanged` update mode).
The page is the longest settings page of LiveSplit: a single scrolling page, no tabs, no tooltips. WinForms layout: ~459px wide fixed, a 4-column table (194 / 29 / 29 / 193 px) in which GroupBoxes span all 4 columns. The page grew by one column editor height (165px) per column; in the new view use a normal scrolling `ItemsControl` for the columns.

## Page layout, row by row (`row: control(s)`)

0. Label "Background Color:" | swatch A (`BackgroundColor`, visible: `IsColor1Visible`) | swatch B (`BackgroundSwatch2Color`, always visible) | combo (spans the rest), items `SplitsSettings.GradientOptions` = `Plain`, `Vertical`, `Horizontal`, `Alternating`, bound to `GradientString` (string; display text == value).
   - Swatch semantics: swatch B edits `BackgroundColor2` normally, but `BackgroundColor` when the gradient is Plain (then swatch A is hidden). The model already does that in `BackgroundSwatch2Color`; bind swatch A to `BackgroundColor`, swatch B to `BackgroundSwatch2Color`.
   - Note: unlike Timer there are no "...With Delta Color" entries. Alternating draws a colour A / colour B row stripe.
1. Label "Total Splits:" | number box -> `VisualSplitCount` (int, min 1, max 1000, step 1, 0 decimals). The number box commits immediately.
2. Label "Upcoming Splits:" | number box -> `SplitPreviewCount` (int, min 0, max `UpcomingSplitsMaximum`, step 1). `UpcomingSplitsMaximum` = `max(0, VisualSplitCount - (AlwaysShowLastSplit ? 2 : 1))`; it raises PropertyChanged when `VisualSplitCount` or `AlwaysShowLastSplit` change. The model lowers `SplitPreviewCount` to the maximum when `VisualSplitCount` (or, interactively, `AlwaysShowLastSplit`) changes, so the view only needs to bind min/max.
3. Checkbox "Show Thin Separators" (col 0) -> `ShowThinSeparators` | checkbox "Show Separator Before Last Split" (col 1, spans) -> `SeparatorLastSplit`.
4. Checkbox "Always Show Last Split" (col 0) -> `AlwaysShowLastSplit` | checkbox "Fill with Blank Space if Not Enough Splits" (col 1) -> `ShowBlankSplits`.
5. Checkbox "Lock Last Split to Bottom" (col 0) -> `LockLastSplit`, enabled: `IsLockLastSplitEnabled` (== `ShowBlankSplits`) | checkbox "Display 2 Rows" (col 1) -> `Display2RowsShown`, enabled: `IsDisplay2RowsEnabled`.
   - In a horizontal layout the old page showed "Display 2 Rows" checked and disabled without modifying the setting. `Display2RowsShown` returns true in `Mode == Horizontal` and ignores writes there; in vertical mode it reads/writes `Display2Rows`. Bind the checkbox to `Display2RowsShown` (not to `Display2Rows`).
6. Label `SizeLabel` ("Split Width:" in horizontal mode, "Split Height:" in vertical mode; translated by the model) | slider (spans), no ticks, integer steps, range `SizeMinimum`..`SizeMaximum` (Horizontal 5..120, Vertical 0..250), bound to `SizeValue` (float; maps to `SplitWidth` in Horizontal and to `ScaledSplitHeight` = `SplitHeight * 10` in Vertical). All four raise PropertyChanged when `Mode` changes.
   - The designer default label ("Horizontal Split Width:") is overwritten by `SizeLabel`, ignore it.
7. Label "Current Split Background Color:" | swatch A (`CurrentSplitTopColor`, visible: `IsTopColorVisible`) | swatch B (`CurrentSplitBottomSwatchColor`) | combo, items `SplitsSettings.SplitGradientOptions` = `Plain`, `Vertical`, `Horizontal`, bound to `SplitGradientString` (string).
   - Swatch B edits `CurrentSplitBottomColor`, but `CurrentSplitTopColor` when the gradient is Plain (swatch A hidden). Same trick as row 0, already in `CurrentSplitBottomSwatchColor`.
8. GroupBox "Split Names":
   - Checkbox "Automatically Abbreviate Names If Necessary" -> `AutomaticAbbreviations`.
   - GroupBox "Color":
     - Checkbox "Override Layout Settings" -> `OverrideTextColor`.
     - Label "Color (Before Current Split):" + swatch -> `BeforeNamesColor`
     - Label "Color (Current Split):" + swatch -> `CurrentNamesColor`
     - Label "Color (After Current Split):" + swatch -> `AfterNamesColor`
     - The three labels and swatches are enabled only when `IsNamesColorEnabled` (== `OverrideTextColor`).
9. GroupBox "Icons":
   - Checkbox "Display Icons" -> `DisplayIcons` | checkbox "Show Icon Shadows" -> `IconShadows`, enabled: `IsIconOptionsEnabled` (== `DisplayIcons`).
   - Label "Size:" | slider -> `IconSize` (float, 10..80, no ticks, integer steps, designer default 16 is irrelevant), both enabled: `IsIconOptionsEnabled`.
10. GroupBox "Deltas":
   - GroupBox "Accuracy":
     - Checkbox "Drop Decimals When More Than 1 Minute" -> `DropDecimals`
     - Radio group (one row): "Seconds" -> `IsDeltaSeconds`, "Tenths" -> `IsDeltaTenths`, "Hundredths" -> `IsDeltaHundredths`, "Milliseconds" -> `IsDeltaMilliseconds`. Each is a bool, true for the current `DeltasAccuracy` (enum `TimeAccuracy`); writing `true` selects it, writing `false` is ignored. All four raise PropertyChanged when `DeltasAccuracy` changes. (Defaults to Tenths. Alternatively bind to the `DeltasAccuracy` enum with a converter.)
   - GroupBox "Live Delta Color":
     - Checkbox "Override Layout Settings" -> `OverrideDeltasColor`
     - Label "Color:" + swatch -> `DeltasColor`, both enabled: `IsDeltaColorEnabled` (== `OverrideDeltasColor`).
11. GroupBox "Split Times":
   - GroupBox "Color":
     - Checkbox "Override Layout Settings" -> `OverrideTimesColor`
     - Label "Color (Before Current Split):" + swatch -> `BeforeTimesColor`
     - Label "Color (Current Split):" + swatch -> `CurrentTimesColor`
     - Label "Color (After Current Split):" + swatch -> `AfterTimesColor`
     - Labels and swatches enabled only when `IsTimesColorEnabled` (== `OverrideTimesColor`).
   - GroupBox "Accuracy": radio group "Seconds" `IsSplitTimesSeconds`, "Tenths" `IsSplitTimesTenths`, "Hundredths" `IsSplitTimesHundredths`, "Milliseconds" `IsSplitTimesMilliseconds` (same semantics as the delta radios, backed by `SplitTimesAccuracy`; default Seconds).
12. GroupBox "Columns" (see next section).

Swatches are colour pickers with alpha (the old `SettingsHelper.ColorButtonClick` dialog; Colors are `System.Drawing.Color` incl. alpha).

## Columns group

Header row: checkbox "Show Column Labels" -> `ShowColumnLabelsShown`, enabled: `IsColumnLabelsEnabled` | label "Labels Color:" + swatch -> `LabelsColor`, both enabled: `IsLabelsColorEnabled` | button "Add Column" (right aligned) -> `SplitsSettings.AddColumn()`.
 - `ShowColumnLabelsShown` is false and the checkbox disabled in horizontal mode (column labels only exist in vertical layouts); writes are ignored there. In vertical mode it reads/writes `ShowColumnLabels`. Bind to `ShowColumnLabelsShown`.
 - "Add Column" appends an empty-named column (`ColumnsList.Add`), the list control must show it at the end and (optionally) scroll to it. Old focus behaviour: after Remove the last column's "Remove Column" button got focus (or the "Show Column Labels" checkbox when the list became empty); after Move Up/Down the moved column's "Remove Column" button got focus. Nice to keep, not required.

Below the header, one editor per item of `SplitsSettings.ColumnsList` (`ObservableCollection<ColumnSettings>`, display order == draw order; the collection raises Add/Remove/Move/Reset notifications, so an `ItemsControl` with an item template is enough). Defaults: two columns, "+/-" (Delta) and "Time" (Split Time), both "Current Comparison" / "Current Timing Method". There can be zero columns.

### Column editor (`ColumnSettings`)

GroupBox with header `GroupTitle` (= "Column: {name}", translated, updates while typing the name). Rows:

1. Label "Name:" | textbox -> `ColumnName` (string, no restrictions, commit on every keystroke). Shown as the column label in the labels row and the legacy name for the default columns.
2. Label "Column Type:" | combo (drop-down list) -> `Type` (string), items `ColumnSettings.ColumnTypeOptions`: `Delta`, `Split Time`, `Delta or Split Time`, `Segment Delta`, `Segment Time`, `Segment Delta or Segment Time`, `Custom Variable`. Changing the type rebuilds `ComparisonOptions` and may reset `Comparison` (see below).
3. Label "Comparison:" | combo (drop-down list) -> `Comparison` (string), items `ComparisonOptions` (an `IReadOnlyList<string>` that is replaced, with PropertyChanged, when the type changes or `Initialize()` runs). First item is always `Current Comparison`, then the comparisons of the run (`state.Run.Comparisons`), minus `None`; for the segment column types also minus `Best Split Times` (and if the column's comparison was `Best Split Times` it is reset to `Current Comparison`). If the stored comparison does not exist in the run it is appended to the items so the selection is not lost. Combo items are comparison names: display as is (not translated).
4. Label "Timing Method:" | combo (drop-down list) -> `TimingMethod`, items `ColumnSettings.TimingMethodOptions`: `Current Timing Method`, `Real Time`, `Game Time`.
5. Buttons (a separate bottom row, right aligned): "Move Up" (`MoveUp()`, enabled: `CanMoveUp`), "Move Down" (`MoveDown()`, enabled: `CanMoveDown`), "Remove Column" (`Remove()`).
   - The commands raise events that `SplitsSettings` handles (it reorders/removes in `ColumnsList`, which in turn refreshes `CanMoveUp/CanMoveDown` of all columns). The view does not touch `ColumnsList` itself. Moving a column at the edge is a no-op (the buttons are disabled there anyway).

"Custom Variable" columns read the run metadata custom variable named like the column (`ColumnName`).

## Settings that are never shown / non-UI properties

* `CurrentState` (the `LiveSplitState`, used to list comparisons), `Mode` (set by `GetSettingsControl`).
* `SplitHeight` / `ScaledSplitHeight` / `SplitWidth` are only reachable through the size slider (row 6).
* `Display2Rows` and `ShowColumnLabels` hold the real values; the "...Shown" properties are the view-facing ones.
* `SplitLayoutChanged` event: raised when `ShowThinSeparators`, `AlwaysShowLastSplit`, `ShowBlankSplits`, `LockLastSplit` or `SeparatorLastSplit` change interactively (not while `SetSettings` loads); the component rebuilds its rows. The view does not use it.
* Settings XML version is 1.6; versions < 1.5 (no `<Columns>`) are converted on load.

## Defaults and compatibility

The constructor keeps the Windows defaults: 8 total / 1 upcoming; all separator, pinned-last, blank-space and lock-last checkboxes on; icons and shadows on, icon size 24; split width 20 and height 3.6; one row, no column labels, no abbreviation and no color overrides. Background is Alternating with transparent color A and ARGB `01FFFFFF` color B. Current-split highlight is Vertical, RGB `3373F4` to `153574`. Name, time, delta and label colors default to white. Delta accuracy is Tenths with dropped decimals after one minute; split-time accuracy is Seconds.

XML remains version 1.6, with ordered `<Columns><Settings>` entries containing Version 1.5, Name, Type, Comparison and TimingMethod. Persist untranslated enum values and comparison names. Empty and unknown comparison names survive loading. Pre-1.5 legacy `ShowSplitTimes` and `Comparison` become either Delta + Split Time columns or one Delta or Split Time column. Pre-1.3 `SplitNamesColor` (since 1.2) / `UseTextColor` migrate to the three name colors and `OverrideTextColor`. Missing legacy values retain the original loader's defaults (not always constructor defaults: split height 6, LockLastSplit false, Plain background). Obsolete `StartingSize`, `StartingTableLayoutSize` and `StartingColumnSettingHeight` were WinForms layout bookkeeping, never persisted, and have no model counterparts.

Render verification uses an eight-segment run at segment index 4, with six visible segments, icons, the pinned final segment and both time columns. PNGs cover Vertical with column labels, Vertical with two rows and labels, and Horizontal with its forced two-row arrangement.
