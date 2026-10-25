using System;
using System.Reflection;
using UnityEngine;

namespace HTFMod;

/// <summary>
/// Combat helpers:
/// - No recoil: Harmony blocks view/gun-model recoil (see Patches.cs).
/// - No spread: zeroes the held weapon's spread angle every frame.
/// - Infinite ammo: keeps the held weapon's magazine full.
/// - Aim players: while the trigger hotkey is held, locks onto the player
///   closest to the crosshair and tracks until it dies or leaves the cone.
///   The pull itself runs in the PlayerCamera.SetCamPosRot prefix (Patches.cs)
///   so it survives any script execution order.
/// - Player ESP: boxes + distance for other players through walls (drawn in MenuUi).
/// </summary>
public static class Combat
{
    private static FieldInfo _fiRot;    // PlayerCamera._rot  (Vector3: x=pitch, y=yaw)
    private static FieldInfo _fiSpread; // Weapon._spread     (float)
    private static PropertyInfo _piAmmo; // Weapon.Ammo       (int, private setter)
    private static FieldInfo _fiTbs;    // Weapon._timeBetweenShots (fire rate)
    private static FieldInfo _fiFullAuto; // Weapon._fullAuto (fire mode)
    private static bool _initFailed;

    private static Player _lockedPlayer;

    // per-weapon originals: fire rate / fire mode / spread are restored from these
    // whenever their feature is off — serialized fields must never stay mutated
    private static readonly System.Collections.Generic.Dictionary<Weapon, (float tbs, bool auto, float spread)> _weaponOrig = new();
    private static float _frScanNext;

    public static void Tick(bool menuOpen)
    {
        var p = Player.LocalPlayer;
        if (p == null)
        {
            _lockedPlayer = null;
            _aimActive = false;
            return;
        }

        Init();
        if (_initFailed)
            return;

        if (Features.InfiniteAmmo && _piAmmo != null)
            TickWeapon(p);

        // fire rate / fire mode / spread apply to EVERY weapon (held or not)
        if (_fiTbs != null || _fiFullAuto != null || _fiSpread != null)
            TickAllWeapons();

        if (Features.Aimbot && _fiRot != null)
            TickAimbot(p, menuOpen);
        else
        {
            _lockedPlayer = null;
            _aimActive = false;
        }
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
            _fiTbs ??= typeof(Weapon).GetField("_timeBetweenShots", BindingFlags.NonPublic | BindingFlags.Instance);
            _fiFullAuto ??= typeof(Weapon).GetField("_fullAuto", BindingFlags.NonPublic | BindingFlags.Instance);
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

    /// <summary>
    /// Per-weapon overrides, scanned every 0.25s across ALL weapons in the scene:
    /// - FastFire: shot interval divided by FireRateMult (original restored when off)
    /// - FireMode: click = semi, hold / hold+click = full auto (via _fullAuto)
    /// - NoSpread: spread zeroed while on, ORIGINAL spread restored when off —
    ///   serialized fields must never stay mutated, otherwise turning NoSpread
    ///   back off would keep shotgun pellets merged into one point.
    /// </summary>
    private static void TickAllWeapons()
    {
        if (UnityEngine.Time.unscaledTime < _frScanNext)
            return;
        _frScanNext = UnityEngine.Time.unscaledTime + 0.25f;

        try
        {
            var weapons = UnityEngine.Object.FindObjectsByType<Weapon>(
                UnityEngine.FindObjectsInactive.Exclude, UnityEngine.FindObjectsSortMode.None);

            bool applyRate = Features.FastFire && Features.FireRateMult > 1.01f;

            foreach (var w in weapons)
            {
                if (w == null)
                    continue;

                if (!_weaponOrig.TryGetValue(w, out var orig))
                {
                    try
                    {
                        orig = (
                            _fiTbs != null ? Convert.ToSingle(_fiTbs.GetValue(w)) : 0f,
                            _fiFullAuto != null && (bool)_fiFullAuto.GetValue(w),
                            _fiSpread != null ? Convert.ToSingle(_fiSpread.GetValue(w)) : 0f);
                        _weaponOrig[w] = orig;
                    }
                    catch { continue; }
                }

                if (_fiTbs != null && orig.tbs > 0f)
                    _fiTbs.SetValue(w, applyRate ? orig.tbs / Features.FireRateMult : orig.tbs);

                if (_fiFullAuto != null)
                    _fiFullAuto.SetValue(w, Features.FireMode != 0);

                if (_fiSpread != null)
                    _fiSpread.SetValue(w, Features.NoSpread ? 0f : orig.spread);
            }

            // drop destroyed weapons from the cache
            if (_weaponOrig.Count > 0)
            {
                System.Collections.Generic.List<Weapon> dead = null;
                foreach (var kv in _weaponOrig)
                {
                    if (kv.Key == null)
                    {
                        (dead ??= new System.Collections.Generic.List<Weapon>()).Add(kv.Key);
                    }
                }
                if (dead != null)
                    foreach (var d in dead)
                        _weaponOrig.Remove(d);
            }
        }
        catch { }
    }

    // ────────────────────── Aim players ──────────────────────

    // Acquisition result consumed by the SetCamPosRot prefix (see Patches.cs):
    // writing _rot right before the game applies it makes the aim immune to
    // script execution order between our Update and the game's Update.
    private static Vector3 _aimPoint;
    private static bool _aimActive;
    private static bool _aimErrLogged;

    /// <summary>True while the aimbot should pull the view toward AimPoint.</summary>
    public static bool AimActive => _aimActive;

    private static void TickAimbot(Player p, bool menuOpen)
    {
        try
        {
            bool trigger = InputX.KeyHeld(Hotkeys.Key(Hotkeys.AimbotTrigger));
            if (menuOpen || !trigger)
            {
                _lockedPlayer = null;
                _aimActive = false;
                return;
            }
            var pc = p.Camera;
            if (pc == null || !pc.MouseLocked)
            {
                _lockedPlayer = null;
                _aimActive = false;
                return;
            }
            var cam = GameInfo.CurCamera;
            if (cam == null)
            {
                _aimActive = false;
                return;
            }

            Vector3 camPos = cam.transform.position;
            Vector3 forward = cam.transform.forward;
            float maxAngle = Features.AimbotMaxAngle;

            // Sticky lock: keep tracking until the target dies or leaves the cone
            if (_lockedPlayer != null && _lockedPlayer != Player.LocalPlayer && !IsDead(_lockedPlayer))
            {
                if (GetPlayerBounds(_lockedPlayer, out var b))
                {
                    Vector3 ap = ChestPoint(_lockedPlayer, b);
                    if (LockValid(ap, camPos, forward, maxAngle))
                    {
                        _aimPoint = ap;
                        _aimActive = true;
                        return;
                    }
                }
            }
            _lockedPlayer = null;
            _aimActive = false;

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
                    Vector3 tp = AimPointOf(pl, out var _);
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
            _aimPoint = bestAim;
            _aimActive = true;
        }
        catch (Exception e)
        {
            _aimActive = false;
            AimError(e);
        }
    }

    /// <summary>
    /// Called from the PlayerCamera.SetCamPosRot prefix on the LOCAL player's camera:
    /// pulls _rot toward the locked aim point by AimbotStrength per frame. The game
    /// applies _rot to the camera transform in the same call, so the pull is visible
    /// immediately, every frame, regardless of script execution order.
    /// </summary>
    public static void ApplyAim(PlayerCamera pc)
    {
        try
        {
            var p = Player.LocalPlayer;
            if (p == null || pc == null || pc != p.Camera)
                return;
            var cam = GameInfo.CurCamera;
            if (cam == null)
                return;

            Vector3 camPos = cam.transform.position;
            Vector3 dir = (_aimPoint - camPos).normalized;
            Quaternion want = Quaternion.LookRotation(dir);

            // Decompose the ACTUAL camera orientation and pull _rot by the angle
            // deltas toward the wanted view. No pitch/yaw sign conventions anywhere:
            // the deltas are measured against the same eulerAngles the game applies,
            // so vertical correction is exact regardless of the game's _rot mapping.
            Vector3 curE = cam.transform.eulerAngles;
            Vector3 wantE = want.eulerAngles;

            Vector3 rot = (Vector3)_fiRot.GetValue(pc);
            float k = Mathf.Clamp01(Features.AimbotStrength);
            rot.y += Mathf.DeltaAngle(curE.y, wantE.y) * k;
            rot.x += Mathf.DeltaAngle(curE.x, wantE.x) * k;
            rot.x = Mathf.Clamp(rot.x, -90f, 90f);
            _fiRot.SetValue(pc, rot);
        }
        catch (Exception e)
        {
            AimError(e);
        }
    }

    /// <summary>One-time logging so aimbot failures are visible in LogOutput.log.</summary>
    private static void AimError(Exception e)
    {
        if (_aimErrLogged)
            return;
        _aimErrLogged = true;
        Loader.Log("Aimbot error: " + e.Message);
    }

    /// <summary>
    /// Stable chest/neck aim point: 65% up the visible body, XZ from the render
    /// bounds. Bounds exclude floating name tags (see GetPlayerBounds) so the Y
    /// no longer jumps between with-tag and without-tag frames.
    /// </summary>
    private static Vector3 AimPointOf(Player pl, out Bounds b)
    {
        if (GetPlayerBounds(pl, out b))
            return ChestPoint(pl, b);
        return pl.Transform.position + Vector3.up * 1.35f;
    }

    private static Vector3 ChestPoint(Player pl, Bounds b)
    {
        float baseY = pl.Transform.position.y;
        float top = Mathf.Min(b.max.y, baseY + 2.0f);
        return new Vector3(b.center.x, Mathf.Lerp(baseY + 0.8f, top, 0.65f), b.center.z);
    }

    /// <summary>
    /// Player model render bounds (aggregates nearby body parts). The transform origin is
    /// unreliable — using the visible body guarantees correct aiming and ESP boxes.
    /// Renderers floating more than 2.4m above the feet (name tags) are excluded so the
    /// bounds hug the actual body.
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
                if ((rb.center - pos).sqrMagnitude > 16f) // skip parts >4m away
                    continue;
                if (rb.center.y - pos.y > 2.4f) // floating name tags above the head
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

    private static bool LockValid(Vector3 aim, Vector3 camPos, Vector3 forward, float maxAngle)
    {
        Vector3 to = aim - camPos;
        float dist = to.magnitude;
        if (dist < 1.5f || dist > 170f)
            return false;
        return Vector3.Angle(forward, to) <= maxAngle + 8f; // hysteresis: no lock flapping at the boundary
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
