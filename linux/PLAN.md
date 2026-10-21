# LiveSplit → Linux native: plan

Repo: `src/` (clone of LiveSplit/LiveSplit + submodules), branch `linux-native`.

## What we found
- ~22k LOC Core, ~17k LOC View (WinForms), ~35k LOC in 27 components, 192 files use WinForms, 174 use System.Drawing.
- Only ~13 files P/Invoke Win32 (keyboard hooks, WinAPI, registry/credentials, TimerForm, color picker).
- livesplit-core (Rust) is used only for parsing split files. `livesplit_core.dll` → build `liblivesplit_core.so`.
- WinForms does not exist on modern .NET for Linux; System.Drawing.Common is Windows-only since .NET 7.

## Architecture (keep the C# app, replace the platform layer)
1. **LiveSplit.Drawing** — our own `System.Drawing` (+Drawing2D/Text/Imaging) API subset on **SkiaSharp**.
   Component `DrawHorizontal/DrawVertical(Graphics g, ...)` code compiles unchanged.
2. **Core + components → net10.0** (no WinForms). Win32 bits behind interfaces:
   hotkeys (evdev / X11 / CLI-socket for Wayland), credentials (file/libsecret), file assoc (dropped), gamepad (evdev).
3. **Avalonia UI shell** replaces WinForms View: timer window (renders layout through shim), context menu,
   Run Editor, Layout Editor, Settings, per-component settings, comparisons, share/speedrun.com.
4. **Auto splitting**: ASR via Linux `liblivesplit_core.so`/auto-splitting runtime; ASL later.
5. Formats untouched: `.lss`, `.lsl`, settings.cfg stay compatible with Windows LiveSplit.

## Stages (each ends runnable + committed)
- S0 toolchain: .NET 10 SDK in ~/.dotnet, build liblivesplit_core.so
- S1 Drawing shim + PNG render test
- S2 Core on net10.0, builds, unit tests (test/LiveSplit.Tests) pass
- S3 Components (model + drawing) on net10.0
- S4 Avalonia main window: loads .lsl/.lss, runs timer, renders layout, hotkeys, context menu
- S5 Editors/dialogs, component settings UIs (fan out to subagents per component)
- S6 Auto splitters, packaging (PKGBUILD / AppImage), desktop entry

## Verification
Each stage: `dotnet build`, unit tests, headless render to PNG compared visually, then real window on Hyprland.
