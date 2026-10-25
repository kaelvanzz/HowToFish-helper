using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace HTFMod;

/// <summary>
/// 注入入口。在远程线程上执行,绝不触碰 Unity API ——
/// 通过 UnityEngine.Awaitable 的私有静态同步上下文把初始化投递到 Unity 主线程。
/// </summary>
public static class Loader
{
    public const string Namespace = "HTFMod";
    public const string ClassName = "Loader";
    public const string MethodName = "Load";
    public const string Version = "1.0";

    public static volatile bool Ok;
    private static volatile string _error;

    public static void Load()
    {
        var watcher = new Thread(PollForMainThread) { IsBackground = true, Name = "HTFMod.Loader" };
        watcher.Start();

        // 注入器会等这个方法返回;给它最多 20s 判定结果
        var sw = Stopwatch.StartNew();
        while (!Ok && _error == null && sw.ElapsedMilliseconds < 20000)
            Thread.Sleep(100);

        if (_error != null)
            throw new InvalidOperationException("HTFMod 加载失败: " + _error);
    }

    private static void PollForMainThread()
    {
        try
        {
            for (int i = 0; i < 120; i++) // 最多等 60s
            {
                var ctx = FindUnitySyncContext();
                if (ctx != null)
                {
                    ctx.Post(_ => Boot(), null);
                    // Boot 在主线程跑完才置 Ok;继续等,错误也会被捕获
                    var sw = Stopwatch.StartNew();
                    while (!Ok && _error == null && sw.ElapsedMilliseconds < 30000)
                        Thread.Sleep(100);
                    return;
                }
                Thread.Sleep(500);
            }
            _error = "60s 内未找到 Unity 主线程同步上下文";
        }
        catch (Exception e)
        {
            _error = e.Message;
            Log("PollForMainThread 异常: " + e);
        }
    }

    private static SynchronizationContext FindUnitySyncContext()
    {
        // Unity 6: UnitySynchronizationContext 实例存在 Awaitable._synchronizationContext (private static)
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            AssemblyName an;
            try { an = asm.GetName(); } catch { continue; }
            if (!an.Name.StartsWith("UnityEngine", StringComparison.Ordinal))
                continue;

            Type t = null;
            try { t = asm.GetType("UnityEngine.Awaitable", false); } catch { }
            if (t == null) continue;

            var f = t.GetField("_synchronizationContext", BindingFlags.NonPublic | BindingFlags.Static);
            if (f != null)
                return f.GetValue(null) as SynchronizationContext;
        }
        return null;
    }

    private static void Boot()
    {
        try
        {
            ModCore.Initialize();
            Ok = true;
            Log("HTFMod v" + Version + " 已就绪 (主线程)");
        }
        catch (Exception e)
        {
            _error = e.Message;
            Log("Boot 异常: " + e);
        }
    }

    public static void Log(string msg)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "htf-mod.log"),
                $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
        }
        catch { /* 忽略日志失败 */ }
    }
}
