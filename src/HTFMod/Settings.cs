using System.Collections.Generic;
using BepInEx.Configuration;

namespace HTFMod;

/// <summary>
/// Persists all feature toggles and sliders in the BepInEx config (section "Features").
/// Loaded once at startup; saved when a toggle flips, the menu closes, and on a short
/// timer while something is dirty (slider drags). Not persisted: BuiltInCheats — the
/// game-side cheat state can only be applied from inside a running session.
/// </summary>
public static class Settings
{
    private static readonly List<(string Id, ConfigEntry<bool> Entry, System.Func<bool> Get, System.Action<bool> Set)> _bools
        = new();
    private static readonly List<(string Id, ConfigEntry<float> Entry, System.Func<float> Get, System.Action<float> Set)> _floats
        = new();
    private static ConfigFile _cfg;
    private static bool _dirty;
    private static float _nextSave;

    public static void Init(ConfigFile cfg)
    {
        _cfg = cfg;

        B("GodMode", () => Features.GodMode, v => Features.GodMode = v, false);
        B("FullnessLock", () => Features.FullnessLock, v => Features.FullnessLock = v, false);
        B("KeepInventory", () => Features.KeepInventory, v => Features.KeepInventory = v, false);
        B("PlayerFlight", () => Features.PlayerFlight, v => Features.PlayerFlight = v, false);
        B("ThirdPerson", () => Features.ThirdPerson, v => Features.ThirdPerson = v, false);
        B("SelfOneShot", () => Features.SelfOneShot, v => Features.SelfOneShot = v, false);
        B("NoRecoil", () => Features.NoRecoil, v => Features.NoRecoil = v, false);
        B("NoSpread", () => Features.NoSpread, v => Features.NoSpread = v, false);
        B("InfiniteAmmo", () => Features.InfiniteAmmo, v => Features.InfiniteAmmo = v, false);
        B("FastFire", () => Features.FastFire, v => Features.FastFire = v, false);
        B("Aimbot", () => Features.Aimbot, v => Features.Aimbot = v, false);
        B("PlayerSkeleton", () => Features.PlayerSkeleton, v => Features.PlayerSkeleton = v, false);
        B("PlayerHealth", () => Features.PlayerHealth, v => Features.PlayerHealth = v, false);
        F("EspBoxColor", () => Features.EspBoxColor, v => Features.EspBoxColor = (int)v, 15881036f);      // 0xF24C4C
        F("EspSkeletonColor", () => Features.EspSkeletonColor, v => Features.EspSkeletonColor = (int)v, 5040222f); // 0x4CE65E
        F("EspCreatureColor", () => Features.EspCreatureColor, v => Features.EspCreatureColor = (int)v, 13399026f); // 0xCC73F2
        F("EspItemColor", () => Features.EspItemColor, v => Features.EspItemColor = (int)v, 14727260f); // 0xE0B85C
        F("CrosshairColor", () => Features.CrosshairColor, v => Features.CrosshairColor = (int)v, 15922738f); // 0xF2F2F2
        F("EspFillColor", () => Features.EspFillColor, v => Features.EspFillColor = (int)v, 15881036f);      // 0xF24C4C
        F("EspCreatureFillColor", () => Features.EspCreatureFillColor, v => Features.EspCreatureFillColor = (int)v, 13399026f); // 0xCC73F2
        F("EspItemFillColor", () => Features.EspItemFillColor, v => Features.EspItemFillColor = (int)v, 14727260f); // 0xE0B85C
        F("EspOutlineWidth", () => Features.EspOutlineWidth, v => Features.EspOutlineWidth = v, 1f);
        F("PlayerBoxStyle", () => Features.PlayerBoxStyle, v => Features.PlayerBoxStyle = (int)v, 0f);
        F("CreatureEspStyle", () => Features.CreatureEspStyle, v => Features.CreatureEspStyle = (int)v, 0f);
        F("CrosshairStyle", () => Features.CrosshairStyle, v => Features.CrosshairStyle = (int)v, 0f);
        F("CrosshairLen", () => Features.CrosshairLen, v => Features.CrosshairLen = v, 7f);
        F("CrosshairGap", () => Features.CrosshairGap, v => Features.CrosshairGap = v, 5f);
        F("ItemEspDist", () => Features.ItemEspDist, v => Features.ItemEspDist = v, 150f);
        F("SilentAimHeight", () => Features.SilentAimHeight, v => Features.SilentAimHeight = v, 0.45f);
        F("EspBoxAlpha", () => Features.EspBoxAlpha, v => Features.EspBoxAlpha = v, 0.70f);
        F("EspFillAlpha", () => Features.EspFillAlpha, v => Features.EspFillAlpha = v, 0.08f);
        F("CreatureEspAlpha", () => Features.CreatureEspAlpha, v => Features.CreatureEspAlpha = v, 0.70f);
        F("CreatureFillAlpha", () => Features.CreatureFillAlpha, v => Features.CreatureFillAlpha = v, 0.07f);
        F("ItemEspAlpha", () => Features.ItemEspAlpha, v => Features.ItemEspAlpha = v, 0.62f);
        F("ItemFillAlpha", () => Features.ItemFillAlpha, v => Features.ItemFillAlpha = v, 0.14f);
        B("Crosshair", () => Features.Crosshair, v => Features.Crosshair = v, false);
        B("SilentAim", () => Features.SilentAim, v => Features.SilentAim = v, false);
        B("SilentAimPredict", () => Features.SilentAimPredict, v => Features.SilentAimPredict = v, true);
        F("SilentAimPriority", () => Features.SilentAimPriority, v => Features.SilentAimPriority = (int)v, 0f);
        F("SilentAimTarget", () => Features.SilentAimTarget, v => Features.SilentAimTarget = (int)v, 2f);
        B("SilentAimHead", () => Features.SilentAimHead, v => Features.SilentAimHead = v, false);
        B("SilentAimPlayers", () => Features.SilentAimPlayers, v => Features.SilentAimPlayers = v, true);
        B("SilentAimCreatures", () => Features.SilentAimCreatures, v => Features.SilentAimCreatures = v, true);
        F("SilentAimPlayerPoint", () => Features.SilentAimPlayerPoint, v => Features.SilentAimPlayerPoint = (int)v, 0f);
        B("SilentAimBodyIfLethal", () => Features.SilentAimBodyIfLethal, v => Features.SilentAimBodyIfLethal = v, true);
        F("SilentAimAngle", () => Features.SilentAimAngle, v => Features.SilentAimAngle = v, 60f);
        bool saMigrated = false;
        B("SilentAimSplitMigrated", () => saMigrated, v => saMigrated = v, false);
        if (!saMigrated)
        {
            Features.SilentAimPlayers = Features.SilentAimTarget != 1;
            Features.SilentAimCreatures = Features.SilentAimTarget != 0;
            Features.SilentAimPlayerPoint = Features.SilentAimHead ? 1 : 0;
            saMigrated = true;
            MarkDirty();
        }
        B("CreatureEsp", () => Features.CreatureEsp, v => Features.CreatureEsp = v, false);
        B("CreatureLabel", () => Features.CreatureLabel, v => Features.CreatureLabel = v, true);
        B("CreatureFrame", () => Features.CreatureFrame, v => Features.CreatureFrame = v, true);
        B("CreatureFill", () => Features.CreatureFill, v => Features.CreatureFill = v, false);
        B("CreatureSilhouette", () => Features.CreatureSilhouette, v => Features.CreatureSilhouette = v, false);
        F("CreatureEspDist", () => Features.CreatureEspDist, v => Features.CreatureEspDist = v, 150f);
        // one-shot: exclusive style index → independent toggles
        bool migrated = false;
        B("CreatureEspMigrated", () => migrated, v => migrated = v, false);
        if (!migrated)
        {
            int s = Features.CreatureEspStyle;
            Features.CreatureLabel = true;
            Features.CreatureFrame = s == 0 || s == 1;
            Features.CreatureFill = false;
            Features.CreatureSilhouette = s == 2;
            if (s == 3) Features.CreatureFrame = false;
            migrated = true;
            MarkDirty();
        }
        B("ItemEsp", () => Features.ItemEsp, v => Features.ItemEsp = v, false);
        B("ItemLabel", () => Features.ItemLabel, v => Features.ItemLabel = v, true);
        F("LabelSize", () => Features.LabelSize, v => Features.LabelSize = (int)v, 12f);
        F("ItemEspStyle", () => Features.ItemEspStyle, v => Features.ItemEspStyle = (int)v, 0f);
        B("ItemFrame", () => Features.ItemFrame, v => Features.ItemFrame = v, false);
        B("ItemFill", () => Features.ItemFill, v => Features.ItemFill = v, false);
        if (Features.ItemEspStyle >= 1) Features.ItemFrame = true;
        B("PlayerEsp", () => Features.PlayerEsp, v => Features.PlayerEsp = v, false);
        B("PlayerLabel", () => Features.PlayerLabel, v => Features.PlayerLabel = v, true);
        B("BoatFlight", () => Features.BoatFlight, v => Features.BoatFlight = v, false);
        B("IslandLabels", () => Features.IslandLabels, v => Features.IslandLabels = v, false);
        B("DripSpawn", () => Features.DripSpawn, v => Features.DripSpawn = v, false);
        B("SlotPredict", () => Features.SlotPredict, v => Features.SlotPredict = v, false);
        B("BaitFish", () => Features.BaitFish, v => Features.BaitFish = v, false);
        B("BaitInstant", () => Features.BaitInstant, v => Features.BaitInstant = v, false);
        F("SlotCheatRarity", () => Features.SlotCheatRarity, v => Features.SlotCheatRarity = (int)v, 0f);
        F("SlotSkinItemId", () => Features.SlotSkinItemId, v => Features.SlotSkinItemId = (int)v, -1f);
        F("SlotSkinIndex", () => Features.SlotSkinIndex, v => Features.SlotSkinIndex = (int)v, 0f);
        F("TpIsland", () => Features.TpIsland, v => Features.TpIsland = v, 1f);

        F("SpeedMult", () => Features.SpeedMult, v => Features.SpeedMult = v, 1f);
        F("JumpMult", () => Features.JumpMult, v => Features.JumpMult = v, 1f);
        F("BoatMult", () => Features.BoatMult, v => Features.BoatMult = v, 1f);
        F("FireRateMult", () => Features.FireRateMult, v => Features.FireRateMult = v, 1f);
        F("FireMode", () => Features.FireMode, v => Features.FireMode = (int)v, 1f);
        F("GiantWeight", () => Features.GiantWeight, v => Features.GiantWeight = v, 10f);
        F("AimbotStrength", () => Features.AimbotStrength, v => Features.AimbotStrength = v, 0.5f);
        F("AimbotMaxAngle", () => Features.AimbotMaxAngle, v => Features.AimbotMaxAngle = v, 30f);
        F("ThirdPersonDist", () => Features.ThirdPersonDist, v => Features.ThirdPersonDist = v, 3f);
        F("FovMult", () => Features.FovMult, v => Features.FovMult = v, 1f);
    }

    private static void B(string id, System.Func<bool> get, System.Action<bool> set, bool def)
    {
        var e = _cfg.Bind("Features", id, def, "Saved state: " + id);
        set(e.Value);
        _bools.Add((id, e, get, set));
    }

    private static void F(string id, System.Func<float> get, System.Action<float> set, float def)
    {
        var e = _cfg.Bind("Features", id, def, "Saved state: " + id);
        set(e.Value);
        _floats.Add((id, e, get, set));
    }

    /// <summary>Something changed — will be written to disk shortly.</summary>
    public static void MarkDirty()
    {
        _dirty = true;
    }

    /// <summary>Write all current feature values into the config file.</summary>
    public static void SaveNow()
    {
        if (_cfg == null)
            return;
        try
        {
            foreach (var it in _bools)
                it.Entry.Value = it.Get();
            foreach (var it in _floats)
                it.Entry.Value = it.Get();
            _cfg.Save();
        }
        catch
        {
        }
        _dirty = false;
    }

    /// <summary>Called every frame from CheatController: periodic flush while dirty.</summary>
    public static void Tick()
    {
        if (!_dirty)
            return;
        if (UnityEngine.Time.unscaledTime < _nextSave)
            return;
        _nextSave = UnityEngine.Time.unscaledTime + 2f;
        SaveNow();
    }
}
