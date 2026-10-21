# Platform layer (LiveSplit.Core, namespace `LiveSplit.Hosting`)

## Paths (`Paths`)

| | default | portable mode |
|---|---|---|
| config (`settings.cfg`, `share-settings.json`, `credentials.json`) | `$XDG_CONFIG_HOME/LiveSplit` (`~/.config/LiveSplit`) | executable directory |
| data | `$XDG_DATA_HOME/LiveSplit` (`~/.local/share/LiveSplit`) | executable directory |
| cache (auto splitter list, game list) | `$XDG_CACHE_HOME/LiveSplit` (`~/.cache/LiveSplit`) | `<exe dir>/cache` |
| logs | `$XDG_STATE_HOME/LiveSplit` (`~/.local/state/LiveSplit`) | `<exe dir>/logs` |
| command socket | `$XDG_RUNTIME_DIR/livesplit.sock` (falls back to the temp directory) | same |

Portable mode is on when a file named `portable` or `settings.cfg` sits next to the executable. `Paths.SetDirectories` / `Paths.SetPortable` override everything (tests, `--config-dir` style flags).
`PathCompat.Normalize` turns Windows paths from shared files into Unix paths (`C:\x\y` becomes `/x/y`, other drives map through `$WINEPREFIX/dosdevices`, `Z:` is `/`);
`PathCompat.Resolve` additionally finds files case-insensitively. It is applied to recent splits/layouts read from `settings.cfg`; components should apply it to media paths stored in `.lsl` component settings.

## Global hotkeys

`KeyboardBackends.Create()` picks, in order (override with `LIVESPLIT_HOTKEY_BACKEND=evdev|x11|none`):

1. `EvdevKeyboardBackend`: reads `/dev/input/event*` for devices whose `/sys/class/input/eventN/device/capabilities` advertise keys; works on Wayland and X11, never swallows keys, rescans for hotplug every 2 s.
   Needs read access to `/dev/input`: add the user to the `input` group (`sudo usermod -aG input $USER`, then log in again). On `EACCES` the backend reports that message through `StatusMessage` (`CompositeHook.KeyboardStatusMessage`) and the next backend is tried.
   Security note: any process of a user in `input` can read all keystrokes; this is the same trade-off other Wayland timers make.
2. `X11KeyboardBackend` (only when `XDG_SESSION_TYPE=x11` or requested): polls `XQueryKeymap` every 8 ms via libX11; only sees keys while an X client has the pointer/keyboard focus context, so it is a fallback.
3. `NullKeyboardBackend`: hotkeys disabled, status message explains why.

`LinuxKeyMap` (evdev code to `Keys`) and `KeyEventTranslator` (modifier tracking, `ControlKey|modifiers` reporting like the Windows low level hook, auto-repeat suppression) are pure and unit tested.
Numpad keys report as `NumPadN` regardless of NumLock. `AltGr` is `RMenu`.
When a window of the app has focus, the UI layer still receives key events from its toolkit; hotkey and toolkit events can both fire, de-duplicate in the UI layer if needed.

## Auto splitter memory access

Reading another process needs ptrace-style permission. With the default Yama setting (`/proc/sys/kernel/yama/ptrace_scope` = 1) only descendants of LiveSplit can be read.
Either launch the game from LiveSplit, set `ptrace_scope` to 0, or grant the binary `CAP_SYS_PTRACE` (`sudo setcap cap_sys_ptrace=eip <path to host executable>`).
Proton/Wine games show up as Linux processes named after the `.exe`; their addresses are the Windows address space, so existing ASL offsets keep working.

## Credentials

`secret-tool` (package `libsecret`) when present, with a 0600 `credentials.json` fallback when no secret service is running.

## Native library

`liblivesplit_core.so` is built by `src/LiveSplit.Core/build_livesplit_core.sh` (add `--bindings` to regenerate `LiveSplitCore.g.cs`), copied to `linux/native/` (git ignored) and into the output directory by the Core project.

## Known gaps

* Gamepads: `GamepadHook` is a stub (no `/dev/input/js*` or evdev joystick reader yet).
* `HotkeyHook` cannot swallow keys for other programs (no grab); `DeactivateHotkeysForOtherPrograms` is accepted and ignored.
* Process injection primitives (`AllocateMemory`, `VirtualProtect`, `CreateThread`, so `WriteDetour`) are not implemented.
* No self updater, no file association registration (ship a `.desktop` file with `MimeType` entries).
