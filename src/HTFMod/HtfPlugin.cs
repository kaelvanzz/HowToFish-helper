using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using System.Reflection;

namespace HTFMod;

[BepInPlugin("htf.cheatmenu", "How to Fish 鱼塘改造器", "1.1.0")]
public class HtfPlugin : BaseUnityPlugin
{
    private ConfigEntry<string> _lang;

    private void Awake()
    {
        try
        {
            _lang = Config.Bind("General", "Language", "auto",
                "Menu language: auto / zh-hans / zh-hant / en");
            L.SaveHook = key =>
            {
                _lang.Value = key;
                Config.Save();
            };
            L.ApplyKey(_lang.Value);
            Hotkeys.Init(Config);
            Settings.Init(Config);
            Loader.LogSink = m => Logger.LogInfo(m);

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
                    Logger.LogWarning("Patch failed (skipped, other features unaffected): " + t.Name + " — " + e.Message);
                }
            }

            // One-shot: Item.LocalHit is virtual and overridden by Creature/BowheadWhale/DeadPlayer.
            // Virtual dispatch would bypass a base-class-only patch, so patch every override too.
            // (Silent lookup: subclasses without their own override produce no warnings.)
            var oneShotPrefix = new HarmonyMethod(typeof(Patch_Item_LocalHit), nameof(Patch_Item_LocalHit.Prefix));
            foreach (var t in typeof(Item).Assembly.GetTypes())
            {
                if (!typeof(Item).IsAssignableFrom(t) || t.IsAbstract)
                    continue;
                var m = t.GetMethod("LocalHit",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
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
                    Logger.LogWarning("One-shot patch failed (skipped): " + t.Name + " — " + e.Message);
                }
            }

            ModCore.Initialize();
            Logger.LogInfo($"HTFMod v1.1 loaded — patches {ok} ok / {fail} failed, press INSERT for menu");
        }
        catch (System.Exception e)
        {
            Logger.LogError("HTFMod init failed: " + e);
        }
    }
}
