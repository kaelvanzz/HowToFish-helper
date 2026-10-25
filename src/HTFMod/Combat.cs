using System;
using System.Reflection;
using UnityEngine;

namespace HTFMod;

/// <summary>
/// Combat helpers:
/// - No recoil: Harmony blocks view/gun-model recoil (see Patches.cs).
/// - No spread: zeroes the held weapon's spread angle every frame.
/// - Infinite ammo: keeps the held weapon's magazine full.
/// - Aim players: while holding RMB with a gun equipped, locks onto the player
///   closest to the crosshair and tracks until it dies or leaves the cone.
/// - Player ESP: boxes + distance for other players through walls (drawn in MenuUi).
/// </summary>
public static class Combat
{
    private static FieldInfo _fiRot;    // PlayerCamera._rot  (Vector3: x=pitch, y=yaw)
    private static FieldInfo _fiSpread; // Weapon._spread     (float)
    private static PropertyInfo _piAmmo; // Weapon.Ammo       (int, private setter)
    private static bool _initFailed;

    private static Player _lockedPlayer;

    public static void Tick(bool menuOpen)
    {
        var p = Player.LocalPlayer;
        if (p == null)
        {
            _lockedPlayer = null;
            return;
        }

        Init();
        if (_initFailed)
            return;

        if (Features.NoSpread || Features.InfiniteAmmo)
        {
            if (_fiSpread != null || _piAmmo != null)
                TickWeapon(p);
        }

        if (Features.Aimbot)
        {
            if (_fiRot != null)
                TickAimbot(p, menuOpen);
            else
                _lockedPlayer = null;
        }
        else
            _lockedPlayer = null;
    }

    private static void Init()
    {
        if (_initFailed)
            return;
        if (_fiRot != null && _fiSpread != null && _piAmmo != null)
            return;
        try
        {
            _fiRot ??= typeof(PlayerCamera).GetField("_rot", BindingFlags.NonPublic | BindingFlags.Instance);
            _fiSpread ??= typeof(Weapon).GetField("_spread", BindingFlags.NonPublic | BindingFlags.Instance);
            _piAmmo ??= typeof(Weapon).GetProperty("Ammo");
            if (_fiRot == null && _fiSpread == null && _piAmmo == null)
                _initFailed = true;
        }
        catch
        {
            _initFailed = true;
        }
    }

    // ────────────────────── No spread / infinite ammo ──────────────────────

    private static void TickWeapon(Player p)
    {
        try
        {
            var held = p.Holding != null ? p.Holding.HeldItem : null;
            var w = held != null ? held.Weapon : null;
            if (w == null)
                return;

            if (Features.NoSpread && _fiSpread != null)
                _fiSpread.SetValue(w, 0f);

            if (Features.InfiniteAmmo && _piAmmo != null)
            {
                var att = w.Attachments;
                int max = att != null ? att.AmmoPerMag : 0;
                if (max > 0)
                {
                    int cur = (int)_piAmmo.GetValue(w);
                    if (cur != max)
                        _piAmmo.SetValue(w, max); // magazine always full
                }
            }
        }
        catch { /* object vanishing between equip/unequip frames, ignore */ }
    }

    // ────────────────────── Aim players ──────────────────────

    private static void TickAimbot(Player p, bool menuOpen)
    {
        // Activation: RMB held + holding a gun + mouse locked (not in menu/inventory/pause)
        if (menuOpen || !InputX.MouseRightHeld() || !HoldingGun(p))
        {
            _lockedPlayer = null;
            return;
        }
        var pc = p.Camera;
        if (pc == null || !pc.MouseLocked)
        {
            _lockedPlayer = null;
            return;
        }
        var cam = GameInfo.CurCamera;
        if (cam == null)
            return;

        Vector3 camPos = cam.transform.position;
        Vector3 forward = cam.transform.forward;
        float maxAngle = Features.AimbotMaxAngle;

        // Sticky lock: keep tracking until the target dies or leaves the cone
        if (_lockedPlayer != null && _lockedPlayer != Player.LocalPlayer && !IsDead(_lockedPlayer))
        {
            if (GetPlayerBounds(_lockedPlayer, out var b) && LockValid(b.center, camPos, forward, maxAngle))
            {
                AimAt(pc, camPos, b.center);
                return;
            }
        }
        _lockedPlayer = null;

        // Re-acquire: closest-to-crosshair alive player inside the cone
        Player best = null;
        float bestAngle = maxAngle;
        Vector3 bestAim = Vector3.zero;

        foreach (var pl in PlayerManager.AlivePlayers)
        {
            if (pl == null || pl == Player.LocalPlayer)
                continue;
            try
            {
                Vector3 tp = GetPlayerBounds(pl, out var b) ? b.center : pl.Transform.position + Vector3.up * 1.35f;
                Vector3 to = tp - camPos;
                float dist = to.magnitude;
                if (dist < 1.5f || dist > 160f)
                    continue;

                float angle = Vector3.Angle(forward, to);
                if (angle >= bestAngle)
                    continue;

                best = pl;
                bestAngle = angle;
                bestAim = tp;
            }
            catch { }
        }

        if (best == null)
            return;

        _lockedPlayer = best;
        AimAt(pc, camPos, bestAim);
    }

    /// <summary>
    /// Player model render bounds (aggregates nearby parts). The transform origin is
    /// unreliable — using the visible body guarantees correct aiming and ESP boxes.
    /// </summary>
    public static bool GetPlayerBounds(Player pl, out Bounds b)
    {
        b = default;
        try
        {
            var rs = pl.GetComponentsInChildren<Renderer>();
            if (rs == null || rs.Length == 0)
                return false;

            Vector3 pos = pl.Transform.position;
            Bounds acc = default;
            bool has = false;
            foreach (var r in rs)
            {
                if (r == null)
                    continue;
                var rb = r.bounds;
                if ((rb.center - pos).sqrMagnitude > 16f) // skip parts >4m away (name tags etc.)
                    continue;
                if (!has) { acc = rb; has = true; }
                else acc.Encapsulate(rb);
            }
            if (!has || acc.size.y < 0.4f)
                return false;
            b = acc;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDead(Player pl)
    {
        try { return pl.Dying != null && pl.Dying.IsDead; }
        catch { return true; }
    }

    private static bool HoldingGun(Player p)
    {
        try
        {
            var held = p.Holding != null ? p.Holding.HeldItem : null;
            return held != null && held.Weapon != null;
        }
        catch { return false; }
    }

    private static bool LockValid(Vector3 aim, Vector3 camPos, Vector3 forward, float maxAngle)
    {
        Vector3 to = aim - camPos;
        float dist = to.magnitude;
        if (dist < 1.5f || dist > 170f)
            return false;
        return Vector3.Angle(forward, to) <= maxAngle + 8f; // hysteresis: no lock flapping at the boundary
    }

    private static void AimAt(PlayerCamera pc, Vector3 camPos, Vector3 aimPos)
    {
        Vector3 dir = (aimPos - camPos).normalized;
        float targetPitch = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
        float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        Vector3 rot = (Vector3)_fiRot.GetValue(pc);
        float k = Mathf.Clamp01(Features.AimbotStrength);
        rot.y += Mathf.DeltaAngle(rot.y, targetYaw) * k;
        rot.x += (targetPitch - rot.x) * k;
        _fiRot.SetValue(pc, rot);
    }

    /// <summary>
    /// World impact point of the camera crosshair: raycast from the camera center along
    /// the view direction; falls back to a point 300m away. Bullets and the laser
    /// (when convergence is enabled) resolve to this point.
    /// </summary>
    public static Vector3 CameraAimPoint(Vector3 fromPos)
    {
        var cam = GameInfo.CurCamera;
        if (cam == null)
            return fromPos + Vector3.forward * 300f;
        Vector3 o = cam.transform.position;
        Vector3 d = cam.transform.forward;
        if (Physics.Raycast(o, d, out var hit, 300f, GameInfo.ProjectileHitLayer, QueryTriggerInteraction.Ignore)
            && (hit.point - o).sqrMagnitude > 2.25f) // guard against point-blank self hits
            return hit.point;
        return o + d * 300f;
    }
}
