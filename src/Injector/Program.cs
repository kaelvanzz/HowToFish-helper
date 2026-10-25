using System.Text;

namespace HTFInjector;

internal static class Program
{
    private const string Banner = """
        ┌─────────────────────────────────────────────┐
        │  HOW TO FISH · 鱼塘改造器 注入器  v1.0        │
        └─────────────────────────────────────────────┘
        """;

    // x64 stub: rcx = params struct { fn, a0, a1, a2, a3, result }
    private static readonly byte[] StubBytes =
    {
        0x57,                   // push rdi
        0x48, 0x89, 0xCF,       // mov rdi, rcx
        0x48, 0x8B, 0x47, 0x00, // mov rax, [rdi+0x00]  ; func
        0x48, 0x8B, 0x4F, 0x08, // mov rcx, [rdi+0x08]  ; arg0
        0x48, 0x8B, 0x57, 0x10, // mov rdx, [rdi+0x10]  ; arg1
        0x4C, 0x8B, 0x47, 0x18, // mov r8,  [rdi+0x18]  ; arg2
        0x4C, 0x8B, 0x4F, 0x20, // mov r9,  [rdi+0x20]  ; arg3 (REX 4C: B=0, base=rdi)
        0x48, 0x83, 0xEC, 0x20, // sub rsp, 0x20
        0xFF, 0xD0,             // call rax
        0x48, 0x83, 0xC4, 0x20, // add rsp, 0x20
        0x48, 0x89, 0x47, 0x28, // mov [rdi+0x28], rax  ; result
        0x31, 0xC0,             // xor eax, eax
        0x5F,                   // pop rdi
        0xC3,                   // ret
    };

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length > 0 && args[0] == "--test")
            return RunProbe(args.Length > 1 ? args[1] : "How to Fish");

        Console.WriteLine(Banner);
        Console.WriteLine();

        try
        {
            string procName = args.Length > 0 ? args[0] : "How to Fish";
            string modPath = args.Length > 1 ? args[1] : DefaultModPath();

            if (!File.Exists(modPath))
                throw new Exception($"找不到 mod 文件: {modPath}");

            System.Diagnostics.Process proc;
            if (args.Length > 0 && int.TryParse(args[0], out int pid))
            {
                proc = System.Diagnostics.Process.GetProcessById(pid);
            }
            else
            {
                proc = FindProcess(procName);
            }
            Console.WriteLine($"[*] 目标进程 : {proc.ProcessName} (PID {proc.Id})");

            using var target = new TargetProcess(proc.Id, proc.ProcessName);
            Console.WriteLine("[*] 已打开进程句柄");

            var (monoBase, monoSize) = target.FindModule("mono-2.0-bdwgc.dll");
            Console.WriteLine($"[*] mono 模块 : 0x{monoBase:X} ({monoSize / 1024} KB)");

            var exp = new RemoteExports(target, monoBase);
            nint fnRootDomain = exp["mono_get_root_domain"];
            nint fnAttachCoop = exp["mono_jit_thread_attach"];
            nint fnImageFromData = exp["mono_image_open_from_data"];
            nint fnClassFromName = exp["mono_class_from_name"];
            nint fnMethodFromName = exp["mono_class_get_method_from_name"];
            nint fnInvoke = exp["mono_runtime_invoke"];
            Console.WriteLine("[*] mono 导出 : 6 个函数已解析");

            nint stub = target.AllocRemote(StubBytes.Length, Native.PAGE_EXECUTE_READWRITE);
            target.WriteRemote(stub, StubBytes);
            nint param = target.AllocRemote(64);

            ulong Call(nint fn, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0)
            {
                var p = new byte[64];
                BitConverter.GetBytes((ulong)fn).CopyTo(p, 0);
                BitConverter.GetBytes(a0).CopyTo(p, 8);
                BitConverter.GetBytes(a1).CopyTo(p, 16);
                BitConverter.GetBytes(a2).CopyTo(p, 24);
                BitConverter.GetBytes(a3).CopyTo(p, 32);
                return target.CallStub(stub, param, p);
            }

            ulong domain = Call(fnRootDomain);
            if (domain == 0) throw new Exception("mono_get_root_domain 返回空 — 游戏还没初始化 mono?先等主菜单出现再注入");
            Console.WriteLine($"[*] root domain : 0x{domain:X}");

            ulong thread = Call(fnAttachCoop, domain);
            Console.WriteLine($"[D] jit_thread_attach result : 0x{thread:X}");
            if (thread == 0)
                Console.WriteLine("[!] jit_thread_attach 返回空,继续尝试");

            // 从内存加载 mod 程序集 (SharpMonoInjector 方式,绕开文件探测)
            byte[] modBytes = File.ReadAllBytes(modPath);
            nint dataPtr = target.AllocRemote(modBytes.Length);
            target.WriteRemote(dataPtr, modBytes);
            nint statusPtr = target.AllocRemote(8);
            target.WriteRemote(statusPtr, new byte[8]);
            Console.WriteLine($"[*] mod 字节已写入目标: {modBytes.Length} bytes @ 0x{dataPtr:X}");

            ulong image = Call(fnImageFromData, (ulong)dataPtr, (ulong)modBytes.Length, 0, (ulong)statusPtr);
            Console.WriteLine($"[D] image_open_from_data : 0x{image:X}");
            if (image == 0) throw new Exception($"mono_image_open_from_data 失败 (status=0x{ReadU32(target, statusPtr):X})");

            nint namePtr = WriteAscii(target, Path.GetFileName(modPath));
            nint nsPtr = WriteAscii(target, "HTFMod");
            nint clsPtr = WriteAscii(target, "Loader");
            nint metPtr = WriteAscii(target, "Load");

            ulong klass = Call(fnClassFromName, image, (ulong)nsPtr, (ulong)clsPtr);
            Console.WriteLine($"[D] class_from_name : 0x{klass:X}");
            if (klass == 0) throw new Exception("找不到类 HTFMod.Loader");

            ulong method = Call(fnMethodFromName, klass, (ulong)metPtr, unchecked((ulong)(long)-1));
            Console.WriteLine($"[D] method_from_name : 0x{method:X}");
            if (method == 0) throw new Exception("找不到方法 HTFMod.Loader.Load");

            Console.WriteLine("[*] 正在调用入口 HTFMod.Loader.Load() ...");
            Call(fnInvoke, method);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[✓] 注入成功!进游戏按 INSERT 打开菜单。日志: %TEMP%\\htf-mod.log");
            Console.ResetColor();
            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[✗] " + e.Message);
            Console.ResetColor();
            if (Environment.GetEnvironmentVariable("HTF_DEBUG") == "1")
                Console.WriteLine(e.ToString());
            return 1;
        }
    }

    /// <summary>
    /// mono 线程 worker (x64)。rcx = 参数块,在其中顺序调用:
    /// klass = class_from_name(image, ns, cls);
    /// method = method_from_name(klass, "Load", -1);
    /// invoke(method, NULL, NULL, NULL);
    /// </summary>
    private static readonly byte[] WorkerStub =
    {
        0x53,                                   // push rbx
        0x57,                                   // push rdi
        0x41, 0x54,                             // push r12
        0x48, 0x89, 0xCB,                       // mov rbx, rcx
        // klass = class_from_name(image, ns, cls)
        0x48, 0x8B, 0x43, 0x00,                 // mov rax,[rbx+0x00]
        0x48, 0x8B, 0x4B, 0x18,                 // mov rcx,[rbx+0x18]
        0x48, 0x8B, 0x53, 0x20,                 // mov rdx,[rbx+0x20]
        0x4C, 0x8B, 0x43, 0x28,                 // mov r8,[rbx+0x28]
        0x48, 0x83, 0xEC, 0x20,                 // sub rsp,0x20
        0xFF, 0xD0,                             // call rax
        0x48, 0x83, 0xC4, 0x20,                 // add rsp,0x20
        0x48, 0x89, 0x43, 0x38,                 // mov [rbx+0x38],rax
        0x48, 0x85, 0xC0,                       // test rax,rax
        0x74, 0x4B,                             // jz DONE (->117)
        // method = method_from_name(klass, name, -1)
        0x48, 0x8B, 0x43, 0x08,                 // mov rax,[rbx+0x08]
        0x48, 0x8B, 0x4B, 0x38,                 // mov rcx,[rbx+0x38]
        0x48, 0x8B, 0x53, 0x30,                 // mov rdx,[rbx+0x30]
        0x41, 0xB8, 0xFF, 0xFF, 0xFF, 0xFF,     // mov r8d,-1
        0x48, 0x83, 0xEC, 0x20,                 // sub rsp,0x20
        0xFF, 0xD0,                             // call rax
        0x48, 0x83, 0xC4, 0x20,                 // add rsp,0x20
        0x48, 0x89, 0x43, 0x40,                 // mov [rbx+0x40],rax
        0x48, 0x85, 0xC0,                       // test rax,rax
        0x74, 0x26,                             // jz DONE (->117)
        // invoke(method, NULL, NULL, NULL)
        0x48, 0x8B, 0x43, 0x10,                 // mov rax,[rbx+0x10]
        0x48, 0x8B, 0x4B, 0x40,                 // mov rcx,[rbx+0x40]
        0x31, 0xD2,                             // xor edx,edx
        0x45, 0x31, 0xC0,                       // xor r8d,r8d
        0x45, 0x31, 0xC9,                       // xor r9d,r9d
        0x48, 0x83, 0xEC, 0x20,                 // sub rsp,0x20
        0xFF, 0xD0,                             // call rax
        0x48, 0x83, 0xC4, 0x20,                 // add rsp,0x20
        0x48, 0x89, 0x43, 0x48,                 // mov [rbx+0x48],rax
        0x48, 0xC7, 0x43, 0x50, 0x01, 0x00, 0x00, 0x00, // mov qword[rbx+0x50],1
        // DONE:
        0x31, 0xC0,                             // xor eax,eax
        0x5B,                                   // pop rbx
        0x5F,                                   // pop rdi
        0x41, 0x5C,                             // pop r12
        0xC3,                                   // ret
    };

    private static int RunProbe(string procArg)
    {
        try
        {
            System.Diagnostics.Process proc;
            if (int.TryParse(procArg, out int pid))
                proc = System.Diagnostics.Process.GetProcessById(pid);
            else
                proc = FindProcess(procArg);
            using var target = new TargetProcess(proc.Id, proc.ProcessName);
            var (monoBase, _) = target.FindModule("mono-2.0-bdwgc.dll");
            var exp = new RemoteExports(target, monoBase);

            nint stub = target.AllocRemote(StubBytes.Length, Native.PAGE_EXECUTE_READWRITE);
            target.WriteRemote(stub, StubBytes);
            nint param = target.AllocRemote(64);

            ulong Call(nint fn, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0)
            {
                var p = new byte[64];
                BitConverter.GetBytes((ulong)fn).CopyTo(p, 0);
                BitConverter.GetBytes(a0).CopyTo(p, 8);
                BitConverter.GetBytes(a1).CopyTo(p, 16);
                BitConverter.GetBytes(a2).CopyTo(p, 24);
                BitConverter.GetBytes(a3).CopyTo(p, 32);
                return target.CallStub(stub, param, p);
            }

            nint TryGet(string name) { try { return exp[name]; } catch { return nint.Zero; } }

            ulong domain = Call(exp["mono_get_root_domain"]);
            Console.WriteLine($"[diag] root_domain           = 0x{domain:X}  alive={IsAlive(proc.Id)}");

            nint fnDomainGet = TryGet("mono_domain_get");
            if (fnDomainGet != nint.Zero)
            {
                ulong cur = Call(fnDomainGet);
                Console.WriteLine($"[diag] domain_get (pre-attach)= 0x{cur:X}  alive={IsAlive(proc.Id)}");
            }

            nint fnAttach = TryGet("mono_thread_attach");
            if (fnAttach != nint.Zero)
            {
                ulong th = Call(fnAttach, domain);
                Console.WriteLine($"[diag] thread_attach          = 0x{th:X}  alive={IsAlive(proc.Id)}");
            }

            if (fnDomainGet != nint.Zero)
            {
                ulong cur = Call(fnDomainGet);
                Console.WriteLine($"[diag] domain_get (post-attach)= 0x{cur:X}  alive={IsAlive(proc.Id)}");
            }

            byte[] modBytes = File.ReadAllBytes(DefaultModPath());
            nint dataPtr = target.AllocRemote(modBytes.Length);
            target.WriteRemote(dataPtr, modBytes);
            nint statusPtr = target.AllocRemote(8);
            target.WriteRemote(statusPtr, new byte[8]);

            ulong image = Call(exp["mono_image_open_from_data"], (ulong)dataPtr, (ulong)modBytes.Length, 0, (ulong)statusPtr);
            Console.WriteLine($"[diag] image_open_from_data   = 0x{image:X}  alive={IsAlive(proc.Id)}");

            // 读 image 的 name / assembly 指针做轻诊断
            nint fnImageGetName = TryGet("mono_image_get_name");
            if (fnImageGetName != nint.Zero && image != 0)
            {
                ulong namePtr = Call(fnImageGetName, image);
                string name = "";
                if (namePtr != 0)
                {
                    try { name = Encoding.ASCII.GetString(target.ReadRemote((nint)namePtr, 32)); int z = name.IndexOf('\0'); if (z >= 0) name = name[..z]; }
                    catch { }
                }
                Console.WriteLine($"[diag] image_get_name         = \"{name}\"  alive={IsAlive(proc.Id)}");
            }

            nint nsPtr = WriteAscii(target, "HTFMod");
            nint clsPtr = WriteAscii(target, "Loader");
            ulong klass = Call(exp["mono_class_from_name"], image, (ulong)nsPtr, (ulong)clsPtr);
            Console.WriteLine($"[diag] class_from_name        = 0x{klass:X}  alive={IsAlive(proc.Id)}");

            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine("[diag] " + e.Message);
            return 1;
        }
    }

    private static bool IsAlive(int pid)
    {
        try { using var p = System.Diagnostics.Process.GetProcessById(pid); return !p.HasExited; }
        catch { return false; }
    }

    private static string DefaultModPath()
    {
        // <repo>\build\HTFMod.dll  或与 exe 同目录
        string here = AppContext.BaseDirectory;
        string candidate = Path.Combine(here, "HTFMod.dll");
        if (File.Exists(candidate)) return candidate;

        string repoBuild = Path.GetFullPath(Path.Combine(here, "..", "..", "..", "build", "HTFMod.dll"));
        if (File.Exists(repoBuild)) return repoBuild;

        string srcBuild = Path.GetFullPath(Path.Combine(here, "..", "..", "build", "HTFMod.dll"));
        if (File.Exists(srcBuild)) return srcBuild;

        return candidate;
    }

    private static System.Diagnostics.Process FindProcess(string name)
    {
        var procs = System.Diagnostics.Process.GetProcesses()
            .Where(p =>
            {
                try { return p.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase); }
                catch { return false; }
            })
            .ToArray();

        if (procs.Length == 0)
            throw new Exception($"没有找到运行中的 \"{name}\" 进程 — 请先启动游戏并等到出现窗口");

        if (procs.Length > 1)
            throw new Exception($"找到 {procs.Length} 个同名进程,请用 PID 注入: HTFInjector <pid> <mod.dll>");

        return procs[0];
    }

    private static nint WriteAscii(TargetProcess target, string s)
    {
        byte[] data = Encoding.UTF8.GetBytes(s + "\0");
        nint p = target.AllocRemote(data.Length);
        target.WriteRemote(p, data);
        return p;
    }

    private static uint ReadU32(TargetProcess target, nint addr)
    {
        byte[] b = target.ReadRemote(addr, 4);
        return BitConverter.ToUInt32(b, 0);
    }
}
