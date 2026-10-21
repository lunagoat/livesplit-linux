# ScriptableAutoSplit Settings UI Spec

Model: `LiveSplit.UI.Components.ComponentSettings` (`SettingsBase`, `INotifyPropertyChanged`).
Component: `LiveSplit.UI.Components.Component` (`LogicComponent`).

## Overview

ScriptableAutoSplit executes Auto Split Language (`.asl`) scripts compiled at runtime using Roslyn (`Microsoft.CodeAnalysis.CSharp`). It provides an interactive hierarchical tree view for script-defined custom settings and standard options.

## Dialog Layout

Dialog size: 462 x 498, arranged in a table layout with standard padding.
All labels are translation keys (`UiLocalizer.Translate`).

## Controls in Order

| Row / Label | Kind | Binding / Property | Behavior / Description |
|---|---|---|---|
| "Script Path:" | Label + Textbox + Browse Button | `ScriptPath` | Path to the `.asl` script file. Textbox supports drag-and-drop. Browse button opens file dialog with filter `"Auto Split Script (*.asl)\|*.asl\|All Files (*.*)\|*.*"`. Changes to `ScriptPath` re-compile the script and reload settings. |
| "Options:" | Checkbox group + Version label | `BasicSettings`, `GameVersionLabel` | Horizontal flow layout displaying basic ASL actions:<br>• **Start**: checkbox bound to basic setting `"start"`. Enabled only when the script implements a `start` block.<br>• **Split**: checkbox bound to basic setting `"split"`. Enabled only when the script implements a `split` block.<br>• **Reset**: checkbox bound to basic setting `"reset"`. Enabled only when the script implements a `reset` block.<br>• **Game Version**: Right-aligned label displaying `GameVersionLabel` (e.g. `"Game Version: 1.0"` when defined by the script). |
| "Advanced:" / Custom Settings Tree | Tree View | `CustomSettings`, `HasCustomSettings` | Hierarchical tree view displaying script-defined custom settings (`ObservableCollection<ASLSettingNode>`). Visible when `HasCustomSettings` is true.<br>• **Checkbox**: bound two-way to `node.Value`. Toggling automatically updates descendants' `IsEnabled`.<br>• **Label**: `node.Label` (or capitalized setting id).<br>• **Tooltip**: `node.Tooltip` (setting description).<br>• **Enabled State**: `node.IsEnabled` is true only when its parent is both enabled and checked (`parent.IsEnabled && parent.Value`).<br>• **Expansion**: `node.IsExpanded`. |
| Tree Action Buttons | Three Buttons | Actions | Horizontal row below the tree view:<br>• **"Check All"**: sets all custom settings to `true` (`SetAll(true)`).<br>• **"Uncheck All"**: sets all custom settings to `false` (`SetAll(false)`).<br>• **"Reset to Default"**: resets all custom settings to script defaults (`SetAll(null)`). |

## Tree Node Context Menu

Right-clicking any node in the custom settings tree exposes the following actions:
- **Expand Tree** / **Collapse Tree**: recursively expands or collapses all nodes in the entire tree.
- **Collapse Tree to Selection**: collapses all nodes except the active selection path.
- **Expand Branch** / **Collapse Branch**: expands or collapses the clicked node and its subtree.
- **Check Branch** / **Uncheck Branch**: sets `Value` to `true` or `false` for the clicked node and all its descendants (`SetAll(value, node)`).
- **Reset Branch to Default**: resets the clicked node and all its descendants to their default values (`SetAll(null, node)`).
- **Reset Setting to Default**: resets only the clicked individual setting to its default value (`node.Value = node.DefaultValue`).

## XML Persistence

- Settings XML version: `"1.5"`.
- Tags:
  - `<ScriptPath>`: Normalized script file path.
  - `<Start>`, `<Split>`, `<Reset>`: Boolean states for the basic options.
  - `<CustomSettings>`: Collection of `<Setting id="..." type="bool">value</Setting>` elements.

## Linux Process Memory Attach Notice (ptrace_scope)

> [!NOTE]
> ASL scripts access target game process memory via `LiveSplit.ComponentUtil` (using `process_vm_readv` and `/proc/<pid>/mem`).
> Matching Wine/Proton games is handled transparently via `ProcessUtil.GetProcessesByName(name)` in LiveSplit.Core.
> Cross-process memory reading requires Yama ptrace permissions on Linux. If the script cannot attach to the game process, verify that `/proc/sys/kernel/yama/ptrace_scope` is set to `0` or grant `cap_sys_ptrace=eip` to the host executable.
