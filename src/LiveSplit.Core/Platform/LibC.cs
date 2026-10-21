using System;
using System.Runtime.InteropServices;

namespace LiveSplit.Hosting;

internal static unsafe class LibC
{
    public const int O_RDONLY = 0;
    public const int O_RDWR = 2;
    public const int O_NONBLOCK = 0x800;
    public const int O_CLOEXEC = 0x80000;

    public const short POLLIN = 0x1;
    public const short POLLERR = 0x8;
    public const short POLLHUP = 0x10;
    public const short POLLNVAL = 0x20;

    public const int EINTR = 4;
    public const int EAGAIN = 11;
    public const int EACCES = 13;
    public const int ENODEV = 19;
    public const int EPERM = 1;
    public const int ESRCH = 3;
    public const int EFAULT = 14;

    [StructLayout(LayoutKind.Sequential)]
    public struct PollFd
    {
        public int fd;
        public short events;
        public short revents;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IoVec
    {
        public void* iov_base;
        public nuint iov_len;
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    public static extern int open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    public static extern int close(int fd);

    [DllImport("libc", EntryPoint = "read", SetLastError = true)]
    public static extern nint read(int fd, byte* buffer, nuint count);

    [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
    public static extern int poll(PollFd* fds, nuint nfds, int timeout);

    [DllImport("libc", EntryPoint = "process_vm_readv", SetLastError = true)]
    public static extern nint process_vm_readv(int pid, IoVec* localIov, nuint localCount, IoVec* remoteIov, nuint remoteCount, nuint flags);

    [DllImport("libc", EntryPoint = "process_vm_writev", SetLastError = true)]
    public static extern nint process_vm_writev(int pid, IoVec* localIov, nuint localCount, IoVec* remoteIov, nuint remoteCount, nuint flags);

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    public static extern int kill(int pid, int signal);

    public static int LastError => Marshal.GetLastPInvokeError();
}
