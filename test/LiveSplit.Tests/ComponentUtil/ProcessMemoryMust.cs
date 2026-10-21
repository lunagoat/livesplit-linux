using LiveSplit.ComponentUtil;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace LiveSplit.Tests.ComponentUtil;

public class ProcessMemoryMust
{
    private static unsafe IntPtr AddressOf(GCHandle handle)
    {
        return handle.AddrOfPinnedObject();
    }

    [Fact]
    public void ReadValuesOfTheCurrentProcess()
    {
        var buffer = new byte[64];
        BitConverter.GetBytes(0x1234_5678).CopyTo(buffer, 0);
        BitConverter.GetBytes(3.5f).CopyTo(buffer, 8);
        BitConverter.GetBytes(0x1122_3344_5566_7788L).CopyTo(buffer, 16);
        Encoding.UTF8.GetBytes("hello").CopyTo(buffer, 32);
        Encoding.Unicode.GetBytes("wide").CopyTo(buffer, 48);
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            using Process process = Process.GetCurrentProcess();
            IntPtr address = AddressOf(handle);

            Assert.Equal(0x1234_5678, process.ReadValue<int>(address));
            Assert.Equal(3.5f, process.ReadValue<float>(address + 8));
            Assert.Equal(0x1122_3344_5566_7788L, process.ReadValue<long>(address + 16));
            Assert.Equal((IntPtr)0x1122_3344_5566_7788L, process.ReadPointer(address + 16));
            Assert.Equal("hello", process.ReadString(address + 32, ReadStringType.UTF8, 16));
            Assert.Equal("wide", process.ReadString(address + 48, ReadStringType.UTF16, 16));
            Assert.Equal(buffer[..8], process.ReadBytes(address, 8));
        }
        finally
        {
            handle.Free();
        }
    }

    [Fact]
    public void ReturnDefaultsWhenReadingUnmappedMemory()
    {
        using Process process = Process.GetCurrentProcess();

        Assert.False(process.ReadValue(new IntPtr(8), out int _));
        Assert.Equal(-1, process.ReadValue(new IntPtr(8), -1));
        Assert.Null(process.ReadBytes(new IntPtr(8), 16));
    }

    [Fact]
    public void WriteValuesIntoTheCurrentProcess()
    {
        var buffer = new byte[16];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            using Process process = Process.GetCurrentProcess();
            IntPtr address = AddressOf(handle);

            Assert.True(process.WriteBytes(address, [1, 2, 3, 4]));
            Assert.True(process.WriteValue(address + 8, 0x0A0B0C0D));

            Assert.Equal([1, 2, 3, 4], buffer[..4]);
            Assert.Equal(0x0A0B0C0D, BitConverter.ToInt32(buffer, 8));
        }
        finally
        {
            handle.Free();
        }
    }

    [Fact]
    public void ListTheModulesOfTheCurrentProcess()
    {
        using Process process = Process.GetCurrentProcess();

        ProcessModuleWow64Safe[] modules = process.ModulesWow64Safe();

        Assert.NotEmpty(modules);
        Assert.Contains(modules, x => x.ModuleName.StartsWith("libc.", StringComparison.Ordinal) || x.ModuleName.StartsWith("libc-", StringComparison.Ordinal));
        Assert.All(modules, x => Assert.NotEqual(IntPtr.Zero, x.BaseAddress));
        Assert.Equal(modules[0].BaseAddress, process.MainModuleWow64Safe().BaseAddress);
        Assert.True(process.Is64Bit());
    }

    [Fact]
    public void EnumerateMemoryPagesOfTheCurrentProcess()
    {
        var buffer = new byte[16];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            using Process process = Process.GetCurrentProcess();
            long address = (long)AddressOf(handle);

            MemoryBasicInformation[] pages = [.. process.MemoryPages(true)];

            Assert.Contains(pages, x => (long)x.BaseAddress <= address && address < (long)x.BaseAddress + (long)(ulong)x.RegionSize);
            Assert.All(process.MemoryPages(false), x => Assert.Equal(MemPageType.MEM_PRIVATE, x.Type));
        }
        finally
        {
            handle.Free();
        }
    }

    [Fact]
    public void ScanForSignaturesInTheCurrentProcess()
    {
        var buffer = new byte[256];
        new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x12, 0x34 }.CopyTo(buffer, 100);
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            using Process process = Process.GetCurrentProcess();
            IntPtr address = AddressOf(handle);
            var scanner = new SignatureScanner(process, address, buffer.Length);

            IntPtr found = scanner.Scan(new SigScanTarget("DE AD BE EF ?? 34"));

            Assert.Equal(address + 100, found);
        }
        finally
        {
            handle.Free();
        }
    }

    [Fact]
    public void ReadMemoryOfASpawnedProcess()
    {
        using Process sleeper = Process.Start("sleep", "30");
        try
        {
            ProcessModuleWow64Safe[] modules = [];
            for (int i = 0; i < 50 && modules.Length == 0; i++)
            {
                System.Threading.Thread.Sleep(20);
                modules = sleeper.ModulesWow64Safe();
            }

            Assert.Equal("sleep", modules[0].ModuleName);
            Assert.Contains(modules, x => x.ModuleName.StartsWith("libc", StringComparison.Ordinal));
            Assert.True(sleeper.Is64Bit());

            byte[] elf = sleeper.ReadBytes(modules[0].BaseAddress, 4);
            Assert.Equal([0x7F, (byte)'E', (byte)'L', (byte)'F'], elf);

            IntPtr stack = sleeper.MemoryPages(true).First().BaseAddress;
            Assert.NotEqual(IntPtr.Zero, stack);
        }
        finally
        {
            sleeper.Kill();
            sleeper.WaitForExit();
        }
    }

    [Fact]
    public void FindProcessesByNameLikeWindows()
    {
        using Process sleeper = Process.Start("sleep", "30");
        try
        {
            Process[] byName = ProcessUtil.GetProcessesByName("sleep");
            Process[] byExeName = ProcessUtil.GetProcessesByName("SLEEP.exe");

            Assert.Contains(byName, x => x.Id == sleeper.Id);
            Assert.Contains(byExeName, x => x.Id == sleeper.Id);
            Assert.Empty(ProcessUtil.GetProcessesByName("definitely-not-a-process"));
        }
        finally
        {
            sleeper.Kill();
            sleeper.WaitForExit();
        }
    }

    [Fact]
    public void ParseProcMapsLines()
    {
        MemoryMapEntry entry = ProcessMemory.ParseMapsLine("7f1234560000-7f1234580000 r-xp 00001000 fd:01 1234  /usr/lib/my lib.so (deleted)");

        Assert.Equal(0x7f1234560000UL, entry.Start);
        Assert.Equal(0x20000UL, entry.Size);
        Assert.True(entry.Readable);
        Assert.False(entry.Writable);
        Assert.True(entry.Executable);
        Assert.Equal(0x1000UL, entry.Offset);
        Assert.Equal("/usr/lib/my lib.so", entry.Path);

        MemoryMapEntry anonymous = ProcessMemory.ParseMapsLine("7ffd00000000-7ffd00021000 rw-p 00000000 00:00 0");
        Assert.False(anonymous.IsFileBacked);
        Assert.Equal(string.Empty, anonymous.Path);
    }

    [Fact]
    public void DerivePeInformationFromWineStyleModules()
    {
        var image = new byte[0x400];
        image[0] = (byte)'M';
        image[1] = (byte)'Z';
        BitConverter.GetBytes(0x80).CopyTo(image, 0x3C);
        Encoding.ASCII.GetBytes("PE").CopyTo(image, 0x80);
        BitConverter.GetBytes((ushort)0x20B).CopyTo(image, 0x80 + 24);
        BitConverter.GetBytes(0x1000).CopyTo(image, 0x80 + 24 + 16);
        BitConverter.GetBytes(0x234000).CopyTo(image, 0x80 + 24 + 56);
        var handle = GCHandle.Alloc(image, GCHandleType.Pinned);
        try
        {
            ulong address = (ulong)(long)AddressOf(handle);

            Assert.True(ProcessMemory.TryReadPeInfo(Environment.ProcessId, address, out int size, out int entry, out bool is64));

            Assert.Equal(0x234000, size);
            Assert.Equal(0x1000, entry);
            Assert.True(is64);
        }
        finally
        {
            handle.Free();
        }
    }
}
