# Hotkey Indicator settings

Single page, no groups, tabs or tooltips:
1. “Global Hotkeys On Color:” color swatch → `HotkeysOnColor` (default ARGB FF29CC54).
2. “Global Hotkeys Off Color:” color swatch → `HotkeysOffColor` (default FFCC3729).
3. `SizeLabel` (“Width:” in Horizontal mode, otherwise “Height:”), integer slider → `SizeValue`, `SizeMinimum` 1, `SizeMaximum` 50, step 1. Maps to `IndicatorWidth` or `IndicatorHeight` without overwriting the other dimension; each defaults to 3.

All bindings commit immediately. `Mode` is supplied by GetSettingsControl. Legacy unused `BackgroundGradient`/`GradientString` remain public but have no controls and are not serialized. View owns color pickers.
