# LiveSplit.Core: public API changes for the Linux port

Everything not listed here keeps its Windows signature. Persisted formats (`.lss`, `.lsl`, `settings.cfg`) are unchanged.
Core targets `net10.0`, references `linux/LiveSplit.Drawing` for `System.Drawing`, and references no UI toolkit.

## Types now provided by Core (`Compat/`, namespace `System.Windows.Forms`)

`Keys` (same names and numeric values as WinForms, `[Flags]` modifiers), `KeyEventArgs`, `KeyEventHandler`,
`DialogResult`, `MessageBoxButtons`, `MessageBoxIcon`. Nothing else from WinForms exists. `MessageBox.Show(...)` does not exist:
use `PlatformServices.Current.ShowMessage(owner, text, caption, buttons, icon)` (`LiveSplit.Hosting`).

Namespace gotcha: `LiveSplit.Hosting` shadows a type named `Platform` inside any `LiveSplit.*` namespace.
Write `SpeedrunComSharp.Platform` (or `using Platform = SpeedrunComSharp.Platform;`) where components use the speedrun.com type.

## UI toolkit decoupling

| Old | New |
|---|---|
| `IComponent.GetSettingsControl(LayoutMode)` returns `Control` | returns `object` (toolkit specific control, or the settings object; the UI layer maps it to a view). `LogicComponent`, `ControlComponent` and all core components follow |
| `RaceProviderSettings.GetSettingsControl()` returns `Control` | returns `object` (default `null`); new `virtual string SettingsMessage` for the placeholder text |
| `LiveSplitState.Form` / ctor parameter: `System.Windows.Forms.Form` | `LiveSplit.UI.IFormHost` (new interface: `InvokeRequired`, `IsActive`, `Invoke`, `BeginInvoke`, `Invalidate(Rectangle)`, `AddControl`, `RemoveControl`, `SetControlBounds`, `GetControlVisible`, `SetControlVisible`). `Form.ActiveForm == state.Form` becomes `state.Form.IsActive` |
| `Invalidator(Form)`, `Invalidator.Form` | `Invalidator(IFormHost)`, `IFormHost Form` |
| `ControlComponent(LiveSplitState, Control, Action<Exception>)`, protected `Form Form`, `Control Control` | `ControlComponent(LiveSplitState, object control, ...)`, protected `IFormHost Form`, `object Control`; positioning goes through `IFormHost.SetControlBounds` |
| `InputBox.Show(IWin32Window owner, ...)`, `LoginBox.Show(IWin32Window owner, ...)` | same static API with `object owner`; delegates to `PlatformServices.Current.Prompt` / `Login` |
| `ScrollableMessageBox`, `TwitchGameResolveDialog` (+ designers, resx) | deleted; `IPlatformServices.ShowScrollableMessage` / `ChooseFromSearch` |
| `SettingsHelper.GetFontDialog`, `SettingsHelper.ColorButtonClick(Button, Control)` | deleted (UI layer owns font and colour pickers) |
| `UiLocalizer.Apply(Form)`, `ApplyOpenForms`, `ApplyCurrentCultureToOpenForms` | deleted; `Translate`, `TranslateKey` unchanged |
| `UpdateHelper.Update(Form, Action, IUpdateable[])` | deleted (no self updater). `Version`, `UserAgent`, `AlreadyChecked` stay. `UpdateManager.IUpdateable` is now defined in Core (`Updates/IUpdateable.cs`) so `IComponentFactory : IUpdateable` and component factories compile unchanged |
| `FiletypeRegistryHelper` | deleted; `LiveSplit.Hosting.IFileAssociation` / `FileAssociation.Current` (no-op, a `.desktop` file is shipped instead) |
| `ShareSettings : ApplicationSettingsBase` | plain class with the same five string properties, `Default`, `Reload()`, `Save()`; stored as JSON in `Paths.ConfigDirectory/share-settings.json` (call `Save()` instead of relying on auto save) |

Fonts and images in `.lsl`/`.lss` are `BinaryFormatter` blobs written by .NET Framework. `SettingsHelper.GetFontFromElement`,
`GetImageFromElement` and both `CreateSetting` overloads now go through `LiveSplit.UI.DrawingSerialization`, which reads them with
`System.Formats.Nrbf` and writes byte-identical `System.Drawing.Font` blobs (and `System.Drawing.Bitmap` blobs in the layout livesplit-core expects).

## Dialogs and services the UI layer must provide (`LiveSplit.Hosting.IPlatformServices`, assign `PlatformServices.Current`)

`ShowMessage`, `ShowScrollableMessage`, `Prompt` (one and two fields), `Login` (username, password, remember),
`ChooseFromSearch` (Twitch game picker: search callback, returns selected index, `-1` with `OK` means "no game"),
`ChooseSaveFile`, `ChooseOpenFile` (filter syntax `Name (*.x)|*.x|...`), `SetClipboardImage`, `SetClipboardText`, `OpenUrl`
(default implementation runs `xdg-open`). The default `ConsolePlatformServices` logs messages and cancels every prompt.
The UI layer also implements `IFormHost`, the colour/font pickers and per-component settings views, and the update/"What's new" flow.

## Input

| Old | New |
|---|---|
| `KeyboardHook` polled `GetAsyncKeyState`; `protected Dictionary<Keys,bool> RegisteredKeys`; `static GetAsyncKeyState` | event driven over `IKeyboardBackend`; `protected HashSet<Keys> RegisteredKeys`; new ctor `KeyboardHook(IKeyboardBackend)`, `IsAvailable`, `StatusMessage`, `IDisposable`; `RegisterHotKey`, `UnregisterAllHotkeys`, `Poll`, `KeyPressed` unchanged |
| `LowLevelKeyboardHook`, `KeyboardInput`, `WindowsHookHelper` | deleted |
| `CompositeHook`: `protected LowLevelKeyboardHook KeyboardHook`, `Joystick GetMouse()` | `protected KeyboardHook KeyboardHook`, `object GetMouse()` (always `null`); new `IsKeyboardAvailable`, `KeyboardStatusMessage`, `IDisposable` |
| `HotkeyHook.RegisterHotKey(System.Windows.Input.ModifierKeys, Keys)` | `LiveSplit.Model.Input.ModifierKeys` (same values: Alt 1, Control 2, Shift 4, Windows 8). Registered through the same backend; Linux cannot swallow the key for other programs, so `DeactivateHotkeysForOtherPrograms` has no effect |
| `GamepadHook` (SharpDX DirectInput) | stub that reports no gamepads; `GamepadButton`, `KeyOrButton` unchanged |

## Process memory (`LiveSplit.ComponentUtil`)

Signatures of `ReadValue<T>`, `ReadBytes`, `ReadPointer`, `ReadString`, `WriteValue`, `WriteBytes`, `MemoryPages`, `Is64Bit`,
`ModulesWow64Safe`, `MainModuleWow64Safe`, `SignatureScanner`, `DeepPointer`, `MemoryWatcher` are unchanged. Implementation: `process_vm_readv` with a
`/proc/<pid>/mem` fallback, writes through `/proc/<pid>/mem`, modules and pages from `/proc/<pid>/maps`. Wine/Proton processes expose their PE modules with
PE `SizeOfImage` and entry point; the main module is the `.exe` matching the process name.

* `WinAPI` P/Invokes are gone (the `MemoryBasicInformation`, `MemPageState/Type/Protect` types stay).
* `AllocateMemory`, `FreeMemory`, `VirtualProtect`, `CreateThread` return `IntPtr.Zero`/`false` (so `WriteDetour` throws `Win32Exception`). `Suspend`/`Resume` use `SIGSTOP`/`SIGCONT`.
* `Process.GetProcessesByName("game")` does not match `game.exe` under Wine. Use the new `ProcessUtil.GetProcessesByName(name)` (matches `/proc/<pid>/comm` and the basename of argv[0], with or without `.exe`).
* `ProcessMemory` (new, public) exposes `ReadMaps`, `TryRead`, `TryWrite`, `TryReadPeInfo` for components that need more.

## Server

`CommandServer`: the Windows named pipe `LiveSplit` is replaced by a Unix socket at `$XDG_RUNTIME_DIR/livesplit.sock` (mode 0600). `StartNamedPipe()` keeps its name
(`StartUnixSocket()` is an alias), `WaitingServerPipe` became `Socket UnixServer`, `Form` is `IFormHost`. TCP (port from settings, default 16834) and WebSocket (`/livesplit`)
and the command protocol are unchanged.

## Misc

* `CredentialManager`: Windows Credential Manager replaced by `ICredentialStore` (`CredentialManager.Store`): `secret-tool` (libsecret) when installed, otherwise a 0600 JSON file `credentials.json` in `Paths.ConfigDirectory`. `Credential`, `CredentialType`, `WebCredentials` unchanged.
* `ComponentManager.PATH_COMPONENTS` is `"Components/"`, `BasePath` defaults to the executable directory, a missing directory yields no components, assemblies load through `AssemblyLoadContext.Default`.
* `Log` writes warnings/errors to `Paths.LogDirectory/LiveSplit.log` (1 MB rotation) instead of the Windows event log.
* `AutoSplitterFactory` caches `LiveSplit.AutoSplitters.xml` in `Paths.CacheDirectory` (`AutoSplittersXmlPath`); `CompositeGameList` caches in the same directory.
* `LiveSplitCoreFactory.LoadLiveSplitCore()` resolves `liblivesplit_core.so` from `$LIVESPLIT_CORE_PATH`, the base directory, `native/`, `runtimes/<rid>/native`, then default probing.
* `RunMetadata.Platform` is typed `SpeedrunComSharp.Platform` explicitly (see namespace gotcha above).
* `SimpleLabel` measures monospaced characters with `Graphics.MeasureString` instead of `TextRenderer.MeasureText`.
* Persisted `TimeSpan` values (`Time`, `Attempt`) and recent file paths in `settings.cfg` are read culture-invariantly / run through `PathCompat.Normalize`.
* Excel export (`Codaxy.Xlio`) works on net10.0 and stays enabled.
