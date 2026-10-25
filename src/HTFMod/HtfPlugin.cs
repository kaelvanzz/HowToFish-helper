using BepInEx;
using BepInEx.Unity.Mono;
using HarmonyLib;

namespace HTFMod;

[BepInPlugin("htf.cheatmenu", "How to Fish 鱼塘改造器", "1.1.0")]
public class HtfPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        try
        {
            var harmony = new Harmony("htf.cheatmenu");
            int ok = 0, fail = 0;
            foreach (var t in typeof(HtfPlugin).Assembly.GetTypes())
            {
                if (t.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0)
                    continue;
                try
                {
                    harmony.CreateClassProcessor(t).Patch();
                    ok++;
                }
                catch (System.Exception e)
                {
                    fail++;
                    Logger.LogWarning("补丁失败(已跳过,不影响其他功能): " + t.Name + " — " + e.Message);
                }
            }

            // 秒杀:Item.LocalHit 是虚方法且被 Creature/BowheadWhale/DeadPlayer 等重写,
            // 必须把所有子类重写也打上,否则打生物时虚分发绕过基类补丁
            var oneShotPrefix = new HarmonyMethod(typeof(Patch_Item_LocalHit), nameof(Patch_Item_LocalHit.Prefix));
            foreach (var t in typeof(Item).Assembly.GetTypes())
            {
                if (!typeof(Item).IsAssignableFrom(t) || t.IsAbstract)
                    continue;
                var m = AccessTools.DeclaredMethod(t, "LocalHit");
                if (m == null)
                    continue;
                try
                {
                    harmony.Patch(m, prefix: oneShotPrefix);
                    ok++;
                }
                catch (System.Exception e)
                {
                    fail++;
                    Logger.LogWarning("秒杀补丁失败(已跳过): " + t.Name + " — " + e.Message);
                }
            }

            ModCore.Initialize();
            Logger.LogInfo($"HTFMod v1.1 已加载 — 补丁 {ok} 成功 / {fail} 失败,按 Insert 打开菜单");
        }
        catch (System.Exception e)
        {
            Logger.LogError("HTFMod 初始化失败: " + e);
        }
    }
}
