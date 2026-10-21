# win32shim

A Linux x86_64 process-inspection library for unmodified LiveSplit auto-splitter
assemblies hosted by Linux .NET. Map kernel32, psapi, ntdll, user32, gdi32,
dbghelp and winmm (including `.dll` spellings) to this single shared library in
the host's `NativeLibrary.SetDllImportResolver`. This crate does not install a
resolver or load Windows native code.

Exports use `unsafe extern "C"`, the Linux x64 calling convention required by
Linux .NET P/Invoke, with Windows x64 parameter widths and `repr(C)` structure
layouts. This is **not** the Microsoft x64 register calling convention
(`extern "win64"`); native Windows machine code cannot call it directly.
Plain string entry points use A semantics (UTF-8 bytes on Linux); W entry points
use UTF-16. Callers supply valid buffers and correct structure sizes. ANSI code
page emulation is not provided. Errors are stored per thread by `GetLastError`;
this does not implement .NET's separate `Marshal.GetLastWin32Error` cache.

Process handles are positive Linux pids; `OpenProcess` checks readable `/proc`
existence, ignoring access flags. Snapshot handles have bit 63 set and capture
lists in a mutex-protected table. `CloseHandle` removes snapshots and otherwise
always succeeds. Kernel permissions still control memory access. Wine guest
addresses must already correspond to Linux virtual addresses.

| Functions (plain/A/W variants where applicable) | Implementation |
| --- | --- |
| OpenProcess, ReadProcessMemory, WriteProcessMemory | Real: procfs, process_vm_readv/writev; EPERM/ENOSYS fallback to procfs mem. Short copies return 299 and the transferred count. |
| VirtualQueryEx | Real: procfs maps, committed regions and free gaps; per-mapping regions without coalescing. |
| EnumProcesses | Real: numeric procfs directories; bytes actually returned in needed. |
| EnumProcessModules, EnumProcessModulesEx, K32EnumProcessModulesEx | Real: file mappings, PE-only if MZ detected, otherwise ELF; main image first. Filter flags ignored. |
| GetModuleBaseName, K32GetModuleBaseName, GetModuleFileNameEx, GetModuleInformation | Real: captured mappings; PE SizeOfImage; EntryPoint zero. Wine drive_c paths converted to C:\\ paths. |
| CreateToolhelp32Snapshot, Process32First/Next, Module32First/Next, CloseHandle | Real: captured process/module lists; unsupported snapshot types fail with 50. Process details come from procfs. |
| IsWow64Process, IsWow64Process2 | Real: target ELF class; native machine AMD64. |
| GetSystemInfo, GetNativeSystemInfo | Real: page size/CPU count; Windows x64 address bounds and allocation granularity. |
| QueryPerformanceCounter/Frequency | Real: CLOCK_MONOTONIC nanoseconds, 1 GHz frequency. |
| NtSuspendProcess, NtResumeProcess | Real: SIGSTOP/SIGCONT; these affect the entire Linux process. |
| GetLastError | Real: thread-local Windows error codes. |
| WaitForSingleObject, timeBeginPeriod, timeEndPeriod | Stub: return success immediately; no waiting or timer change. |
| AllocConsole, FreeConsole, CreateProcess, CreateRemoteThread, OpenThread, ResumeThread, TerminateThread, GetExitCodeThread | Stub: zero and error 50. |
| VirtualAlloc, VirtualAllocEx, VirtualFreeEx, VirtualProtectEx, FlushInstructionCache, K32QueryWorkingSetEx | Stub: zero and error 50. |
| LoadLibrary, LoadLibraryEx, FreeLibrary, GetProcAddress, GetModuleHandle | Stub: zero and error 50. |
| EnumDisplayMonitors, FindWindow, GetAsyncKeyState, GetCapture, GetForegroundWindow, GetKeyState, GetMenuItemCount, GetMessageExtraInfo, GetMonitorInfo, GetSystemMenu, GetSystemMetrics, GetWindowRect, GetWindowThreadProcessId, MapVirtualKey, MonitorFromPoint, MonitorFromRect, RegisterHotKey, RemoveMenu, SendInput, SendMessage, SetFocus, SetWindowPos, UnregisterHotKey | Stub: zero and error 50; no desktop or input integration. |
| AddFontMemResourceEx, BitBlt, SymCleanup, SymEnumSymbols, SymInitialize, SymLoadModuleEx | Stub: zero and error 50. |

Module lists and maps are observations of a running process and can race unloads
or exit. A file mapped multiple times is split into runs when intervening
mappings belong to other backing objects; anonymous/BSS tails are not added to
ELF image sizes. Memory access uses Linux permissions, not Windows page access
checks (procfs mem can read PROT_NONE pages). Arbitrary code injection, remote
allocation, symbol loading, GUI interaction and native Windows DLL loading are
unsupported. No Wine/Proton installation is required for the tests; a synthetic
file-backed PE mapping exercises Windows image detection.

Build and validate from this directory:

```sh
cargo build --release --offline
cargo test --offline
python3 tests/check_exports.py
```

Output: `target/release/libwin32shim.so`. `exports.txt` lists the complete required
symbol surface, including plain and A/W aliases. Tests cover x64 layouts,
self/child memory, partial copies, maps/free gaps, modules, both snapshot
encodings, bitness, clocks, last-error isolation and synthetic PE detection.
