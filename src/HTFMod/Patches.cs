using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HTFMod;

/// <summary>No recoil: swallow view-space recoil kicks (camera).</summary>
[HarmonyPatch(typeof(PlayerCamera), "Recoil")]
internal static class Patch_PlayerCamera_Recoil
{
    private static bool Prefix() => !Features.NoRecoil;
}

/// <summary>No recoil: swallow gun-model recoil kicks.</summary>
[HarmonyPatch(typeof(Weapon), "AddModelRecoil")]
internal static class Patch_Weapon_AddModelRecoil
{
    private static bool Prefix() => !Features.NoRecoil;
}

/// <summary>No recoil: swallow recoil impulses on the held-tool rigidbody.</summary>
[HarmonyPatch(typeof(PlayerToolMovement), "Recoil")]
internal static class Patch_PlayerToolMovement_Recoil
{
    private static bool Prefix() => !Features.NoRecoil;
}

/// <summary>
/// No recoil: freeze the jump/fall sway of the gun model (Fall drives the vertical swing
/// while airborne). The original method is skipped and the sway offset zeroed.
/// </summary>
[HarmonyPatch(typeof(PlayerToolMovement), "Fall")]
internal static class Patch_PlayerToolMovement_Fall
{
    private static readonly FieldInfo FiFallPos = AccessTools.Field(typeof(PlayerToolMovement), "_fallPos");

    private static bool Prefix(PlayerToolMovement __instance)
    {
        if (!Features.NoRecoil)
            return true;
        try { FiFallPos?.SetValue(__instance, Vector3.zero); }
        catch { }
        return false; // skip original: no airborne swing
    }
}

/// <summary>
/// Player flight: takes over the vertical velocity component right after the game's
/// own movement code writes it (Move is the method the game uses).
/// Space up / Ctrl down / release to hover; automatically yields while driving the boat.
/// Also zeroes the accumulated gravity term, otherwise the next Move call would apply
/// one gravity step before we correct, causing a slow sink.
/// </summary>
[HarmonyPatch(typeof(PlayerMovement), "Move")]
internal static class Patch_PlayerMovement_Move
{
    private static readonly FieldInfo FiExtraGravity = AccessTools.Field(typeof(PlayerMovement), "_extraGravityForce");

    private static void Postfix(PlayerMovement __instance)
    {
        if (!Features.PlayerFlight)
            return;
        try
        {
            var p = Player.LocalPlayer;
            if (p == null || __instance != p.Movement)
                return;
            if (BoatManager.Boat != null && Boat.IsDrivingLocally)
                return; // driving: yield to boat flight

            FiExtraGravity?.SetValue(__instance, 0f); // cancel next-frame gravity accumulation

            var rb = p.Rigidbody;
            if (rb == null || rb.isKinematic)
                return;

            Vector3 v = rb.linearVelocity;
            if (InputX.KeyHeld(Hotkeys.Key(Hotkeys.PlayerFlightUp)))
                v.y = 10f;
            else if (InputX.KeyHeld(Hotkeys.Key(Hotkeys.PlayerFlightDown)))
                v.y = -10f;
            else
                v.y = 0f;
            rb.linearVelocity = v;
        }
        catch { }
    }
}

/// <summary>
/// Skip reload entirely for the local player while Infinite Ammo is on: the game's
/// LocalReload (R key or auto-reload on empty mag) is skipped and the queued-reload
/// flag cleared — the magazine is always full, so firing continues without interruption.
/// </summary>
[HarmonyPatch(typeof(Weapon), "LocalReload")]
internal static class Patch_Weapon_LocalReload
{
    private static readonly FieldInfo FiQueue = AccessTools.Field(typeof(Weapon), "_queueReload");

    private static bool Prefix(Weapon __instance)
    {
        if (!Features.InfiniteAmmo)
            return true;
        try
        {
            var holder = __instance.Holder;
            if (holder == null || holder != Player.LocalPlayer)
                return true;
            FiQueue?.SetValue(__instance, false);
            return false;
        }
        catch
        {
            return true;
        }
    }
}

/// <summary>
/// Fire mode "hold+click": every click fires instantly even while the previous
/// cooldown is still running (hold already sprays via forced full-auto).
/// </summary>
[HarmonyPatch(typeof(Weapon), "PrimaryInput")]
internal static class Patch_Weapon_PrimaryInput
{
    private static readonly FieldInfo FiCool = AccessTools.Field(typeof(Weapon), "_hasCoolDown");

    private static void Prefix(Weapon __instance)
    {
        if (Features.FireMode != 2)
            return;
        try
        {
            var holder = __instance.Holder;
            if (holder == null || holder != Player.LocalPlayer)
                return;
            FiCool?.SetValue(__instance, false); // the click always shoots right now
        }
        catch { }
    }
}

/// <summary>
/// Slot machine control (host): RollRandom is where the 10 slots and the winning
/// index are rolled. The game has its own cheat hook (SetCheatSkin) — we pick an
/// item+skin of the chosen rarity from the machine's available items and force it.
/// </summary>
[HarmonyPatch(typeof(SlotMachineManager), "RollRandom")]
internal static class Patch_SlotMachine_RollRandom
{
    private static void Prefix()
    {
        // 1) exact skin pick wins over everything
        if (Features.SlotSkinItemId >= 0)
        {
            try
            {
                var it = GameInfo.IDToItem((byte)Features.SlotSkinItemId);
                if (it != null && it.SkinPreset != null && Features.SlotSkinIndex < it.SkinPreset.Skins.Count)
                {
                    SlotMachineManager.SetCheatSkin(it, (byte)Features.SlotSkinIndex);
                    return;
                }
            }
            catch { }
        }

        // 2) rarity mode: random item+skin of the chosen rarity from the prize pool
        int mode = Features.SlotCheatRarity; // 0 off / 1 rare / 2 legendary
        if (mode == 0)
        {
            SlotMachineManager.SetCheatSkin(null, byte.MaxValue); // reset the game's cheat hook
            return;
        }
        try
        {
            var items = SlotMachine.AvailableItems;
            if (items == null)
                return;
            var want = mode == 2 ? Rarity.Legendary : Rarity.Rare;
            var candidates = new List<(Item item, byte skin)>();
            foreach (var it in items)
            {
                if (it == null || it.SkinPreset == null)
                    continue;
                var skins = it.SkinPreset.Skins;
                for (byte i = 0; i < skins.Count; i++)
                    if (skins[i].Rarity == want)
                        candidates.Add((it, i));
            }
            if (candidates.Count == 0)
            {
                SlotMachineManager.SetCheatSkin(null, byte.MaxValue);
                return;
            }
            var pick = candidates[Random.Range(0, candidates.Count)];
            SlotMachineManager.SetCheatSkin(pick.item, pick.skin);
        }
        catch
        {
            SlotMachineManager.SetCheatSkin(null, byte.MaxValue);
        }
    }
}

/// <summary>Slot machine prediction: Roll runs on every client (broadcast) with the
/// final result — capture it the moment it arrives, long before the reels stop.</summary>
[HarmonyPatch(typeof(SlotMachine), "Roll")]
internal static class Patch_SlotMachine_Roll
{
    private static void Postfix(Player roller, byte[] itemIDs, byte[] itemSkins, byte rolled)
    {
        SlotsPrediction.OnRoll(roller, itemIDs, itemSkins, rolled);
    }
}

/// <summary>Last slot-machine roll, shown by the prediction HUD.</summary>
public static class SlotsPrediction
{
    public static string ResultText;
    public static string RollerName;
    public static float Until;

    public static void OnRoll(Player roller, byte[] itemIDs, byte[] itemSkins, byte rolled)
    {
        try
        {
            if (itemIDs == null || itemSkins == null || rolled >= itemIDs.Length)
                return;

            byte id = itemIDs[rolled];
            Item src = id != byte.MaxValue ? GameInfo.IDToItem(id) : null;
            string item = src != null ? ItemDb.NameOf(src) : L.T("船皮肤", "船皮膚", "Boat skin");

            string rarity = "";
            SkinPreset preset = src != null ? src.SkinPreset
                : (BoatManager.Boat != null ? BoatManager.Boat.SkinPreset : null);
            byte skinIdx = itemSkins[rolled];
            if (preset != null && skinIdx < preset.Skins.Count)
            {
                var r = preset.Skins[skinIdx].Rarity;
                rarity = r == Rarity.Legendary ? L.T(" · 传说", " · 傳說", " · Legendary")
                       : r == Rarity.Rare ? L.T(" · 稀有", " · 稀有", " · Rare") : "";
            }

            RollerName = roller != null && roller.SteamName != null ? roller.SteamName : "?";
            ResultText = item + rarity;
            Until = Time.unscaledTime + 6f;
        }
        catch { }
    }
}

/// <summary>
/// Silent aim: the local player's bullets are redirected to the cone target
/// (chest line) the instant they leave the muzzle — the camera never moves.
/// </summary>
[HarmonyPatch(typeof(ProjectileManager), "AddProjectile")]
internal static class Patch_SilentAim_Projectile
{
    private static bool _saLogged;

    private static void Prefix(Player owner, WeaponInfo weaponInfo, bool isLocal, Vector3 pos, ref Vector3 velocity)
    {
        if (!Features.SilentAim || !isLocal)
            return;
        if (owner == null || owner != Player.LocalPlayer)
            return;

        float speed = velocity.magnitude;
        if (speed < 0.01f)
            return;
        float gravity = 0f;
        try { if (weaponInfo != null) gravity = weaponInfo.ProjectileGravity; } catch { }

        if (!Combat.TrySilentAimDir(pos, speed, gravity, out var dir))
            return;
        velocity = dir.normalized * speed;

        if (!_saLogged)
        {
            _saLogged = true;
            Loader.Log("SilentAim: projectile redirected toward the cone target");
        }
    }
}

/// <summary>Silent aim for multi-pellet weapons: every pellet converges on the target.</summary>
[HarmonyPatch(typeof(ProjectileManager), "AddProjectiles")]
internal static class Patch_SilentAim_Projectiles
{
    private static void Prefix(Player owner, WeaponInfo weaponInfo, bool isLocal, Vector3 pos, Vector3[] velocities)
    {
        if (!Features.SilentAim || !isLocal || velocities == null || velocities.Length == 0)
            return;
        if (owner == null || owner != Player.LocalPlayer)
            return;

        // Preserve the pellet spread: rotate the whole pellet fan onto the target
        // instead of collapsing every pellet onto one line.
        Vector3 origCenter = Vector3.zero;
        float avgSpeed = 0f;
        for (int i = 0; i < velocities.Length; i++)
        {
            origCenter += velocities[i];
            avgSpeed += velocities[i].magnitude;
        }
        if (origCenter.sqrMagnitude < 0.01f)
            return;
        origCenter.Normalize();
        avgSpeed = Mathf.Max(0.01f, avgSpeed / velocities.Length);

        float gravity = 0f;
        try { if (weaponInfo != null) gravity = weaponInfo.ProjectileGravity; } catch { }
        if (!Combat.TrySilentAimDir(pos, avgSpeed, gravity, out var dir))
            return;

        var q = Quaternion.FromToRotation(origCenter, dir.normalized);
        for (int i = 0; i < velocities.Length; i++)
        {
            float speed = velocities[i].magnitude;
            if (speed > 0.01f)
                velocities[i] = (q * velocities[i].normalized) * speed;
        }
    }
}

/// <summary>FOV multiplier: scales the camera FOV after the game sets it every frame.</summary>
[HarmonyPatch(typeof(PlayerCamera), "SetFov")]
internal static class Patch_PlayerCamera_SetFov
{
    private static void Postfix()
    {
        float m = Features.FovMult;
        if (m > 0.99f && m < 1.01f)
            return;
        try
        {
            var cam = GameInfo.CurCamera;
            if (cam != null)
                cam.fieldOfView = Mathf.Clamp(cam.fieldOfView * m, 20f, 150f);
        }
        catch { }
    }
}

/// <summary>
/// PlayerCamera.SetCamPosRot is where the game copies _rot onto the camera transform.
/// - Prefix (aimbot): the pull is already in _rot when the camera applies it —
///   immune to script execution order and visible the same frame.
/// - Postfix (third person): the game places the camera at the player's head;
///   afterwards we pull it back along the view direction by the configured distance.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "SetCamPosRot")]
internal static class Patch_PlayerCamera_SetCamPosRot
{
    private static void Prefix(PlayerCamera __instance)
    {
        if (Combat.AimActive)
            Combat.ApplyAim(__instance);
    }

    private static void Postfix(PlayerCamera __instance)
    {
        if (!Features.ThirdPerson || Features.ThirdPersonDist <= 0.05f)
            return;
        try
        {
            var t = __instance.CamTransform;
            if (t != null)
                t.position -= t.forward * Features.ThirdPersonDist;
        }
        catch { }
    }
}

/// <summary>
/// God mode (self only): intercepts the master damage entry.
/// Fire/poison/drown/starvation/attack damage all funnel through TakeDamage,
/// which is also where the game's own god-mode check lives.
/// - On this machine: the local toggle exempts the local player.
/// - Online bridge: registered players are exempted on the host.
/// </summary>
[HarmonyPatch(typeof(PlayerVitals), "TakeDamage")]
internal static class Patch_PlayerVitals_TakeDamage
{
    private static bool Prefix(PlayerVitals __instance)
    {
        if (!Features.GodMode && !Multiplayer.HasGodProtected)
            return true;
        try
        {
            var owner = __instance.Owner;
            if (owner == null)
                return true;
            if (owner.IsLocalClient && Features.GodMode)
                return false;
            if (Multiplayer.IsGodProtected(owner))
                return false;
        }
        catch { }
        return true;
    }
}

/// <summary>
/// God mode (supplement): attack-hit entry also intercepted, which additionally
/// removes hit sounds / kill-score noise when god mode is on.
/// </summary>
[HarmonyPatch(typeof(PlayerVitals), "LocalHit")]
internal static class Patch_PlayerVitals_LocalHit
{
    private static bool Prefix(PlayerVitals __instance)
    {
        if (!Features.GodMode && !Multiplayer.HasGodProtected)
            return true;
        try
        {
            var owner = __instance.Owner;
            if (owner == null)
                return true;
            if (owner.IsLocalClient && Features.GodMode)
                return false;
            if (Multiplayer.IsGodProtected(owner))
                return false;
        }
        catch { }
        return true;
    }
}

/// <summary>
/// Keep inventory: skip the full-inventory drop on death/respawn.
/// The local player's drops follow the local toggle; players registered through
/// the online bridge are exempted on the host side.
/// </summary>
[HarmonyPatch(typeof(PlayerInventory), "ServerDropAll")]
internal static class Patch_PlayerInventory_ServerDropAll
{
    private static bool Prefix(PlayerInventory __instance)
    {
        try
        {
            var owner = __instance.Owner;
            if (owner == null)
                return true;
            if (owner.IsLocalClient)
                return !Features.KeepInventory;
            return !Multiplayer.IsKeepInvProtected(owner);
        }
        catch { return true; }
    }
}

/// <summary>
/// One-shot (works for every weapon): both LocalHit methods are the funnel where all
/// damage passes with an "attacker" parameter. Guns/projectiles/melee/fists all end up
/// here, so when the attacker is the local player the damage is set to a fixed high
/// value (base-3 fists included). Other players' attacks are untouched.
/// Note: Item.LocalHit is virtual and overridden by Creature/BowheadWhale/DeadPlayer —
/// those overrides are enumerated and patched in HtfPlugin.Awake.
/// </summary>
internal static class Patch_Item_LocalHit
{
    public static void Prefix(Item __instance, Player player, ref int damage)
    {
        if (!Features.SelfOneShot || damage <= 0)
            return;
        if (player == null || player != Player.LocalPlayer)
            return;
        damage = 100000; // fixed high damage: even 3-damage fists one-shot late bosses
    }
}

/// <summary>
/// Force-crit for creatures: rewrite the hit point just past HeadPos so the
/// vanilla headshot multiplier (and headshot killscore) always fires.
/// </summary>
[HarmonyPatch(typeof(Creature), "LocalHit")]
internal static class Patch_Creature_ForceCrit
{
    private static void Prefix(Creature __instance, ref Vector3 point, bool rangedHit, Player player)
    {
        if (!Features.ForceCrit || !rangedHit)
            return;
        if (player == null || player != Player.LocalPlayer)
            return;
        try
        {
            var t = __instance.transform;
            Vector3 local = t.InverseTransformPoint(point);
            if (local.z <= __instance.HeadPos)
            {
                local.z = __instance.HeadPos + 0.08f;
                point = t.TransformPoint(local);
            }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(PlayerVitals), "LocalHit")]
internal static class Patch_PlayerVitals_LocalHit_Damage
{
    private static void Prefix(PlayerVitals __instance, Player playerWhoHit, ref int damage)
    {
        if (!Features.SelfOneShot || damage <= 0)
            return;
        if (playerWhoHit == null || playerWhoHit != Player.LocalPlayer)
            return;
        damage = 100000; // fixed high damage
    }
}
