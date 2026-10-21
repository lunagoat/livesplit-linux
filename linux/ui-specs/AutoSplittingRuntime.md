# AutoSplittingRuntime Settings UI Spec

Model: `LiveSplit.AutoSplittingRuntime.ComponentSettings` (`SettingsBase`, `INotifyPropertyChanged`).
Component: `LiveSplit.AutoSplittingRuntime.ASRComponent` (`LogicComponent`).

## Overview

The Auto Splitting Runtime executes WebAssembly (`.wasm`) auto splitters compiled against the livesplit-auto-splitting runtime specification. It loads native `libasr_capi.so` and manages dynamic WASM settings trees.

## Dialog Layout

Dialog size: 476 x 512, 7px padding. Arranged in a vertical grid/table layout.
All labels are translation keys (`UiLocalizer.Translate`).

## Controls in Order

| Group / Label | Kind | Binding / Property | Behavior / Description |
|---|---|---|---|
| "Script Path:" | Label + Textbox + Browse Button | `ScriptPath`, `IsScriptPathEnabled` | File path to `.wasm` script. Textbox supports drag-and-drop. Browse button opens file dialog with filter `"Auto Splitting Runtime (*.wasm)\|*.wasm\|All Files (*.*)\|*.*"`. Disabled when script path is fixed by an auto-splitter preset. Updating `ScriptPath` reloads the runtime and rebuilds the settings tree. |
| Error Banner | Label / TextBlock | `Error` | Visible when `Error != null`. Displays runtime compilation or initialization errors. |
| GroupBox "Auto Splitter Settings" | Dynamic widget container | `Items` (`ObservableCollection<ASRWidget>`) | Renders the dynamic WASM settings tree produced by `runtime.GetSettingsWidgets()`. |

### Dynamic WASM Widgets (`ASRWidget`)

Each item in `Items` represents a WASM setting widget described by `Type`:

| Widget Type | Kind | Binding / Properties | Description |
|---|---|---|---|
| `"title"` | Heading / Section Header | `Description`, `HeadingLevel` | Visual section separator with indentation/size determined by `HeadingLevel` (h0, h1, ...). |
| `"bool"` | Checkbox | `Description`, `BooleanValue`, `Tooltip` | Checkbox toggling a boolean setting. Setting value updates `runtime.SettingsMapSetBool(Key, value)`. |
| `"choice"` | ComboBox / Dropdown | `Description`, `Choices`, `StringValue`, `Tooltip` | Single-choice selection. Populated with `Choices` (`Key`, `Description`). Setting value updates `runtime.SettingsMapSetString(Key, selectedKey)`. |
| `"file-select"` | Textbox + Browse Button | `Description`, `StringValue`, `Filter`, `Tooltip` | Path to an external file. Automatically maps between host path and WASI virtual filesystem path via `path_to_wasi` / `wasi_to_path`. |

## Context Menu Actions

Available when right-clicking on settings items in the tree view:
- Expand Tree / Collapse Tree
- Expand Branch / Collapse Branch
- Reset Setting to Default
- Reset Branch to Default

## Linux Process Memory Attach Notice (ptrace_scope)

> [!IMPORTANT]
> **Linux Process Memory Access and `ptrace_scope`**:
> On Linux, the auto splitting runtime accesses target process memory via `process_vm_readv` (and `/proc/<pid>/mem`).
> By default, the Linux Yama LSM module restricts cross-process memory reading (`/proc/sys/kernel/yama/ptrace_scope` = 1), allowing processes to attach only to their direct child/descendant processes.
>
> If the auto splitter fails to attach or read game state:
> 1. Launch the game as a child process of LiveSplit, OR
> 2. Grant `CAP_SYS_PTRACE` capabilities to the LiveSplit host binary:
>    ```bash
>    sudo setcap cap_sys_ptrace=eip <path-to-livesplit-executable>
>    ```
> 3. Or configure Yama ptrace scope to allow attaching to other processes owned by the same user:
>    ```bash
>    echo 0 | sudo tee /proc/sys/kernel/yama/ptrace_scope
>    ```
> Wine and Proton games are resolved through LiveSplit.Core's `ProcessUtil.GetProcessesByName`, matching PE module names and `/proc/<pid>/comm`.

## Native Library Resolution

The native library `libasr_capi.so` is loaded by `ASRLoader` using `NativeLibrary` probing in the following order:
1. Environment variable `$LIVESPLIT_ASR_PATH` (file or directory)
2. `AppContext.BaseDirectory/libasr_capi.so`
3. `AppContext.BaseDirectory/native/libasr_capi.so`
4. Development/workspace native path: `linux/native/libasr_capi.so`
