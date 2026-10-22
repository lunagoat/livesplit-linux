//! Win32-shaped process inspection APIs for Linux .NET P/Invoke.
//! Callers must supply valid, writable buffers of the documented Windows sizes.
#![allow(non_snake_case)]
#![allow(clippy::missing_safety_doc)]
#[cfg(not(all(target_os = "linux", target_arch = "x86_64")))]
compile_error!("win32shim requires Linux x86_64");
use std::{
    cell::Cell,
    collections::{BTreeMap, HashMap},
    ffi::c_void,
    fs, io,
    os::unix::fs::FileExt,
    ptr,
    sync::{Mutex, OnceLock},
};
pub type HANDLE = usize;
thread_local! { static ERROR: Cell<u32> = const { Cell::new(0) }; }
fn error(e: u32) {
    ERROR.with(|v| v.set(e));
}
/// Opt-in diagnostics: set LIVESPLIT_SHIM_TRACE=1 to log API calls to stderr. Identical consecutive
/// messages are collapsed into a repeat count so a polling loop cannot flood the log.
fn trace(msg: impl FnOnce() -> String) {
    static ON: OnceLock<bool> = OnceLock::new();
    static STATE: Mutex<(String, usize)> = Mutex::new((String::new(), 0));
    static START: OnceLock<std::time::Instant> = OnceLock::new();
    if !*ON.get_or_init(|| std::env::var_os("LIVESPLIT_SHIM_TRACE").is_some()) {
        return;
    }
    let start = START.get_or_init(std::time::Instant::now);
    let text = msg();
    let mut st = STATE.lock().unwrap_or_else(|e| e.into_inner());
    if st.0 == text {
        st.1 += 1;
        return;
    }
    if st.1 > 0 {
        eprintln!("[win32shim]   (previous message repeated {} more times)", st.1);
    }
    eprintln!("[win32shim] +{}ms {}", start.elapsed().as_millis(), text);
    *st = (text, 0);
}
fn io_error(e: &io::Error) {
    error(match e.raw_os_error() {
        Some(libc::EPERM | libc::EACCES) => 5,
        Some(libc::ENOENT | libc::ESRCH) => 6,
        _ => 87,
    });
}
fn pid(h: usize) -> Option<i32> {
    if h > 0 && h <= i32::MAX as usize {
        Some(h as i32)
    } else {
        error(6);
        None
    }
}
#[no_mangle]
pub unsafe extern "C" fn GetLastError() -> u32 {
    ERROR.with(Cell::get)
}
#[no_mangle]
pub unsafe extern "C" fn OpenProcess(_: u32, _: i32, p: u32) -> usize {
    if p == 0 || p > i32::MAX as u32 {
        error(87);
        return 0;
    }
    match fs::read_dir(format!("/proc/{p}")) {
        Ok(_) => {
            trace(|| format!("OpenProcess({p}) ok"));
            p as usize
        }
        Err(e) => {
            trace(|| format!("OpenProcess({p}) failed: {e}"));
            io_error(&e);
            0
        }
    }
}
unsafe fn transfer(
    h: usize,
    addr: usize,
    buf: *mut c_void,
    size: usize,
    count: *mut usize,
    write: bool,
) -> i32 {
    if !count.is_null() {
        *count = 0;
    }
    let Some(p) = pid(h) else { return 0 };
    if size == 0 {
        return 1;
    }
    if buf.is_null() {
        error(87);
        return 0;
    }
    let local = libc::iovec {
        iov_base: buf,
        iov_len: size,
    };
    let remote = libc::iovec {
        iov_base: addr as *mut c_void,
        iov_len: size,
    };
    let n = if write {
        libc::process_vm_writev(p, &local, 1, &remote, 1, 0)
    } else {
        libc::process_vm_readv(p, &local, 1, &remote, 1, 0)
    };
    {
        static CALLS: std::sync::atomic::AtomicUsize = std::sync::atomic::AtomicUsize::new(0);
        let c = CALLS.fetch_add(1, std::sync::atomic::Ordering::Relaxed) + 1;
        if c.is_power_of_two() {
            trace(|| format!("memory transfer #{c}: pid {p} {addr:#x} size {size} -> {n}"));
        }
    }
    let result = if n >= 0 {
        Ok(n as usize)
    } else {
        let e = io::Error::last_os_error();
        if matches!(e.raw_os_error(), Some(libc::EPERM | libc::ENOSYS)) {
            fs::OpenOptions::new()
                .read(true)
                .write(write)
                .open(format!("/proc/{p}/mem"))
                .and_then(|f| {
                    if write {
                        f.write_at(std::slice::from_raw_parts(buf.cast(), size), addr as u64)
                    } else {
                        f.read_at(
                            std::slice::from_raw_parts_mut(buf.cast(), size),
                            addr as u64,
                        )
                    }
                })
        } else {
            Err(e)
        }
    };
    match result {
        Ok(n) => {
            if !count.is_null() {
                *count = n;
            }
            if n == size {
                1
            } else {
                error(299);
                0
            }
        }
        Err(e) => {
            if !matches!(e.raw_os_error(), Some(libc::EFAULT | libc::EIO)) {
                trace(|| format!("{}(pid {p}, {addr:#x}, {size}) failed: {e}", if write { "WriteProcessMemory" } else { "ReadProcessMemory" }));
            }
            if matches!(e.raw_os_error(), Some(libc::EFAULT | libc::EIO)) {
                error(299);
            } else {
                io_error(&e);
            }
            0
        }
    }
}
#[no_mangle]
pub unsafe extern "C" fn ReadProcessMemory(
    h: usize,
    a: *const c_void,
    b: *mut c_void,
    n: usize,
    r: *mut usize,
) -> i32 {
    transfer(h, a as usize, b, n, r, false)
}
#[no_mangle]
pub unsafe extern "C" fn WriteProcessMemory(
    h: usize,
    a: *mut c_void,
    b: *const c_void,
    n: usize,
    r: *mut usize,
) -> i32 {
    transfer(h, a as usize, b as *mut c_void, n, r, true)
}
#[derive(Clone, Debug)]
struct Mapping {
    start: usize,
    end: usize,
    protect: u32,
    shared: bool,
    path: String,
}
fn maps(h: usize) -> Option<Vec<Mapping>> {
    let p = pid(h)?;
    let text = match fs::read_to_string(format!("/proc/{p}/maps")) {
        Ok(t) => t,
        Err(e) => {
            trace(|| format!("cannot read /proc/{p}/maps: {e}"));
            io_error(&e);
            return None;
        }
    };
    Some(
        text.lines()
            .filter_map(|line| {
                // Split only the five fixed fields; paths may contain spaces.
                let mut rest = line;
                let mut fields = Vec::new();
                for _ in 0..5 {
                    rest = rest.trim_start();
                    let i = rest.find(char::is_whitespace).unwrap_or(rest.len());
                    fields.push(&rest[..i]);
                    rest = &rest[i..];
                }
                let (a, b) = fields[0].split_once('-')?;
                let perm = fields[1].as_bytes();
                if perm.len() < 4 {
                    return None;
                }
                let (r, w, x) = (perm[0] == b'r', perm[1] == b'w', perm[2] == b'x');
                let protect = match (r, w, x) {
                    (_, true, true) => 0x40,
                    (true, false, true) => 0x20,
                    (false, false, true) => 0x10,
                    (_, true, false) => 4,
                    (true, false, false) => 2,
                    _ => 1,
                };
                Some(Mapping {
                    start: usize::from_str_radix(a, 16).ok()?,
                    end: usize::from_str_radix(b, 16).ok()?,
                    protect,
                    shared: perm[3] == b's',
                    path: rest.trim_start().trim_end_matches(" (deleted)").to_owned(),
                })
            })
            .collect(),
    )
}
#[repr(C)]
#[derive(Debug, Default)]
pub struct MEMORY_BASIC_INFORMATION {
    pub BaseAddress: usize,
    pub AllocationBase: usize,
    pub AllocationProtect: u32,
    pub pad: u32,
    pub RegionSize: usize,
    pub State: u32,
    pub Protect: u32,
    pub Type: u32,
    pub pad2: u32,
}
#[no_mangle]
pub unsafe extern "C" fn VirtualQueryEx(
    h: usize,
    a: *const c_void,
    out: *mut MEMORY_BASIC_INFORMATION,
    len: usize,
) -> usize {
    if out.is_null() || len < size_of::<MEMORY_BASIC_INFORMATION>() {
        error(87);
        return 0;
    }
    let Some(ms) = maps(h) else {
        return 0;
    };
    let addr = a as usize;
    if addr == 0 {
        trace(|| format!("VirtualQueryEx scan of {h} starts, {} mappings", ms.len()));
    }
    {
        static CALLS: std::sync::atomic::AtomicUsize = std::sync::atomic::AtomicUsize::new(0);
        let c = CALLS.fetch_add(1, std::sync::atomic::Ordering::Relaxed) + 1;
        if c.is_power_of_two() {
            trace(|| format!("VirtualQueryEx call #{c} at {addr:#x} ({} mappings)", ms.len()));
        }
    }
    let mut result = MEMORY_BASIC_INFORMATION::default();
    if let Some(m) = ms.iter().find(|m| addr >= m.start && addr < m.end) {
        result.BaseAddress = m.start;
        result.AllocationBase = m.start;
        result.AllocationProtect = m.protect;
        result.RegionSize = m.end - m.start;
        result.State = 0x1000;
        result.Protect = m.protect;
        result.Type = if m.path.starts_with('/') {
            if m.shared {
                0x40000
            } else {
                0x1000000
            }
        } else {
            0x20000
        };
    } else {
        const LIMIT: usize = 0x7fff_ffff_0000;
        if addr >= LIMIT {
            error(87);
            return 0;
        }
        let start = ms
            .iter()
            .filter(|m| m.end <= addr)
            .map(|m| m.end)
            .max()
            .unwrap_or(0);
        let end = ms
            .iter()
            .find(|m| m.start > addr)
            .map(|m| m.start)
            .unwrap_or(LIMIT)
            .min(LIMIT);
        result.BaseAddress = start;
        result.RegionSize = end - start;
        result.State = 0x10000;
        result.Protect = 1;
    }
    ptr::write(out, result);
    size_of::<MEMORY_BASIC_INFORMATION>()
}
#[derive(Clone)]
struct Module {
    base: usize,
    size: u32,
    path: String,
    pe: bool,
}
fn remote_bytes(h: usize, a: usize, n: usize) -> Option<Vec<u8>> {
    let mut b = vec![0; n];
    let old = ERROR.with(Cell::get);
    let ok = unsafe { ReadProcessMemory(h, a as _, b.as_mut_ptr().cast(), n, ptr::null_mut()) };
    error(old);
    if ok != 0 {
        Some(b)
    } else {
        None
    }
}
fn pe_size(h: usize, base: usize) -> Option<u32> {
    let dos = remote_bytes(h, base, 64)?;
    if &dos[..2] != b"MZ" {
        return None;
    }
    let off = u32::from_le_bytes(dos[60..64].try_into().ok()?) as usize;
    if off > 16 * 1024 * 1024 {
        return Some(0);
    }
    let Some(a) = base.checked_add(off) else {
        return Some(0);
    };
    let Some(hdr) = remote_bytes(h, a, 84) else {
        return Some(0);
    };
    if &hdr[..4] != b"PE\0\0" {
        return Some(0);
    }
    Some(u32::from_le_bytes(hdr[80..84].try_into().ok()?))
}
fn modules(h: usize) -> Option<Vec<Module>> {
    let ms = maps(h)?;
    let mut by_path: BTreeMap<String, Vec<&Mapping>> = BTreeMap::new();
    for m in &ms {
        if m.path.starts_with('/') {
            by_path.entry(m.path.clone()).or_default().push(m);
        }
    }
    let mut all = Vec::new();
    for (path, entries) in by_path {
        // Separate repeated mappings of the same file when another mapping occupies the gap.
        let mut base = entries[0].start;
        let mut end = entries[0].end;
        let mut runs = Vec::new();
        for m in entries.iter().skip(1) {
            if ms
                .iter()
                .any(|x| x.start >= end && x.end <= m.start && x.path != path)
            {
                runs.push((base, end));
                base = m.start;
            }
            end = m.end;
        }
        runs.push((base, end));
        for (base, end) in runs {
            let pe = pe_size(h, base);
            all.push(Module {
                base,
                size: pe
                    .filter(|v| *v != 0)
                    .unwrap_or((end - base).min(u32::MAX as usize) as u32),
                path: path.clone(),
                pe: pe.is_some(),
            });
        }
    }
    let wine = all.iter().any(|m| m.pe);
    all.retain(|m| {
        if wine {
            m.pe
        } else {
            remote_bytes(h, m.base, 4).is_some_and(|b| b == b"\x7fELF")
        }
    });
    let exe = fs::read_link(format!("/proc/{h}/exe"))
        .ok()
        .map(|p| p.to_string_lossy().into_owned());
    all.sort_by_key(|m| {
        (
            !if wine {
                m.path.to_ascii_lowercase().ends_with(".exe")
            } else {
                exe.as_ref() == Some(&m.path)
            },
            m.base,
        )
    });
    Some(all)
}
fn basename(s: &str) -> &str {
    s.rsplit(['/', '\\']).next().unwrap_or(s)
}
pub fn windows_path(s: &str) -> String {
    if let Some((_, tail)) = s.split_once("/drive_c/") {
        format!("C:\\{}", tail.replace('/', "\\"))
    } else {
        s.to_owned()
    }
}
unsafe fn string_out(s: &str, out: *mut c_void, cap: u32, wide: bool) -> u32 {
    if out.is_null() || cap == 0 {
        error(87);
        return 0;
    }
    if wide {
        let v: Vec<u16> = s.encode_utf16().collect();
        let n = v.len().min(cap as usize - 1);
        ptr::copy_nonoverlapping(v.as_ptr(), out.cast(), n);
        *out.cast::<u16>().add(n) = 0;
        n as u32
    } else {
        let v = s.as_bytes();
        let n = v.len().min(cap as usize - 1);
        ptr::copy_nonoverlapping(v.as_ptr(), out.cast(), n);
        *out.cast::<u8>().add(n) = 0;
        n as u32
    }
}
unsafe fn module_name(
    h: usize,
    base: usize,
    out: *mut c_void,
    cap: u32,
    wide: bool,
    full: bool,
) -> u32 {
    let Some(ms) = modules(h) else { return 0 };
    let Some(m) = ms.iter().find(|m| base == 0 || m.base == base) else {
        error(6);
        return 0;
    };
    string_out(
        &if full {
            windows_path(&m.path)
        } else {
            basename(&m.path).to_owned()
        },
        out,
        cap,
        wide,
    )
}
macro_rules! names { ($($name:ident,$wide:expr,$full:expr);* $(;)?) => {$ (
    #[no_mangle] pub unsafe extern "C" fn $name(h:usize,m:usize,b:*mut c_void,n:u32)->u32{module_name(h,m,b,n,$wide,$full)}
)*}; }
names! {GetModuleBaseName,false,false; GetModuleBaseNameA,false,false; GetModuleBaseNameW,true,false; K32GetModuleBaseNameA,false,false; K32GetModuleBaseName,false,false; K32GetModuleBaseNameW,true,false; GetModuleFileNameEx,false,true; GetModuleFileNameExA,false,true; GetModuleFileNameExW,true,true;}
#[no_mangle]
pub unsafe extern "C" fn EnumProcessModulesEx(
    h: usize,
    arr: *mut usize,
    cb: u32,
    needed: *mut u32,
    _: u32,
) -> i32 {
    if needed.is_null() || (arr.is_null() && cb != 0) {
        error(87);
        return 0;
    }
    let Some(ms) = modules(h) else { return 0 };
    *needed = (ms.len() * 8).min(u32::MAX as usize) as u32;
    for (i, m) in ms.iter().take(cb as usize / 8).enumerate() {
        *arr.add(i) = m.base;
    }
    1
}
#[no_mangle]
pub unsafe extern "C" fn EnumProcessModules(h: usize, a: *mut usize, c: u32, n: *mut u32) -> i32 {
    EnumProcessModulesEx(h, a, c, n, 0)
}
#[no_mangle]
pub unsafe extern "C" fn K32EnumProcessModulesEx(
    h: usize,
    a: *mut usize,
    c: u32,
    n: *mut u32,
    f: u32,
) -> i32 {
    EnumProcessModulesEx(h, a, c, n, f)
}
#[repr(C)]
#[derive(Default)]
pub struct MODULEINFO {
    pub lpBaseOfDll: usize,
    pub SizeOfImage: u32,
    pub pad: u32,
    pub EntryPoint: usize,
}
#[no_mangle]
pub unsafe extern "C" fn GetModuleInformation(
    h: usize,
    b: usize,
    out: *mut MODULEINFO,
    cb: u32,
) -> i32 {
    if out.is_null() || (cb as usize) < size_of::<MODULEINFO>() {
        error(87);
        return 0;
    }
    let Some(ms) = modules(h) else { return 0 };
    let Some(m) = ms.iter().find(|m| m.base == b) else {
        error(6);
        return 0;
    };
    ptr::write(
        out,
        MODULEINFO {
            lpBaseOfDll: m.base,
            SizeOfImage: m.size,
            pad: 0,
            EntryPoint: 0,
        },
    );
    1
}
fn processes() -> Vec<u32> {
    let mut p: Vec<_> = fs::read_dir("/proc")
        .into_iter()
        .flatten()
        .filter_map(Result::ok)
        .filter_map(|e| e.file_name().to_str()?.parse::<u32>().ok())
        .collect();
    p.sort_unstable();
    p
}
#[no_mangle]
pub unsafe extern "C" fn EnumProcesses(out: *mut u32, cb: u32, needed: *mut u32) -> i32 {
    if needed.is_null() || (out.is_null() && cb != 0) {
        error(87);
        return 0;
    }
    let ps = processes();
    let n = ps.len().min(cb as usize / 4);
    if n > 0 {
        ptr::copy_nonoverlapping(ps.as_ptr(), out, n);
    }
    *needed = (n * 4) as u32;
    1
}
#[repr(C)]
pub struct PROCESSENTRY32 {
    pub dwSize: u32,
    pub cntUsage: u32,
    pub th32ProcessID: u32,
    pub th32DefaultHeapID: usize,
    pub th32ModuleID: u32,
    pub cntThreads: u32,
    pub th32ParentProcessID: u32,
    pub pcPriClassBase: i32,
    pub dwFlags: u32,
    pub szExeFile: [u8; 260],
}
#[repr(C)]
pub struct PROCESSENTRY32W {
    pub dwSize: u32,
    pub cntUsage: u32,
    pub th32ProcessID: u32,
    pub th32DefaultHeapID: usize,
    pub th32ModuleID: u32,
    pub cntThreads: u32,
    pub th32ParentProcessID: u32,
    pub pcPriClassBase: i32,
    pub dwFlags: u32,
    pub szExeFile: [u16; 260],
}
#[repr(C)]
pub struct MODULEENTRY32 {
    pub dwSize: u32,
    pub th32ModuleID: u32,
    pub th32ProcessID: u32,
    pub GlblcntUsage: u32,
    pub ProccntUsage: u32,
    pub modBaseAddr: usize,
    pub modBaseSize: u32,
    pub hModule: usize,
    pub szModule: [u8; 256],
    pub szExePath: [u8; 260],
}
#[repr(C)]
pub struct MODULEENTRY32W {
    pub dwSize: u32,
    pub th32ModuleID: u32,
    pub th32ProcessID: u32,
    pub GlblcntUsage: u32,
    pub ProccntUsage: u32,
    pub modBaseAddr: usize,
    pub modBaseSize: u32,
    pub hModule: usize,
    pub szModule: [u16; 256],
    pub szExePath: [u16; 260],
}
#[derive(Clone)]
struct Process {
    id: u32,
    name: String,
    parent: u32,
    threads: u32,
}
fn process_info(id: u32) -> Process {
    let root = format!("/proc/{id}");
    let comm = fs::read_to_string(format!("{root}/comm"))
        .unwrap_or_default()
        .trim()
        .to_owned();
    let cmd = fs::read(format!("{root}/cmdline")).unwrap_or_default();
    let arg = String::from_utf8_lossy(cmd.split(|b| *b == 0).next().unwrap_or_default());
    let name = if comm.to_ascii_lowercase().ends_with(".exe") {
        comm
    } else if !arg.is_empty() {
        basename(&arg).to_owned()
    } else {
        comm
    };
    let status = fs::read_to_string(format!("{root}/status")).unwrap_or_default();
    let field = |key: &str| {
        status
            .lines()
            .find_map(|l| l.strip_prefix(key)?.trim().parse().ok())
            .unwrap_or(0)
    };
    Process {
        id,
        name,
        parent: field("PPid:"),
        threads: field("Threads:"),
    }
}
struct Snapshot {
    processes: Vec<Process>,
    modules: Vec<Module>,
    pid: u32,
    process_index: Option<usize>,
    module_index: Option<usize>,
}
static SNAPSHOTS: OnceLock<Mutex<HashMap<usize, Snapshot>>> = OnceLock::new();
const TAG: usize = 1 << 63;
fn snapshots() -> std::sync::MutexGuard<'static, HashMap<usize, Snapshot>> {
    SNAPSHOTS
        .get_or_init(|| Mutex::new(HashMap::new()))
        .lock()
        .unwrap_or_else(|e| e.into_inner())
}
#[no_mangle]
pub unsafe extern "C" fn CreateToolhelp32Snapshot(flags: u32, p: u32) -> usize {
    if flags & (2 | 8 | 16) == 0 {
        error(50);
        return usize::MAX;
    }
    let p = if p == 0 { std::process::id() } else { p };
    let modules = if flags & (8 | 16) != 0 {
        let Some(ms) = modules(p as usize) else {
            return usize::MAX;
        };
        ms
    } else {
        Vec::new()
    };
    let processes = if flags & 2 != 0 {
        processes().into_iter().map(process_info).collect()
    } else {
        Vec::new()
    };
    let mut table = snapshots();
    let mut id = TAG | 1;
    while table.contains_key(&id) {
        id += 1;
    }
    table.insert(
        id,
        Snapshot {
            processes,
            modules,
            pid: p,
            process_index: None,
            module_index: None,
        },
    );
    id
}
#[no_mangle]
pub unsafe extern "C" fn CloseHandle(h: usize) -> i32 {
    if h & TAG != 0 {
        snapshots().remove(&h);
    }
    1
}
unsafe fn process_entry(h: usize, out: *mut c_void, wide: bool, first: bool) -> i32 {
    let expected = if wide {
        size_of::<PROCESSENTRY32W>()
    } else {
        size_of::<PROCESSENTRY32>()
    };
    if out.is_null() || (ptr::read(out.cast::<u32>()) as usize) < expected {
        error(87);
        return 0;
    }
    let mut table = snapshots();
    let Some(s) = table.get_mut(&h) else {
        error(6);
        return 0;
    };
    let i = if first {
        0
    } else {
        s.process_index.map_or(s.processes.len(), |i| i + 1)
    };
    s.process_index = Some(i);
    let Some(p) = s.processes.get(i) else {
        error(18);
        return 0;
    };
    if wide {
        let mut v: PROCESSENTRY32W = std::mem::zeroed();
        v.dwSize = expected as u32;
        v.th32ProcessID = p.id;
        v.th32ParentProcessID = p.parent;
        v.cntThreads = p.threads;
        string_out(&p.name, v.szExeFile.as_mut_ptr().cast(), 260, true);
        ptr::write(out.cast(), v);
    } else {
        let mut v: PROCESSENTRY32 = std::mem::zeroed();
        v.dwSize = expected as u32;
        v.th32ProcessID = p.id;
        v.th32ParentProcessID = p.parent;
        v.cntThreads = p.threads;
        string_out(&p.name, v.szExeFile.as_mut_ptr().cast(), 260, false);
        ptr::write(out.cast(), v);
    }
    1
}
unsafe fn module_entry(h: usize, out: *mut c_void, wide: bool, first: bool) -> i32 {
    let expected = if wide {
        size_of::<MODULEENTRY32W>()
    } else {
        size_of::<MODULEENTRY32>()
    };
    if out.is_null() || (ptr::read(out.cast::<u32>()) as usize) < expected {
        error(87);
        return 0;
    }
    let mut table = snapshots();
    let Some(s) = table.get_mut(&h) else {
        error(6);
        return 0;
    };
    let i = if first {
        0
    } else {
        s.module_index.map_or(s.modules.len(), |i| i + 1)
    };
    s.module_index = Some(i);
    let Some(m) = s.modules.get(i) else {
        error(18);
        return 0;
    };
    macro_rules! fill {
        ($ty:ty,$w:expr) => {{
            let mut v: $ty = std::mem::zeroed();
            v.dwSize = expected as u32;
            v.th32ModuleID = (i + 1) as u32;
            v.th32ProcessID = s.pid;
            v.GlblcntUsage = 1;
            v.ProccntUsage = 1;
            v.modBaseAddr = m.base;
            v.modBaseSize = m.size;
            v.hModule = m.base;
            string_out(basename(&m.path), v.szModule.as_mut_ptr().cast(), 256, $w);
            string_out(
                &windows_path(&m.path),
                v.szExePath.as_mut_ptr().cast(),
                260,
                $w,
            );
            ptr::write(out.cast(), v);
        }};
    }
    if wide {
        fill!(MODULEENTRY32W, true);
    } else {
        fill!(MODULEENTRY32, false);
    }
    1
}
macro_rules! entries {($($name:ident,$fun:ident,$wide:expr,$first:expr);* $(;)?)=>{$(
#[no_mangle] pub unsafe extern "C" fn $name(h:usize,out:*mut c_void)->i32{$fun(h,out,$wide,$first)}
)*};}
entries! {Process32First,process_entry,false,true;Process32Next,process_entry,false,false;Process32FirstA,process_entry,false,true;Process32NextA,process_entry,false,false;Process32FirstW,process_entry,true,true;Process32NextW,process_entry,true,false;Module32First,module_entry,false,true;Module32Next,module_entry,false,false;Module32FirstA,module_entry,false,true;Module32NextA,module_entry,false,false;Module32FirstW,module_entry,true,true;Module32NextW,module_entry,true,false;}
fn wow64(h: usize) -> Option<bool> {
    let p = pid(h)?;
    let result = fs::File::open(format!("/proc/{p}/exe")).and_then(|file| {
        let mut header = [0u8; 5];
        file.read_exact_at(&mut header, 0)?;
        Ok(header)
    });
    match result {
        Ok(b) if &b[..4] == b"\x7fELF" && matches!(b[4], 1 | 2) => Some(b[4] == 1),
        Ok(_) => {
            error(87);
            None
        }
        Err(e) => {
            io_error(&e);
            None
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn IsWow64Process(h: usize, out: *mut i32) -> i32 {
    if out.is_null() {
        error(87);
        return 0;
    }
    let Some(w) = wow64(h) else { return 0 };
    *out = w as i32;
    1
}
#[no_mangle]
pub unsafe extern "C" fn IsWow64Process2(h: usize, p: *mut u16, n: *mut u16) -> i32 {
    if p.is_null() || n.is_null() {
        error(87);
        return 0;
    }
    let Some(w) = wow64(h) else { return 0 };
    *p = if w { 0x14c } else { 0 };
    *n = 0x8664;
    1
}
#[repr(C)]
#[derive(Default)]
pub struct SYSTEM_INFO {
    pub wProcessorArchitecture: u16,
    pub wReserved: u16,
    pub dwPageSize: u32,
    pub lpMinimumApplicationAddress: usize,
    pub lpMaximumApplicationAddress: usize,
    pub dwActiveProcessorMask: usize,
    pub dwNumberOfProcessors: u32,
    pub dwProcessorType: u32,
    pub dwAllocationGranularity: u32,
    pub wProcessorLevel: u16,
    pub wProcessorRevision: u16,
}
#[no_mangle]
pub unsafe extern "C" fn GetSystemInfo(out: *mut SYSTEM_INFO) {
    if out.is_null() {
        error(87);
        return;
    }
    let n = libc::sysconf(libc::_SC_NPROCESSORS_ONLN).max(1) as u32;
    ptr::write(
        out,
        SYSTEM_INFO {
            wProcessorArchitecture: 9,
            dwPageSize: libc::sysconf(libc::_SC_PAGESIZE).max(4096) as u32,
            lpMinimumApplicationAddress: 0x10000,
            lpMaximumApplicationAddress: 0x7FFF_FFFE_FFFF,
            dwActiveProcessorMask: if n >= 64 {
                usize::MAX
            } else {
                (1usize << n) - 1
            },
            dwNumberOfProcessors: n,
            dwProcessorType: 8664,
            dwAllocationGranularity: 0x10000,
            ..Default::default()
        },
    );
}
#[no_mangle]
pub unsafe extern "C" fn GetNativeSystemInfo(out: *mut SYSTEM_INFO) {
    GetSystemInfo(out)
}
#[no_mangle]
pub unsafe extern "C" fn QueryPerformanceCounter(out: *mut i64) -> i32 {
    if out.is_null() {
        error(87);
        return 0;
    }
    let mut t: libc::timespec = std::mem::zeroed();
    if libc::clock_gettime(libc::CLOCK_MONOTONIC, &mut t) != 0 {
        error(50);
        return 0;
    }
    *out = t.tv_sec * 1_000_000_000 + t.tv_nsec;
    1
}
#[no_mangle]
pub unsafe extern "C" fn QueryPerformanceFrequency(out: *mut i64) -> i32 {
    if out.is_null() {
        error(87);
        return 0;
    }
    *out = 1_000_000_000;
    1
}
#[no_mangle]
pub unsafe extern "C" fn timeBeginPeriod(_: u32) -> u32 {
    0
}
#[no_mangle]
pub unsafe extern "C" fn timeEndPeriod(_: u32) -> u32 {
    0
}
unsafe fn signal(h: usize, s: i32) -> u32 {
    let Some(p) = pid(h) else { return 0xc0000022 };
    if libc::kill(p, s) == 0 {
        0
    } else {
        io_error(&io::Error::last_os_error());
        0xc0000022
    }
}
#[no_mangle]
pub unsafe extern "C" fn NtSuspendProcess(h: usize) -> u32 {
    signal(h, libc::SIGSTOP)
}
#[no_mangle]
pub unsafe extern "C" fn NtResumeProcess(h: usize) -> u32 {
    signal(h, libc::SIGCONT)
}
#[no_mangle]
pub unsafe extern "C" fn WaitForSingleObject(_: usize, _: u32) -> u32 {
    0
}
// Every unsupported entry has its actual Win32 parameter widths. Pointer parameters
// are opaque because these stubs never dereference them.
#[repr(C)]
pub struct POINT {
    pub x: i32,
    pub y: i32,
}
macro_rules! stubs {($($name:ident($($arg:ident:$ty:ty),*)->$ret:ty);* $(;)?)=>{$(
#[no_mangle] pub unsafe extern "C" fn $name($($arg:$ty),*)->$ret{ $(let _=$arg;)* error(50);0}
)*};}
stubs! {
AllocConsole()->i32;FreeConsole()->i32;
CreateProcessW(a:*const u16,b:*mut u16,c:*const c_void,d:*const c_void,e:i32,f:u32,g:*const c_void,h:*const u16,i:*const c_void,j:*mut c_void)->i32;
CreateProcessA(a:*const u8,b:*mut u8,c:*const c_void,d:*const c_void,e:i32,f:u32,g:*const c_void,h:*const u8,i:*const c_void,j:*mut c_void)->i32;
CreateProcess(a:*const u8,b:*mut u8,c:*const c_void,d:*const c_void,e:i32,f:u32,g:*const c_void,h:*const u8,i:*const c_void,j:*mut c_void)->i32;
CreateRemoteThread(a:usize,b:*const c_void,c:usize,d:usize,e:*mut c_void,f:u32,g:*mut u32)->usize;
OpenThread(a:u32,b:i32,c:u32)->usize;ResumeThread(a:usize)->u32;TerminateThread(a:usize,b:u32)->i32;
GetExitCodeThread(a:usize,b:*mut u32)->i32;
VirtualAlloc(a:*mut c_void,b:usize,c:u32,d:u32)->usize;
VirtualAllocEx(a:usize,b:*mut c_void,c:usize,d:u32,e:u32)->usize;
VirtualFreeEx(a:usize,b:*mut c_void,c:usize,d:u32)->i32;
VirtualProtectEx(a:usize,b:*mut c_void,c:usize,d:u32,e:*mut u32)->i32;
FlushInstructionCache(a:usize,b:*const c_void,c:usize)->i32;
FreeLibrary(a:usize)->i32;
GetModuleHandle(a:*const u8)->usize;GetModuleHandleA(a:*const u8)->usize;GetModuleHandleW(a:*const u16)->usize;
LoadLibrary(a:*const u8)->usize;LoadLibraryA(a:*const u8)->usize;LoadLibraryW(a:*const u16)->usize;
LoadLibraryEx(a:*const u8,b:usize,c:u32)->usize;LoadLibraryExA(a:*const u8,b:usize,c:u32)->usize;LoadLibraryExW(a:*const u16,b:usize,c:u32)->usize;
GetProcAddress(a:usize,b:*const u8)->usize;K32QueryWorkingSetEx(a:usize,b:*mut c_void,c:u32)->i32;
EnumDisplayMonitors(a:usize,b:*const c_void,c:usize,d:isize)->i32;
FindWindow(a:*const u8,b:*const u8)->usize;FindWindowA(a:*const u8,b:*const u8)->usize;FindWindowW(a:*const u16,b:*const u16)->usize;
GetAsyncKeyState(a:i32)->i16;GetKeyState(a:i32)->i16;
GetCapture()->usize;GetForegroundWindow()->usize;GetMenuItemCount(a:usize)->i32;GetMessageExtraInfo()->isize;
GetMonitorInfo(a:usize,b:*mut c_void)->i32;GetMonitorInfoA(a:usize,b:*mut c_void)->i32;GetMonitorInfoW(a:usize,b:*mut c_void)->i32;
GetSystemMenu(a:usize,b:i32)->usize;GetSystemMetrics(a:i32)->i32;GetWindowRect(a:usize,b:*mut c_void)->i32;
GetWindowThreadProcessId(a:usize,b:*mut u32)->u32;
MapVirtualKey(a:u32,b:u32)->u32;MapVirtualKeyA(a:u32,b:u32)->u32;MapVirtualKeyW(a:u32,b:u32)->u32;
MonitorFromPoint(a:POINT,b:u32)->usize;MonitorFromRect(a:*const c_void,b:u32)->usize;
RegisterHotKey(a:usize,b:i32,c:u32,d:u32)->i32;UnregisterHotKey(a:usize,b:i32)->i32;RemoveMenu(a:usize,b:u32,c:u32)->i32;
SendInput(a:u32,b:*const c_void,c:i32)->u32;
SendMessage(a:usize,b:u32,c:usize,d:isize)->isize;SendMessageA(a:usize,b:u32,c:usize,d:isize)->isize;SendMessageW(a:usize,b:u32,c:usize,d:isize)->isize;
SetFocus(a:usize)->usize;SetWindowPos(a:usize,b:usize,c:i32,d:i32,e:i32,f:i32,g:u32)->i32;
AddFontMemResourceEx(a:*const c_void,b:u32,c:*const c_void,d:*mut u32)->usize;
BitBlt(a:usize,b:i32,c:i32,d:i32,e:i32,f:usize,g:i32,h:i32,i:u32)->i32;
SymCleanup(a:usize)->i32;
SymEnumSymbols(a:usize,b:u64,c:*const u8,d:usize,e:*mut c_void)->i32;
SymEnumSymbolsA(a:usize,b:u64,c:*const u8,d:usize,e:*mut c_void)->i32;
SymEnumSymbolsW(a:usize,b:u64,c:*const u16,d:usize,e:*mut c_void)->i32;
SymInitialize(a:usize,b:*const u8,c:i32)->i32;SymInitializeA(a:usize,b:*const u8,c:i32)->i32;SymInitializeW(a:usize,b:*const u16,c:i32)->i32;
SymLoadModuleEx(a:usize,b:usize,c:*const u8,d:*const u8,e:u64,f:u32,g:*const c_void,h:u32)->u64;
SymLoadModuleExA(a:usize,b:usize,c:*const u8,d:*const u8,e:u64,f:u32,g:*const c_void,h:u32)->u64;
SymLoadModuleExW(a:usize,b:usize,c:*const u16,d:*const u16,e:u64,f:u32,g:*const c_void,h:u32)->u64;
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn wine_path_conversion() {
        assert_eq!(
            windows_path("/home/x/.wine/drive_c/foo/bar.dll"),
            "C:\\foo\\bar.dll"
        );
        assert_eq!(windows_path("/usr/lib/libc.so.6"), "/usr/lib/libc.so.6");
    }
    #[test]
    fn mapped_pe_detection() {
        unsafe {
            let path =
                std::env::temp_dir().join(format!("win32shim-{}-fake.dll", std::process::id()));
            let f = fs::OpenOptions::new()
                .create_new(true)
                .read(true)
                .write(true)
                .open(&path)
                .unwrap();
            f.set_len(4096).unwrap();
            let mut page = [0u8; 4096];
            page[..2].copy_from_slice(b"MZ");
            page[60..64].copy_from_slice(&128u32.to_le_bytes());
            page[128..132].copy_from_slice(b"PE\0\0");
            page[208..212].copy_from_slice(&0x5000u32.to_le_bytes());
            f.write_at(&page, 0).unwrap();
            use std::os::fd::AsRawFd;
            let p = libc::mmap(
                ptr::null_mut(),
                4096,
                libc::PROT_READ,
                libc::MAP_PRIVATE,
                f.as_raw_fd(),
                0,
            );
            assert_ne!(p, libc::MAP_FAILED);
            let h = std::process::id() as usize;
            let detected = pe_size(h, p as usize);
            let list = modules(h).unwrap();
            libc::munmap(p, 4096);
            fs::remove_file(path).unwrap();
            assert_eq!(detected, Some(0x5000));
            assert!(!list.is_empty());
            assert!(list.iter().all(|m| m.pe));
            assert!(list
                .iter()
                .any(|m| m.base == p as usize && m.size == 0x5000));
        }
    }
}
