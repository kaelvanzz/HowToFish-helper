using System.ComponentModel;
using System.Runtime.InteropServices;

namespace HTFInjector;

internal static class Native
{
    public const uint PROCESS_CREATE_THREAD = 0x0002;
    public const uint PROCESS_QUERY_INFORMATION = 0x0400;
    public const uint PROCESS_VM_OPERATION = 0x0008;
    public const uint PROCESS_VM_WRITE = 0x0020;
    public const uint PROCESS_VM_READ = 0x0010;
    public const uint PROCESS_ACCESS =
        PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION |
        PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ;

    public const uint MEM_COMMIT = 0x1000;
    public const uint MEM_RESERVE = 0x2000;
    public const uint MEM_RELEASE = 0x8000;

    public const uint PAGE_READWRITE = 0x04;
    public const uint PAGE_EXECUTE_READWRITE = 0x40;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MODULEENTRY32W
    {
        public uint dwSize;
        public uint th32ModuleID;
        public uint th32ProcessID;
        public uint GlblcntUsage;
        public uint ProccntUsage;
        public nint modBaseAddr;
        public uint modBaseSize;
        public nint hModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExePath;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(nint handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint VirtualAllocEx(nint proc, nint addr, nint size, uint type, uint protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualFreeEx(nint proc, nint addr, nint size, uint type);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteProcessMemory(nint proc, nint addr, byte[] buffer, nint size, out nint written);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(nint proc, nint addr, byte[] buffer, nint size, out nint read);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint CreateRemoteThread(nint proc, nint attr, nint stack, nint start, nint param, uint flags, out uint tid);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(nint handle, uint ms);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetExitCodeThread(nint handle, out uint code);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint CreateToolhelp32Snapshot(uint flags, uint pid);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool Module32FirstW(nint snapshot, ref MODULEENTRY32W entry);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool Module32NextW(nint snapshot, ref MODULEENTRY32W entry);

    public static void ThrowWin32(string what)
    {
        int err = Marshal.GetLastWin32Error();
        throw new Win32Exception(err, $"{what} failed (Win32Error {err})");
    }
}

internal sealed class TargetProcess : IDisposable
{
    public int Pid { get; }
    public string Name { get; }
    public nint Handle { get; }

    public TargetProcess(int pid, string name)
    {
        Pid = pid;
        Name = name;
        Handle = Native.OpenProcess(Native.PROCESS_ACCESS, false, pid);
        if (Handle == nint.Zero)
            Native.ThrowWin32($"OpenProcess({pid})");
    }

    public (nint Base, uint Size) FindModule(string name)
    {
        const uint TH32CS_SNAPMODULE = 0x00000008;
        nint snap = Native.CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, (uint)Pid);
        if (snap == nint.Zero || snap == new nint(-1))
            Native.ThrowWin32("CreateToolhelp32Snapshot");

        try
        {
            var entry = new Native.MODULEENTRY32W();
            entry.dwSize = (uint)Marshal.SizeOf<Native.MODULEENTRY32W>();

            if (!Native.Module32FirstW(snap, ref entry))
                Native.ThrowWin32("Module32First");

            do
            {
                if (string.Equals(entry.szModule, name, StringComparison.OrdinalIgnoreCase))
                    return (entry.modBaseAddr, entry.modBaseSize);
            }
            while (Native.Module32NextW(snap, ref entry));

            throw new Exception($"目标进程中找不到模块 {name}");
        }
        finally
        {
            Native.CloseHandle(snap);
        }
    }

    public byte[] ReadRemote(nint addr, int size)
    {
        var buf = new byte[size];
        if (!Native.ReadProcessMemory(Handle, addr, buf, size, out nint read) || read != size)
            Native.ThrowWin32($"ReadProcessMemory(0x{addr:X})");
        return buf;
    }

    public void WriteRemote(nint addr, byte[] data)
    {
        if (!Native.WriteProcessMemory(Handle, addr, data, data.Length, out nint written) || written != data.Length)
            Native.ThrowWin32($"WriteProcessMemory(0x{addr:X})");
    }

    public nint AllocRemote(int size, uint protect = Native.PAGE_READWRITE)
    {
        nint p = Native.VirtualAllocEx(Handle, nint.Zero, size, Native.MEM_COMMIT | Native.MEM_RESERVE, protect);
        if (p == nint.Zero)
            Native.ThrowWin32("VirtualAllocEx");
        return p;
    }

    public void FreeRemote(nint addr)
    {
        Native.VirtualFreeEx(Handle, addr, nint.Zero, Native.MEM_RELEASE);
    }

    /// <summary>在目标进程中用 stub 执行一次函数调用,返回写入参数块末尾的结果。</summary>
    public ulong CallStub(nint stub, nint paramBlock, byte[] paramData, int timeoutMs = 8000)
    {
        WriteRemote(paramBlock, paramData);

        nint thread = Native.CreateRemoteThread(Handle, nint.Zero, nint.Zero, stub, paramBlock, 0, out _);
        if (thread == nint.Zero)
            Native.ThrowWin32("CreateRemoteThread");

        try
        {
            uint wait = Native.WaitForSingleObject(thread, (uint)timeoutMs);
            Native.GetExitCodeThread(thread, out uint exitCode);
            Console.WriteLine($"[D] stub call done: wait={wait} exitCode=0x{exitCode:X}");
            if (wait != 0 /* WAIT_OBJECT_0 */)
                throw new TimeoutException("远程线程未在限时内返回");
        }
        finally
        {
            Native.CloseHandle(thread);
        }

        byte[] res = ReadRemote(paramBlock + 40, 8);
        return BitConverter.ToUInt64(res, 0);
    }

    public void Dispose()
    {
        if (Handle != nint.Zero) Native.CloseHandle(Handle);
    }
}

/// <summary>解析远程模块的 PE 导出表并解析具名导出。</summary>
internal sealed class RemoteExports
{
    private readonly TargetProcess _proc;
    private readonly nint _base;
    private readonly Dictionary<string, nint> _funcs = new(StringComparer.Ordinal);

    public RemoteExports(TargetProcess proc, nint moduleBase)
    {
        _proc = proc;
        _base = moduleBase;
        Parse();
    }

    public nint this[string name] =>
        _funcs.TryGetValue(name, out nint addr)
            ? addr
            : throw new Exception($"mono 导出函数不存在: {name}");

    private static nint Off(nint b, long o) => (nint)(b + o);

    private void Parse()
    {
        byte[] dos = _proc.ReadRemote(_base, 0x40);
        int eLfanew = BitConverter.ToInt32(dos, 0x3C);
        if (eLfanew <= 0 || eLfanew > 0x400)
            throw new Exception("PE 头解析失败 (e_lfanew 异常)");

        byte[] nt = _proc.ReadRemote(Off(_base, eLfanew), 0x18 + 0xF0);
        if (nt[0] != 'P' || nt[1] != 'E')
            throw new Exception("PE 签名校验失败");

        int optOffset = 0x18;
        ushort magic = BitConverter.ToUInt16(nt, optOffset);
        int dataDirOffset;
        if (magic == 0x20B) dataDirOffset = optOffset + 112;       // PE32+
        else if (magic == 0x10B) dataDirOffset = optOffset + 96;   // PE32
        else throw new Exception($"未知 PE optional header magic: 0x{magic:X}");

        uint exportRva = BitConverter.ToUInt32(nt, dataDirOffset);
        uint exportSize = BitConverter.ToUInt32(nt, dataDirOffset + 4);
        if (exportRva == 0 || exportSize == 0)
            throw new Exception("该模块没有导出表");

        byte[] dir = _proc.ReadRemote(Off(_base, exportRva), 40);
        int numberOfNames = BitConverter.ToInt32(dir, 24);
        int addrFuncs = BitConverter.ToInt32(dir, 28);
        int addrNames = BitConverter.ToInt32(dir, 32);
        int addrOrds = BitConverter.ToInt32(dir, 36);

        byte[] nameTable = _proc.ReadRemote(Off(_base, addrNames), numberOfNames * 4);
        byte[] ordTable = _proc.ReadRemote(Off(_base, addrOrds), numberOfNames * 2);

        for (int i = 0; i < numberOfNames; i++)
        {
            uint nameRva = BitConverter.ToUInt32(nameTable, i * 4);
            string name = ReadRemoteAscii(Off(_base, nameRva), 128);
            ushort ordinal = BitConverter.ToUInt16(ordTable, i * 2);
            byte[] fn = _proc.ReadRemote(Off(_base, (long)addrFuncs + (long)ordinal * 4), 4);
            uint funcRva = BitConverter.ToUInt32(fn, 0);
            _funcs[name] = Off(_base, funcRva);
        }
    }

    private string ReadRemoteAscii(nint addr, int max)
    {
        byte[] buf = _proc.ReadRemote(addr, max);
        int len = Array.IndexOf(buf, (byte)0);
        if (len < 0) len = max;
        return System.Text.Encoding.ASCII.GetString(buf, 0, len);
    }
}
