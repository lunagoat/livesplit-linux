# LiveSplit for Linux

A native Linux port of [LiveSplit](https://livesplit.org/), the premier speedrunning timer.

This port maintains the core C# application logic, timing engine, and `.lss` / `.lsl` file compatibility while replacing the legacy Windows Forms and Win32 platform layer with modern cross-platform equivalents:
- **.NET 10** modern managed runtime.
- **Avalonia UI** for the windowing, menus, layout editor, run editor, and component settings dialogs.
- **SkiaSharp** via an internal `LiveSplit.Drawing` shim reproducing the subset of `System.Drawing` used by LiveSplit components without any Windows dependencies.
- **Rust cdylib interop** for `liblivesplit_core.so` (high-performance split file parsing) and `libasr_capi.so` (native and WebAssembly auto-splitting runtime).
- **Native Linux input & IPC**: evdev global hotkeys on Wayland and X11, Unix domain command socket (`livesplit-ctl`), and native `/proc/<pid>/mem` autosplitter scanning.

---

## Quick Install

```sh
curl -fsSL https://raw.githubusercontent.com/lunagoat/livesplit-linux/linux-native/linux/install.sh | sh
```

Installs a self-contained x86_64 build (bundled .NET runtime, no root) to `~/.local`, adds a desktop entry and `.lss`/`.lsl` file types, and puts `livesplit` and `livesplit-ctl` in `~/.local/bin`. Re-run to update. Remove with:

```sh
curl -fsSL https://raw.githubusercontent.com/lunagoat/livesplit-linux/linux-native/linux/install.sh | sh -s -- --uninstall
```

Your splits and settings in `~/.config/LiveSplit` are never touched.

## Feature & Component Status

### Component Support

| Component | Status | Description |
|---|---|---|
| **Timer** | ✅ Ported | Main timer display (Real Time / Game Time) |
| **Detailed Timer** | ✅ Ported | Two-line timer with segment and comparison timing |
| **Splits** | ✅ Ported | Scrolling list of splits with deltas, comparisons, and icons |
| **Subsplits** | ✅ Ported | Nested split groups with collapsible sections |
| **Title** | ✅ Ported | Game title, category, attempts, and icon display |
| **Text** | ✅ Ported | Custom label and value display |
| **Blank Space** | ✅ Ported | Configurable spacing block |
| **Comparison Time** | ✅ Ported | Current comparison segment duration |
| **Current Comparison** | ✅ Ported | Displays currently active comparison name |
| **Delta** | ✅ Ported | Ahead/behind delta indicator with color coding |
| **Graph** | ✅ Ported | Real-time run delta graph |
| **Hotkey Indicator** | ✅ Ported | Shows if global hotkeys are enabled or disabled |
| **Manual Game Time** | ✅ Ported | Manually pause/resume game time |
| **Possible Time Save** | ✅ Ported | Time save opportunity for current segment |
| **Previous Segment** | ✅ Ported | Time difference on the previous segment |
| **Run Prediction** | ✅ Ported | Predicted finish time based on comparison |
| **Sound** | ✅ Ported | Configurable sound effects on split, PB, reset |
| **Sum of Best** | ✅ Ported | Theoretical best run time |
| **Total Playtime** | ✅ Ported | Cumulative playtime tracking |
| **Video** | ⚠️ Partial | Placeholder rendering / layout preservation; embedded video playback deferred |
| **World Record** | ✅ Ported | speedrun.com WR retrieval |
| **Counter** | ✅ Ported | Increment/decrement counter display |
| **Racetime** | ✅ Ported | racetime.gg race room integration |
| **TheRun** | ✅ Ported | therun.gg real-time split upload & live tracking |
| **Auto Splitting Runtime (ASR)** | ✅ Ported | WebAssembly (.wasm) auto splitters via `libasr_capi.so` |
| **Scriptable Auto Split (ASL)** | ✅ Ported | C# `.asl` scripts via Roslyn engine & Linux process memory |

### Deferred / Platform Differences

* **Video Embedding**: Embedded video playback (DirectShow/Windows Media Foundation) is not available. The Video component loads and renders layout slots without native decoding.
* **Gamepads**: Global gamepad hotkeys (`GamepadHook`) are currently stubbed. Use desktop binds (e.g. `livesplit-ctl` mapped in your compositor) for gamepad split inputs.
* **XGrabKey (Key Swallow)**: Under Wayland and standard evdev, hotkeys do not consume/swallow keypresses destined for other windows. `DeactivateHotkeysForOtherPrograms` is ignored.
* **Process Detours / DLL Injection**: LiveSplit on Linux inspects game state through memory reading (`/proc/<pid>/mem`). Code injection primitives (`CreateThread`, `VirtualProtect`, detours) are not supported on Linux targets.
* **Built-in Auto Updater**: Disabled in favor of Linux package managers (AUR `livesplit`, distro packages).

---

## Building and Running

### Prerequisites

* **.NET 10 SDK** (runtime + SDK 10.0+)
* **Rust and Cargo** (1.80+ with `rustc`)
* **Fontconfig** (`fontconfig` library and development headers)
* **libsecret** (optional, recommended for secure token storage)

### Build Script

The build script compiles native libraries and publishes a framework-dependent release to `linux/dist/livesplit/`:

```bash
# Build native libraries + publish LiveSplit
./linux/build.sh

# Verify the published build with a headless screenshot test
./linux/build.sh --verify

# Incremental build skipping native Rust cdylib steps
./linux/build.sh --skip-native
```

### Running

To launch LiveSplit directly from the build output:

```bash
./linux/dist/livesplit/LiveSplit [options] [splits.lss] [layout.lsl]
```

#### Command-Line Options

```
Usage: livesplit [options] [file.lss] [file.lsl]

  -s, --splits <file>       Open this splits file (.lss)
  -l, --layout <file>       Open this layout file (.lsl)
  --screenshot <out.png>    Render one frame offscreen to PNG and exit
  --screenshot-demo         With --screenshot: start the timer and split first
  --config-dir <dir>        Use <dir> for settings.cfg instead of ~/.config/LiveSplit
  --no-server               Do not start Unix socket / IPC command servers
  -h, --help                Show command line options
```

---

## Arch Linux / PKGBUILD

An Arch Linux `PKGBUILD` is available under `linux/packaging/`:

```bash
cd linux/packaging
makepkg -si
```

The package installs:
* Binary directory: `/usr/lib/livesplit/`
* Launcher wrapper: `/usr/bin/livesplit` (automatically configures `DOTNET_ROOT` if using `~/.dotnet`)
* CLI control utility: `/usr/bin/livesplit-ctl`
* Desktop integration: `/usr/share/applications/livesplit.desktop`
* MIME definitions: `/usr/share/mime/packages/livesplit.xml` (`.lss` and `.lsl` files)
* Application icons: `/usr/share/icons/hicolor/...`

---

## Hyprland & Wayland Setup

### Window Rules (Floating & Pinned)

Add the following rules to your `~/.config/hypr/hyprland.conf` so LiveSplit stays visible as an overlay across all workspaces:

```ini
# LiveSplit floating overlay
windowrulev2 = float, class:^(LiveSplit)$
windowrulev2 = pin, class:^(LiveSplit)$
windowrulev2 = noinitialfocus, class:^(LiveSplit)$
```

### Evdev Global Hotkeys (`input` group)

LiveSplit uses Linux evdev (`/dev/input/event*`) to monitor hotkeys globally across Wayland and X11 games without requiring compositor-specific protocols.

To give your user permission to read input devices without running LiveSplit as root:

```bash
sudo usermod -aG input $USER
```

*Note: You must log out and log back in for group membership to take effect.*

### Memory Access (`ptrace_scope`) for Auto Splitters

LiveSplit reads process memory through `/proc/<pid>/mem`. Under Linux's Yama security module (`/proc/sys/kernel/yama/ptrace_scope`), an unprivileged process can only read memory of its direct child processes.

To allow LiveSplit to read memory of independently launched games (e.g. Steam / Proton):

1. **Temporary enable** (resets on reboot):
   ```bash
   echo 0 | sudo tee /proc/sys/kernel/yama/ptrace_scope
   ```
2. **Permanent enable**:
   ```bash
   echo "kernel.yama.ptrace_scope = 0" | sudo tee /etc/sysctl.d/10-ptrace.conf
   sudo sysctl --system
   ```
3. **Alternative (File Capability)**:
   Grant `CAP_SYS_PTRACE` specifically to the LiveSplit binary:
   ```bash
   sudo setcap cap_sys_ptrace=eip /usr/lib/livesplit/LiveSplit
   ```

*(Proton/Wine games run as native Linux processes and map to Windows virtual address spaces, so standard Windows ASL memory addresses work unchanged).*

### Compositor Keybinds with `livesplit-ctl`

If you prefer Wayland compositor-managed keybinds or want to bind keys when evdev permissions are not available, use the bundled `livesplit-ctl` tool:

```ini
# In ~/.config/hypr/hyprland.conf
bind = , F4, exec, livesplit-ctl startorsplit
bind = , F5, exec, livesplit-ctl reset
bind = , F6, exec, livesplit-ctl skipsplit
bind = , F7, exec, livesplit-ctl unsplit
bind = , F8, exec, livesplit-ctl pause
```

`livesplit-ctl` connects to `$XDG_RUNTIME_DIR/livesplit.sock` and also supports querying timer status:

```bash
livesplit-ctl gettimerphase       # Outputs: NotRunning, Running, Paused, Ended
livesplit-ctl getcurrentrealtime   # Outputs current time (e.g. 01:23.45)
livesplit-ctl getdelta            # Outputs current delta against comparison
```

---

## File Locations & Configuration

LiveSplit conforms to the XDG Base Directory specification:

| Content | Standard Location | Portable Mode |
|---|---|---|
| **Configuration** (`settings.cfg`, credentials) | `$XDG_CONFIG_HOME/LiveSplit` (`~/.config/LiveSplit`) | `<exe dir>/` |
| **Data** | `$XDG_DATA_HOME/LiveSplit` (`~/.local/share/LiveSplit`) | `<exe dir>/` |
| **Cache** (game lists, splitter catalog) | `$XDG_CACHE_HOME/LiveSplit` (`~/.cache/LiveSplit`) | `<exe dir>/cache/` |
| **Logs** | `$XDG_STATE_HOME/LiveSplit` (`~/.local/state/LiveSplit`) | `<exe dir>/logs/` |
| **Command Socket** | `$XDG_RUNTIME_DIR/livesplit.sock` | same |

### Portable Mode

To enable portable mode, create an empty file named `portable` or place a `settings.cfg` file directly in the directory containing the `LiveSplit` executable.

---

## Compatibility with Windows `.lss` and `.lsl`

* **100% Format Compatibility**: XML schemas, element ordering, and time serialization are byte-for-byte compatible with upstream Windows LiveSplit. Any `.lss` splits file or `.lsl` layout file created on Windows can be opened on Linux and vice versa.
* **Path Translation**: Windows file paths (e.g. `C:\Games\layout.lsl` or Wine drives `Z:\home\...`) stored in layout settings are automatically translated and resolved case-insensitively using `PathCompat.Normalize` and `PathCompat.Resolve`.
* **Font Fallback**: Windows standard fonts (`Segoe UI`, `Consolas`, `Arial`) map cleanly to system fontconfig aliases.

---

## Running Tests

All unit, component, drawing, and headless UI tests run via `dotnet test`:

```bash
# Run all test suites
dotnet test linux/LiveSplit.Linux.slnx

# Run specific project test suites:
dotnet test test/LiveSplit.Tests/LiveSplit.Tests.csproj                    # Core engine tests
dotnet test linux/LiveSplit.Drawing.Tests/LiveSplit.Drawing.Tests.csproj  # Skia drawing shim tests
dotnet test test/LiveSplit.Components.Tests/LiveSplit.Components.Tests.csproj # Component tests
dotnet test linux/LiveSplit.App.Tests/LiveSplit.App.Tests.csproj          # Avalonia headless UI tests
```
