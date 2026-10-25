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
        B("Crosshair", () => Features.Crosshair, v => Features.Crosshair = v, false);
        B("PlayerEsp", () => Features.PlayerEsp, v => Features.PlayerEsp = v, false);
        B("BoatFlight", () => Features.BoatFlight, v => Features.BoatFlight = v, false);
        B("IslandLabels", () => Features.IslandLabels, v => Features.IslandLabels = v, false);
        B("DripSpawn", () => Features.DripSpawn, v => Features.DripSpawn = v, false);

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
