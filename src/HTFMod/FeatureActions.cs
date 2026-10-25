namespace HTFMod;

using HarmonyLib;

/// <summary>
/// Single source of truth for every toggle in the menu. Menu rows AND bound
/// hotkeys both go through Apply, so a hotkey press behaves exactly like
/// clicking the switch (including the host-bridge calls).
/// </summary>
public static class FeatureActions
{
    public static readonly string[] All =
    {
        "GodMode", "FullnessLock", "KeepInventory", "PlayerFlight", "ThirdPerson",
        "SelfOneShot", "BuiltInCheats",
        "NoRecoil", "NoSpread", "InfiniteAmmo", "FastFire", "Aimbot", "PlayerEsp",
        "PlayerSkeleton", "PlayerHealth", "Crosshair",
        "BoatFlight", "IslandLabels", "DripSpawn",
    };

    public static bool Get(string id)
    {
        switch (id)
        {
            case "GodMode": return Features.GodMode;
            case "FullnessLock": return Features.FullnessLock;
            case "KeepInventory": return Features.KeepInventory;
            case "PlayerFlight": return Features.PlayerFlight;
            case "ThirdPerson": return Features.ThirdPerson;
            case "SelfOneShot": return Features.SelfOneShot;
            case "BuiltInCheats": return Features.BuiltInCheats;
            case "NoRecoil": return Features.NoRecoil;
            case "NoSpread": return Features.NoSpread;
            case "InfiniteAmmo": return Features.InfiniteAmmo;
            case "FastFire": return Features.FastFire;
            case "Aimbot": return Features.Aimbot;
            case "PlayerEsp": return Features.PlayerEsp;
            case "PlayerSkeleton": return Features.PlayerSkeleton;
            case "PlayerHealth": return Features.PlayerHealth;
            case "Crosshair": return Features.Crosshair;
            case "BoatFlight": return Features.BoatFlight;
            case "IslandLabels": return Features.IslandLabels;
            case "DripSpawn": return Features.DripSpawn;
            default: return false;
        }
    }

    public static void Apply(string id, bool v)
    {
        Settings.MarkDirty();
        switch (id)
        {
            case "GodMode":
                Features.GodMode = v;
                if (!Features.IsHost)
                    Features.HostAct(CheatAction.GodSelf, flag: v);
                break;

            case "FullnessLock":
                if (Features.IsHost)
                    Features.FullnessLock = v;
                else if (Features.HostAct(CheatAction.VitalsLock, flag: v))
                    Features.FullnessLock = v;
                break;

            case "KeepInventory":
                Features.KeepInventory = v;
                if (!Features.IsHost)
                    Features.HostAct(CheatAction.KeepInventorySelf, flag: v);
                break;

            case "PlayerFlight":
                Features.PlayerFlight = v;
                if (!v)
                {
                    // Kill leftover flight momentum: the game's Move() preserves the
                    // current vertical velocity, so the last ascend/descend write
                    // (+/-10 m/s) would launch the player skyward on disable.
                    try
                    {
                        var p = Player.LocalPlayer;
                        var rb = p != null ? p.Rigidbody : null;
                        if (rb != null && !rb.isKinematic)
                        {
                            var vel = rb.linearVelocity;
                            vel.y = 0f;
                            rb.linearVelocity = vel;
                        }
                        if (p?.Movement != null)
                        {
                            var fj = AccessTools.Field(typeof(PlayerMovement), "_jumpForce");
                            Loader.Log($"Flight disabled — jumpForce={fj?.GetValue(p.Movement)}, SpeedMult={Features.SpeedMult}, JumpMult={Features.JumpMult}");
                        }
                    }
                    catch { }
                }
                break;

            case "ThirdPerson":
                Features.ThirdPerson = v;
                Features.SetLocalBodyVisible(v);
                break;

            case "SelfOneShot":
                Features.SelfOneShot = v;
                break;

            case "BuiltInCheats":
                if (Features.HostAct(CheatAction.BuiltInCheats, flag: v))
                    Features.BuiltInCheats = v;
                break;

            case "NoRecoil": Features.NoRecoil = v; break;
            case "NoSpread": Features.NoSpread = v; break;
            case "InfiniteAmmo": Features.InfiniteAmmo = v; break;
            case "FastFire": Features.FastFire = v; break;
            case "Aimbot": Features.Aimbot = v; break;
            case "PlayerEsp": Features.PlayerEsp = v; break;
            case "PlayerSkeleton": Features.PlayerSkeleton = v; break;
            case "PlayerHealth": Features.PlayerHealth = v; break;
            case "Crosshair": Features.Crosshair = v; break;
            case "BoatFlight": Features.BoatFlight = v; break;
            case "IslandLabels": Features.IslandLabels = v; break;
            case "DripSpawn": Features.DripSpawn = v; break;
        }
    }
}
