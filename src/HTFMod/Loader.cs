using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace HTFMod;

/// <summary>
/// CLI-injector entry point. Runs on a remote thread and never touches Unity APIs —
/// initialization is posted to the Unity main thread through the private sync context
/// of UnityEngine.Awaitable. (Legacy path: the working injection is BepInEx/doorstop.)
/// </summary>
public static class Loader
{
    public const string Namespace = "HTFMod";
    public const string ClassName = "Loader";
    public const string MethodName = "Load";
    public const string Version = "1.1";

    public static volatile bool Ok;
    private static volatile string _error;

    public static void Load()
    {
        var watcher = new Thread(PollForMainThread) { IsBackground = true, Name = "HTFMod.Loader" };
        watcher.Start();

        // The injector waits for this method to return; allow up to 20s to settle.
        var sw = Stopwatch.StartNew();
        while (!Ok && _error == null && sw.ElapsedMilliseconds < 20000)
            Thread.Sleep(100);

        if (_error != null)
            throw new InvalidOperationException("HTFMod load failed: " + _error);
    }

    private static void PollForMainThread()
    {
        try
        {
            for (int i = 0; i < 120; i++) // up to 60s
            {
                var ctx = FindUnitySyncContext();
                if (ctx != null)
                {
                    ctx.Post(_ => Boot(), null);
                    // Wait until Boot finished on the main thread; errors are captured too.
                    var sw = Stopwatch.StartNew();
                    while (!Ok && _error == null && sw.ElapsedMilliseconds < 30000)
                        Thread.Sleep(100);
                    return;
                }
                Thread.Sleep(500);
            }
            _error = "Unity main-thread sync context not found within 60s";
        }
        catch (Exception e)
        {
            _error = e.Message;
            Log("PollForMainThread exception: " + e);
        }
    }

    private static SynchronizationContext FindUnitySyncContext()
    {
        // Unity 6: the UnitySynchronizationContext instance lives in
        // Awaitable._synchronizationContext (private static).
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
            Log("HTFMod v" + Version + " ready (main thread)");
        }
        catch (Exception e)
        {
            _error = e.Message;
            Log("Boot exception: " + e);
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
        catch { /* ignore logging failures */ }
    }
}
