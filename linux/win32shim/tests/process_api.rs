use std::{
    ffi::c_void,
    mem::{offset_of, size_of},
    process::{Child, Command, Stdio},
    ptr,
};
use win32shim::*;

struct ChildGuard(Child);
impl Drop for ChildGuard {
    fn drop(&mut self) {
        let _ = self.0.kill();
        let _ = self.0.wait();
    }
}
fn child() -> ChildGuard {
    ChildGuard(
        Command::new("/bin/sh")
            .args(["-c", "read line"])
            .stdin(Stdio::piped())
            .spawn()
            .unwrap(),
    )
}
unsafe fn module_list(h: usize) -> Vec<usize> {
    let mut needed = 0;
    assert_eq!(
        EnumProcessModulesEx(h, ptr::null_mut(), 0, &mut needed, 3),
        1
    );
    let mut out = vec![0; needed as usize / 8];
    assert!(!out.is_empty());
    assert_eq!(
        EnumProcessModules(h, out.as_mut_ptr(), needed, &mut needed),
        1
    );
    out
}
#[test]
fn windows_x64_layouts() {
    assert_eq!(size_of::<MEMORY_BASIC_INFORMATION>(), 48);
    assert_eq!(offset_of!(MEMORY_BASIC_INFORMATION, RegionSize), 24);
    assert_eq!(size_of::<MODULEINFO>(), 24);
    assert_eq!(offset_of!(MODULEINFO, EntryPoint), 16);
    assert_eq!(size_of::<SYSTEM_INFO>(), 48);
    assert_eq!(offset_of!(SYSTEM_INFO, dwNumberOfProcessors), 32);
    assert_eq!(size_of::<PROCESSENTRY32>(), 304);
    assert_eq!(size_of::<PROCESSENTRY32W>(), 568);
    assert_eq!(offset_of!(PROCESSENTRY32, th32DefaultHeapID), 16);
    assert_eq!(offset_of!(PROCESSENTRY32W, szExeFile), 44);
    assert_eq!(size_of::<MODULEENTRY32>(), 568);
    assert_eq!(size_of::<MODULEENTRY32W>(), 1080);
    assert_eq!(offset_of!(MODULEENTRY32, modBaseAddr), 24);
    assert_eq!(offset_of!(MODULEENTRY32W, hModule), 40);
}
#[test]
fn self_memory_and_errors() {
    unsafe {
        let h = OpenProcess(0, 0, std::process::id());
        assert_ne!(h, 0);
        let mut source = vec![0x12u8, 0x34, 0x56, 0x78];
        let mut buf = [0u8; 4];
        let mut count = 0;
        assert_eq!(
            ReadProcessMemory(
                h,
                source.as_ptr().cast(),
                buf.as_mut_ptr().cast(),
                4,
                &mut count
            ),
            1
        );
        assert_eq!(count, 4);
        assert_eq!(buf, source.as_slice());
        let replacement = [9u8, 8, 7, 6];
        assert_eq!(
            WriteProcessMemory(
                h,
                source.as_mut_ptr().cast(),
                replacement.as_ptr().cast(),
                4,
                &mut count
            ),
            1
        );
        assert_eq!(source, replacement);
        let mut mbi = MEMORY_BASIC_INFORMATION::default();
        assert_eq!(VirtualQueryEx(h, source.as_ptr().cast(), &mut mbi, 48), 48);
        assert_eq!(mbi.State, 0x1000);
        assert!(mbi.BaseAddress <= source.as_ptr() as usize);
        assert!(mbi.BaseAddress + mbi.RegionSize > source.as_ptr() as usize);
        assert_eq!(VirtualQueryEx(h, ptr::null(), &mut mbi, 48), 48);
        assert_eq!(mbi.State, 0x10000);
        assert_eq!(mbi.Protect, 1);
        assert!(mbi.RegionSize > 0);
        assert_eq!(
            ReadProcessMemory(h, ptr::null(), buf.as_mut_ptr().cast(), 4, &mut count),
            0
        );
        assert_eq!(GetLastError(), 299);
        assert_eq!(count, 0);
        assert_eq!(
            ReadProcessMemory(h, ptr::null(), ptr::null_mut(), 0, ptr::null_mut()),
            1
        );
        assert_eq!(OpenProcess(0, 0, 0), 0);
        assert_eq!(GetLastError(), 87);
        assert_eq!(
            ReadProcessMemory(
                usize::MAX,
                ptr::null(),
                buf.as_mut_ptr().cast(),
                4,
                &mut count
            ),
            0
        );
        assert_eq!(GetLastError(), 6);
        assert_eq!(VirtualQueryEx(h, source.as_ptr().cast(), &mut mbi, 47), 0);
        assert_eq!(GetLastError(), 87);
        CloseHandle(h);
    }
}
#[test]
fn partial_copy() {
    unsafe {
        let page = libc::sysconf(libc::_SC_PAGESIZE) as usize;
        let mem = libc::mmap(
            ptr::null_mut(),
            page * 3,
            libc::PROT_READ | libc::PROT_WRITE,
            libc::MAP_PRIVATE | libc::MAP_ANONYMOUS,
            -1,
            0,
        );
        assert_ne!(mem, libc::MAP_FAILED);
        ptr::write_bytes(mem, 0x5a, page);
        let mut buf = vec![0u8; page * 2];
        assert_eq!(libc::munmap(mem.add(page), page), 0);
        let mut count = 0;
        assert_eq!(
            ReadProcessMemory(
                std::process::id() as usize,
                mem,
                buf.as_mut_ptr().cast(),
                buf.len(),
                &mut count
            ),
            0
        );
        assert_eq!(GetLastError(), 299);
        assert_eq!(count, page);
        assert!(buf[..page].iter().all(|b| *b == 0x5a));
        libc::munmap(mem, page);
        libc::munmap(mem.add(page * 2), page);
    }
}
#[test]
fn child_modules_and_snapshots() {
    unsafe {
        let child = child();
        let id = child.0.id();
        let h = OpenProcess(0x410, 0, id);
        assert_eq!(h, id as usize);
        let ms = module_list(h);
        let mut header = [0u8; 4];
        assert_eq!(
            ReadProcessMemory(
                h,
                ms[0] as *const c_void,
                header.as_mut_ptr().cast(),
                4,
                ptr::null_mut()
            ),
            1
        );
        assert_eq!(&header, b"\x7fELF");
        let mut name = [0u8; 260];
        let n = GetModuleBaseName(h, ms[0], name.as_mut_ptr().cast(), 260) as usize;
        assert!(["sh", "dash", "bash"].contains(&std::str::from_utf8(&name[..n]).unwrap()));
        let mut wide = [0u16; 260];
        let nw = GetModuleBaseNameW(h, ms[0], wide.as_mut_ptr().cast(), 260) as usize;
        assert_eq!(
            String::from_utf16(&wide[..nw]).unwrap().as_bytes(),
            &name[..n]
        );
        let mut short = [1u8; 2];
        assert_eq!(
            GetModuleBaseNameA(h, ms[0], short.as_mut_ptr().cast(), 2),
            1
        );
        assert_eq!(short[1], 0);
        let mut info = MODULEINFO::default();
        assert_eq!(GetModuleInformation(h, ms[0], &mut info, 24), 1);
        assert_eq!(info.lpBaseOfDll, ms[0]);
        assert!(info.SizeOfImage >= 4096);
        let mut is32 = 1;
        assert_eq!(IsWow64Process(h, &mut is32), 1);
        assert_eq!(is32, 0);
        let (mut pm, mut nm) = (1, 0);
        assert_eq!(IsWow64Process2(h, &mut pm, &mut nm), 1);
        assert_eq!((pm, nm), (0, 0x8664));
        let s = CreateToolhelp32Snapshot(2, 0);
        assert_ne!(s, usize::MAX);
        let mut entry: PROCESSENTRY32 = std::mem::zeroed();
        entry.dwSize = size_of::<PROCESSENTRY32>() as u32;
        let mut found = false;
        let mut ok = Process32First(s, (&mut entry as *mut PROCESSENTRY32).cast());
        while ok != 0 {
            found |= entry.th32ProcessID == id;
            ok = Process32Next(s, (&mut entry as *mut PROCESSENTRY32).cast());
        }
        assert!(found);
        assert_eq!(GetLastError(), 18);
        let mut ew: PROCESSENTRY32W = std::mem::zeroed();
        ew.dwSize = size_of::<PROCESSENTRY32W>() as u32;
        assert_eq!(
            Process32FirstW(s, (&mut ew as *mut PROCESSENTRY32W).cast()),
            1
        );
        CloseHandle(s);
        assert_eq!(
            Process32Next(s, (&mut entry as *mut PROCESSENTRY32).cast()),
            0
        );
        assert_eq!(GetLastError(), 6);
        let s = CreateToolhelp32Snapshot(8 | 16, id);
        let mut m: MODULEENTRY32W = std::mem::zeroed();
        m.dwSize = size_of::<MODULEENTRY32W>() as u32;
        assert_eq!(Module32FirstW(s, (&mut m as *mut MODULEENTRY32W).cast()), 1);
        assert_eq!(m.modBaseAddr, ms[0]);
        assert_eq!(m.th32ProcessID, id);
        let mut ma: MODULEENTRY32 = std::mem::zeroed();
        ma.dwSize = size_of::<MODULEENTRY32>() as u32;
        assert_eq!(Module32First(s, (&mut ma as *mut MODULEENTRY32).cast()), 1);
        assert_eq!(ma.modBaseAddr, ms[0]);
        CloseHandle(s);
        let mut pids = vec![0; 65536];
        let mut needed = 0;
        assert_eq!(
            EnumProcesses(pids.as_mut_ptr(), (pids.len() * 4) as u32, &mut needed),
            1
        );
        assert!(pids[..needed as usize / 4].contains(&id));
    }
}
#[test]
fn clocks_system_info_stubs_and_tls() {
    unsafe {
        let (mut a, mut b, mut f) = (0, 0, 0);
        assert_eq!(QueryPerformanceFrequency(&mut f), 1);
        assert_eq!(f, 1_000_000_000);
        assert_eq!(QueryPerformanceCounter(&mut a), 1);
        assert_eq!(QueryPerformanceCounter(&mut b), 1);
        assert!(b >= a);
        let mut info = SYSTEM_INFO::default();
        GetNativeSystemInfo(&mut info);
        assert_eq!(info.wProcessorArchitecture, 9);
        assert!(info.dwNumberOfProcessors > 0);
        assert!(info.dwPageSize >= 4096);
        assert_eq!(timeBeginPeriod(1), 0);
        assert_eq!(timeEndPeriod(1), 0);
        assert_eq!(WaitForSingleObject(0, 0), 0);
        assert_eq!(AllocConsole(), 0);
        assert_eq!(GetLastError(), 50);
        std::thread::spawn(|| {
            assert_eq!(GetLastError(), 0);
            OpenProcess(0, 0, 0);
            assert_eq!(GetLastError(), 87);
        })
        .join()
        .unwrap();
        assert_eq!(GetLastError(), 50);
    }
}

#[test]
fn virtual_query_past_user_space_ends_scan_with_errno_87() {
    unsafe {
        let c = child();
        let h = OpenProcess(0x1fffff, 0, c.0.id());
        assert_ne!(h, 0);
        let mut addr = 0usize;
        let mut calls = 0;
        loop {
            let mut info = MEMORY_BASIC_INFORMATION::default();
            let n = VirtualQueryEx(h, addr as *const c_void, &mut info, size_of::<MEMORY_BASIC_INFORMATION>());
            if n == 0 {
                // .NET (SetLastError=true) reads errno, scanners compare it with ERROR_INVALID_PARAMETER.
                assert_eq!(*libc::__errno_location(), 87);
                assert_eq!(GetLastError(), 87);
                break;
            }
            addr = info.BaseAddress + info.RegionSize;
            calls += 1;
            assert!(calls < 100_000, "scan did not terminate");
        }
    }
}
