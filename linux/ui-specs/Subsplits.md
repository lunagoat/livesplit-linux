# Subsplits settings UI contract

Model: `LiveSplit.UI.Components.SplitsSettings` in LiveSplit.Subsplits.dll (same type names as Splits, separate assembly).
A single scrolling settings page; no tabs. All strings are translation keys. Edits commit immediately. Swatches support alpha.
Common controls and the complete column editor follow [Splits](Splits.md), with `AutomaticAbbreviation` singular and no Show Blank Splits option; Lock Last Split is always available.
`ColumnsList` is an ObservableCollection; AddColumn, MoveUp, MoveDown and Remove follow the same ownership and notification approach as Splits. Column editor labels, items, comparison filtering and focus semantics are the same.

Total Splits: VisualSplitCount, integer 1..1000. Upcoming Splits: SplitPreviewCount, integer 0..UpcomingSplitsMaximum. Minimum Number of Sections Before Current Section: MinimumMajorSplits, integer 0..1000, enabled IsMinimumMajorSplitsEnabled. All integer controls step 1, zero decimal places.
Size slider: SizeValue, SizeLabel, SizeMinimum/Maximum (horizontal 5..120 width, vertical 0..250 scaled height), step 1. Icon Size: IconSize, 10..80, step 1, enabled IsIconOptionsEnabled along with shadows and Size label. Indent Blank Icons binds IndentBlankIcons independently.
Background and current split gradients, color overrides, names/times/delta accuracy, two-row and column-label mode dependencies follow Splits.md.

Subsplits helper label: "To set a split as a subsplit, add a - (dash) to the beginning of the split name.\nOptional: Add a section name with {}. Ex: \"{Section Name} Split Name\"".
Visibility radios Always Hide Subsplits / Contextual Subsplits / Always Show Subsplits bind IsHideSubsplits / IsNormalSubsplits / IsShowSubsplits. Writing false is ignored. Contextual is default. Only Current Section -> CurrentSectionOnly, enabled IsCurrentSectionOnlyEnabled. Indent Subsplits -> IndentSubsplits, enabled IsIndentSubsplitsEnabled. Indent Last Split Of Section -> IndentSectionSplit, enabled IsIndentSectionSplitEnabled. Show Icon of Last Split of Section -> ShowIconSectionSplit.
Subsplit Color: Override Layout Settings -> OverrideSubsplitColor; gradient and both swatches enabled IsSubsplitColorEnabled. Gradient combo SubsplitGradientString, Plain/Vertical/Horizontal. Top swatch SubsplitTopColor visible IsSubsplitTopColorVisible; bottom SubsplitBottomSwatchColor redirects to top for Plain.

Section Header: Show Section Header -> ShowHeader. Show Icon -> ShowSectionIcon; Comparison -> HeaderComparison with HeaderComparisonOptions (current + run comparisons excluding None + missing stored name); Timing Method -> HeaderTimingMethod with HeaderTimingMethodOptions (Current Timing Method, Real Time, Game Time). These and Section Header Color / Section Timer groups enabled IsHeaderOptionsEnabled.
Section Header Color: Background Color gradient HeaderGradientString (Plain/Vertical/Horizontal); top HeaderTopColor visible IsHeaderTopColorVisible; bottom HeaderBottomSwatchColor redirects to top for Plain. Show Split Name -> HeaderText; Show Total Section Time -> HeaderTimes. Override Layout Text Colors -> OverrideHeaderColor; Name Color -> HeaderTextColor and Time Color -> HeaderTimesColor enabled IsHeaderTextColorEnabled. Time Accuracy radios Seconds/Tenths/Hundredths/Milliseconds -> IsHeaderAccuracySeconds/Tenths/Hundredths/Milliseconds (or HeaderAccuracy enum), true selects and false ignored.
Section Timer: Show Section Timer -> SectionTimer. Color -> SectionTimerColor, Show Gradient -> SectionTimerGradient, Timer Accuracy radios -> IsSectionTimerAccuracySeconds/Tenths/Hundredths/Milliseconds (or SectionTimerAccuracy enum), enabled IsSectionTimerOptionsEnabled.

Defaults retain Subsplits behavior: split height 6 (constructor writes ScaledSplitHeight=60), no thin separators, 8 total/1 upcoming, 0 minimum previous sections; indentation and icons on; contextual visibility; header, name, total time, section timer and its gradient on; header/timer accuracy Tenths; header gradient Vertical, subsplit gradient Plain; overrides off. Other common defaults match Splits.
XML remains version 1.7, original keys and defaults unchanged; pre-1.7 Comparison/ShowSplitTimes migrate to header comparison and columns; pre-1.3 name color migration preserved. No legacy fonts. CurrentState/Mode are host properties; static HilightSplit/SectionSplit are renderer state. StartingSize/StartingTableLayoutSize/startingColumnSettingHeight were unpersisted WinForms bookkeeping and are removed. No tooltips or delayed commit controls in the original designer. SplitLayoutChanged is suppressed during XML loading.

Original caption and group inventory in designer order (structural layout panels omitted):

| Control | Exact caption | Kind | Bound property |
|---|---|---|---|
| label1 | Total Splits: | label | — |
| label2 | Upcoming Splits: | label | — |
| chkThinSeparators | Show Thin Separators | checkbox | ShowThinSeparators |
| chkLastSplit | Always Show Last Split | checkbox | AlwaysShowLastSplit |
| groupBox3 | Icons | group | — |
| chkIndentBlankIcons | Indent Blank Icons | checkbox | IndentBlankIcons |
| chkDisplayIcons | Display Icons | checkbox | DisplayIcons |
| label5 | Size: | label | — |
| chkIconShadows | Show Icon Shadows | checkbox | IconShadows |
| groupBox4 | Split Times | group | — |
| groupBox5 | Color | group | — |
| label9 | Color (Current Split): | label | — |
| label7 | Color (After Current Split): | label | — |
| chkOverrideTimesColor | Override Layout Settings | checkbox | OverrideTimesColor |
| label6 | Color (Before Current Split): | label | — |
| groupBox1 | Accuracy | group | — |
| rdoMilliseconds | Milliseconds | radio | See mappings below |
| rdoSeconds | Seconds | radio | See mappings below |
| rdoTenths | Tenths | radio | See mappings below |
| rdoHundredths | Hundredths | radio | See mappings below |
| label11 | Background Color: | label | — |
| chkSeparatorLastSplit | Show Separator Before Last Split | checkbox | SeparatorLastSplit |
| groupBox6 | Deltas | group | — |
| groupBox8 | Live Delta Color | group | — |
| chkOverrideDeltaColor | Override Layout Settings | checkbox | OverrideDeltasColor |
| label8 | Color: | label | — |
| groupBox7 | Accuracy | group | — |
| rdoDeltaMilliseconds | Milliseconds | radio | See mappings below |
| rdoDeltaSeconds | Seconds | radio | See mappings below |
| chkDropDecimals | Drop Decimals When More Than 1 Minute | checkbox | DropDecimals |
| rdoDeltaTenths | Tenths | radio | See mappings below |
| rdoDeltaHundredths | Hundredths | radio | See mappings below |
| GroupBox9 | Subsplits | group | — |
| groupBox11 | Section Header | group | — |
| groupBox13 | Section Timer | group | — |
| groupBox14 | Timer Accuracy | group | — |
| rdoSectionTimerAccuracyMilliseconds | Milliseconds | radio | See mappings below |
| rdoSectionTimerAccuracySeconds | Seconds | radio | See mappings below |
| rdoSectionTimerAccuracyTenths | Tenths | radio | See mappings below |
| rdoSectionTimerAccuracyHundredths | Hundredths | radio | See mappings below |
| chkSectionTimer | Show Section Timer | checkbox | SectionTimer |
| label19 | Color | label | — |
| chkSectionTimerGradient | Show Gradient | checkbox | SectionTimerGradient |
| groupBox12 | Section Header Color | group | — |
| groupBox15 | Time Accuracy | group | — |
| rdoHeaderAccuracyMilliseconds | Milliseconds | radio | See mappings below |
| rdoHeaderAccuracySeconds | Seconds | radio | See mappings below |
| rdoHeaderAccuracyTenths | Tenths | radio | See mappings below |
| rdoHeaderAccuracyHundredths | Hundredths | radio | See mappings below |
| label18 | Time Color | label | — |
| chkHeaderTimes | Show Total Section Time | checkbox | HeaderTimes |
| label17 | Name Color | label | — |
| label16 | Background Color: | label | — |
| chkOverrideHeaderColor | Override Layout Text Colors | checkbox | OverrideHeaderColor |
| chkHeaderText | Show Split Name | checkbox | HeaderText |
| lblTimingMethod | Timing Method: | label | — |
| chkShowHeader | Show Section Header | checkbox | ShowHeader |
| lblComparison | Comparison: | label | — |
| chkShowSectionIcon | Show Icon | checkbox | ShowSectionIcon |
| chkShowIconSectionSplit | Show Icon of Last Split of Section | checkbox | ShowIconSectionSplit |
| chkIndentSectionSplit | Indent Last Split Of Section | checkbox | IndentSectionSplit |
| groupBox10 | Subsplit Color | group | — |
| label15 | Subsplit Background Color: | label | — |
| chkOverrideSubsplitColor | Override Layout Settings | checkbox | OverrideSubsplitColor |
| rdoHideSubsplits | Always Hide Subsplits | radio | See mappings below |
| rdoNormalSubsplits | Contextual Subsplits | radio | See mappings below |
| rdoShowSubsplits | Always Show Subsplits | radio | See mappings below |
| chkIndentSubsplits | Indent Subsplits | checkbox | IndentSubsplits |
| chkCurrentSectionOnly | Only Current Section | checkbox | CurrentSectionOnly |
| lblMinimumMajorSplits | Minimum Number of Sections Before Current Section: | label | — |
| lblSplitSize | Horizontal Split Width: | label | — |
| label4 | Current Split Background Color: | label | — |
| chkLockLastSplit | Lock Last Split to Bottom | checkbox | LockLastSplit |
| chkDisplayRows | Display 2 Rows | checkbox | Display2Rows |
| groupColumns | Columns | group | — |
| chkColumnLabels | Show Column Labels | checkbox | ShowColumnLabels |
| lblLabelsColor | Labels Color: | label | — |
| btnAddColumn | Add Column | button | — |
| grpSplitNames | Split Names | group | — |
| grpSplitNamesColor | Color | group | — |
| label13 | Color (After Current Split): | label | — |
| chkOverrideTextColor | Override Layout Settings | checkbox | OverrideTextColor |
| label3 | Color (Before Current Split): | label | — |
| label10 | Color (Current Split): | label | — |
| chkAutomaticAbbreviation | Automatically Abbreviate Names If Necessary | checkbox | AutomaticAbbreviation |
