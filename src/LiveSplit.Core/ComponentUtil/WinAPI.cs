using System;
using System.Runtime.InteropServices;

using SizeT = System.UIntPtr;

namespace LiveSplit.ComponentUtil;

public enum MemPageState : uint
{
    MEM_COMMIT = 0x1000,
    MEM_RESERVE = 0x2000,
    MEM_FREE = 0x10000,
}

public enum MemPageType : uint
{
    MEM_PRIVATE = 0x20000,
    MEM_MAPPED = 0x40000,
    MEM_IMAGE = 0x1000000
}

[Flags]
public enum MemPageProtect : uint
{
    PAGE_NOACCESS = 0x01,
    PAGE_READONLY = 0x02,
    PAGE_READWRITE = 0x04,
    PAGE_WRITECOPY = 0x08,
    PAGE_EXECUTE = 0x10,
    PAGE_EXECUTE_READ = 0x20,
    PAGE_EXECUTE_READWRITE = 0x40,
    PAGE_EXECUTE_WRITECOPY = 0x80,
    PAGE_GUARD = 0x100,
    PAGE_NOCACHE = 0x200,
    PAGE_WRITECOMBINE = 0x400,
}

[StructLayout(LayoutKind.Sequential)]
public struct MemoryBasicInformation // MEMORY_BASIC_INFORMATION
{
    public IntPtr BaseAddress;
    public IntPtr AllocationBase;
    public MemPageProtect AllocationProtect;
    public SizeT RegionSize;
    public MemPageState State;
    public MemPageProtect Protect;
    public MemPageType Type;
}
