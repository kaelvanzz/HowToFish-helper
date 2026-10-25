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
