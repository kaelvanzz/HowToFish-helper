using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
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
                        // Vanilla values come from the CLEAN PREFAB, not the instance —
                        // instances may carry spread/rate corrupted by older sessions
                        // (and saved), which is why NoSpread-off kept shotguns merged.
                        orig = (
                            _fiTbs != null ? Convert.ToSingle(_fiTbs.GetValue(w)) : 0f,
                            _fiFullAuto != null && (bool)_fiFullAuto.GetValue(w),
                            _fiSpread != null ? Convert.ToSingle(_fiSpread.GetValue(w)) : 0f);
                        try
                        {
                            var prefabItem = GameInfo.IDToItem(w.ID);
                            var pw = prefabItem != null ? prefabItem.Weapon : null;
                            if (pw != null)
                            {
                                if (_fiTbs != null)
                                    orig.tbs = Convert.ToSingle(_fiTbs.GetValue(pw));
                                if (_fiFullAuto != null)
                                    orig.auto = (bool)_fiFullAuto.GetValue(pw);
                                if (_fiSpread != null)
                                    orig.spread = Convert.ToSingle(_fiSpread.GetValue(pw));
                            }
                        }
                        catch { }
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

    /// <summary>
    /// Shared target search. Priority: 0 = closest to crosshair, 1 = closest distance,
    /// 2 = lowest HP. aimHead aims at the head instead of the chest.
    /// </summary>
    internal static Player FindTarget(Vector3 camPos, Vector3 forward, float maxAngle, bool aimHead, int priority, float aimHeight, out Vector3 aimPoint)
    {
        aimPoint = default;
        Player best = null;
        float bestScore = float.MaxValue;
        Vector3 bestAim = Vector3.zero;
        float bestHp = float.MaxValue;

        foreach (var pl in PlayerManager.AlivePlayers)
        {
            if (pl == null || pl == Player.LocalPlayer)
                continue;
            try
            {
                Vector3 tp = AimPointOf(pl, out var _, aimHead);
                Vector3 to = tp - camPos;
                float dist = to.magnitude;
                if (dist < 0.4f || dist > 200f)
                    continue;

                float angle = Vector3.Angle(forward, to);
                if (angle >= maxAngle)
                    continue;

                float score = priority switch
                {
                    1 => dist,
                    2 => HealthOf(pl),
                    _ => angle,
                };

                if (score < bestScore || (priority == 2 && score <= bestHp + 0.001f && best == null))
                {
                    best = pl;
                    bestScore = score;
                    bestHp = score;
                    bestAim = tp;
                }
            }
            catch { }
        }

        aimPoint = bestAim;
        return best;
    }

    private static int HealthOf(Player pl)
    {
        try { return pl.Vitals != null ? pl.Vitals._syncedHealth.Value : 100; }
        catch { return 100; }
    }

    /// <summary>Silent aim: where the bullet should go (null = no target in cone).
    /// Target type: players / creatures / both, resolved by the priority metric.</summary>
    internal static Vector3? SilentTarget(Vector3 camPos, Vector3 forward, out Vector3 targetVel)
    {
        targetVel = Vector3.zero;
        Player bestP = null;
        Vector3 bestPAim = default;
        if (Features.SilentAimPlayers)
            bestP = FindTarget(camPos, forward, Features.SilentAimAngle, WantPlayerHead(), Features.SilentAimPriority, Features.SilentAimHeight, out bestPAim);

        Creature bestC = null;
        Vector3 bestCAim = default;
        float bestCScore = float.MaxValue;
        if (Features.SilentAimCreatures)
        {
            ScanSaCreatures();
            foreach (var cr in _saCreatures)
            {
                if (cr == null)
                    continue;
                try
                {
                    Vector3 tp = CreatureAimPoint(cr, camPos);
                    Vector3 to = tp - camPos;
                    float dist = to.magnitude;
                    if (dist < 0.25f || dist > 200f)
                        continue;
                    float angle = Vector3.Angle(forward, to);
                    if (angle >= Features.SilentAimAngle)
                        continue;
                    float score = Features.SilentAimPriority switch
                    {
                        1 => dist,
                        2 => HealthOfCreature(cr),
                        _ => angle,
                    };
                    if (score < bestCScore)
                    {
                        bestCScore = score;
                        bestC = cr;
                        bestCAim = tp;
                    }
                }
                catch { }
            }
        }

        if (bestP == null && bestC == null)
            return null;
        if (bestP == null)
        {
            targetVel = _saVel.TryGetValue(bestC, out var cv) ? cv : Vector3.zero;
            return bestCAim;
        }
        if (bestC == null)
        {
            try { targetVel = bestP.Rigidbody != null ? bestP.Rigidbody.linearVelocity : Vector3.zero; }
            catch { targetVel = Vector3.zero; }
            return bestPAim;
        }

        // both worlds have a candidate — the priority metric decides
        float pS = Features.SilentAimPriority switch
        {
            1 => (bestPAim - camPos).magnitude,
            2 => HealthOf(bestP),
            _ => Vector3.Angle(forward, bestPAim - camPos),
        };
        float cS = Features.SilentAimPriority switch
        {
            1 => (bestCAim - camPos).magnitude,
            2 => HealthOfCreature(bestC),
            _ => Vector3.Angle(forward, bestCAim - camPos),
        };
        if (cS < pS)
        {
            targetVel = _saVel.TryGetValue(bestC, out var cv) ? cv : Vector3.zero;
            return bestCAim;
        }
        try { targetVel = bestP.Rigidbody != null ? bestP.Rigidbody.linearVelocity : Vector3.zero; }
        catch { targetVel = Vector3.zero; }
        return bestPAim;
    }

    /// <summary>
    /// Build a muzzle-to-target direction. Lead is capped so circling fish do not
    /// get overshot; gravity lifts the aim for ballistic (non-hitscan) shots.
    /// </summary>
    internal static bool TrySilentAimDir(Vector3 muzzle, float speed, float gravity, out Vector3 dir)
    {
        dir = default;
        var cam = GameInfo.CurCamera;
        if (cam == null)
            return false;
        var target = SilentTarget(cam.transform.position, cam.transform.forward, out var tVel);
        if (target == null)
            return false;

        Vector3 aim = target.Value;
        Vector3 to = aim - muzzle;
        float dist = to.magnitude;
        if (dist < 0.35f)
            return false;

        if (Features.SilentAimPredict && tVel.sqrMagnitude > 0.04f && speed > 1f)
        {
            float t = Mathf.Clamp(dist / speed, 0f, 0.32f);
            Vector3 lead = tVel * t;
            float leadCap = Mathf.Clamp(dist * 0.12f, 0.35f, 3.5f);
            if (lead.sqrMagnitude > leadCap * leadCap)
                lead = lead.normalized * leadCap;
            aim += lead;
            if (gravity > 0.05f)
            {
                float t2 = Mathf.Clamp((aim - muzzle).magnitude / speed, 0f, 0.32f);
                aim += Vector3.up * (0.5f * gravity * t2 * t2);
            }
        }

        dir = aim - muzzle;
        return dir.sqrMagnitude > 0.12f;
    }

    /// <summary>
    /// Snap onto a live collider instead of the AABB center. Thin / rotated fish
    /// have an AABB that sticks out of the mesh, so aiming at bounds.center misses.
    /// </summary>
    private static Vector3 CreatureAimPoint(Creature cr, Vector3 from)
    {
        try
        {
            var cols = cr.GetComponentsInChildren<Collider>();
            if (cols != null && cols.Length > 0)
            {
                Collider best = null;
                float bestVol = -1f;
                for (int i = 0; i < cols.Length; i++)
                {
                    var c = cols[i];
                    if (c == null || !c.enabled) continue;
                    Vector3 sz = c.bounds.size;
                    float vol = sz.x * sz.y * sz.z;
                    if (vol > bestVol)
                    {
                        bestVol = vol;
                        best = c;
                    }
                }
                if (best != null)
                {
                    var b = best.bounds;
                    float y = Mathf.Lerp(b.min.y, b.max.y, Mathf.Clamp01(Features.SilentAimHeight));
                    Vector3 sample = new Vector3(b.center.x, y, b.center.z);
                    Vector3 to = sample - from;
                    float dist = to.magnitude;
                    if (dist > 0.05f)
                    {
                        try
                        {
                            int layers = (int)GameInfo.ProjectileHitLayer
                                       | (int)GameInfo.ItemLayer
                                       | (int)GameInfo.NpcLayer;
                            if (Physics.SphereCast(from, 0.08f, to / dist, out var hit, dist + 1.5f, layers,
                                    QueryTriggerInteraction.Collide)
                                && hit.transform != null
                                && hit.transform.IsChildOf(cr.transform))
                                return hit.point;
                        }
                        catch { }
                    }
                    try
                    {
                        Vector3 onCol = best.ClosestPoint(sample);
                        if ((onCol - from).sqrMagnitude > 0.01f)
                            return onCol;
                    }
                    catch { }
                    return sample;
                }
            }
        }
        catch { }
        return cr.transform.position + Vector3.up * Mathf.Lerp(0.15f, 0.7f, Features.SilentAimHeight);
    }

    private static void ScanSaCreatures()
    {
        if (Time.unscaledTime < _saNextScan)
            return;
        _saNextScan = Time.unscaledTime + 0.08f;
        _saCreatures.Clear();
        float now = Time.unscaledTime;
        var seen = new HashSet<Creature>();
        try
        {
            foreach (var cr in UnityEngine.Object.FindObjectsByType<Creature>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (cr == null)
                    continue;
                try
                {
                    if (cr.IsDead || !cr.IsSpawned)
                        continue;
                }
                catch { continue; }
                seen.Add(cr);
                var cur = cr.transform.position;
                if (_saLastPos.TryGetValue(cr, out var lp))
                {
                    float dt = now - _saLastT[cr];
                    if (dt > 0.02f && dt < 0.45f)
                    {
                        Vector3 raw = (cur - lp) / dt;
                        // teleport / scan hitch → drop the sample instead of leading 100m
                        if (raw.sqrMagnitude > 1600f)
                            raw = Vector3.zero;
                        if (_saVel.TryGetValue(cr, out var old))
                            _saVel[cr] = Vector3.Lerp(old, raw, 0.4f);
                        else
                            _saVel[cr] = raw;
                    }
                }
                _saLastPos[cr] = cur;
                _saLastT[cr] = now;
                _saCreatures.Add(cr);
            }
            // prune despawned creatures
            List<Creature> gone = null;
            foreach (var k in _saLastPos.Keys)
                if (!seen.Contains(k))
                    (gone ??= new List<Creature>()).Add(k);
            if (gone != null)
                foreach (var k in gone)
                {
                    _saLastPos.Remove(k);
                    _saLastT.Remove(k);
                    _saVel.Remove(k);
                }
        }
        catch { }
    }

    private static readonly List<Creature> _saCreatures = new();
    private static readonly Dictionary<Creature, Vector3> _saVel = new();
    private static readonly Dictionary<Creature, Vector3> _saLastPos = new();
    private static readonly Dictionary<Creature, float> _saLastT = new();
    private static float _saNextScan;

    private static int HealthOfCreature(Creature cr)
    {
        try { return cr.Hp; }
        catch { return int.MaxValue; }
    }

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
                    Vector3 ap = ChestPoint(_lockedPlayer, b, AimHeadFor(_lockedPlayer));
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
            var best = FindTarget(camPos, forward, maxAngle, WantPlayerHead(), 0, 0.65f, out var bestAim);
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

    /// <summary>Head unless "body if lethal" and this shot would already kill.</summary>
    private static bool WantPlayerHead()
    {
        return Features.SilentAimPlayerPoint == 1 || Features.SilentAimHead;
    }

    private static bool AimHeadFor(Player pl)
    {
        if (!WantPlayerHead())
            return false;
        if (!Features.SilentAimBodyIfLethal)
            return true;
        int dmg = LocalShotDamage();
        if (dmg <= 0)
            return true;
        return HealthOf(pl) > dmg;
    }

    private static int LocalShotDamage()
    {
        try
        {
            var p = Player.LocalPlayer;
            var held = p != null && p.Holding != null ? p.Holding.HeldItem : null;
            var w = held != null ? held.Weapon : null;
            if (w == null)
                return 0;
            var m = AccessTools.Method(typeof(Weapon), "GetShotDamage");
            if (m == null)
                return 0;
            return Convert.ToInt32(m.Invoke(w, null));
        }
        catch { return 0; }
    }

    private static Vector3 AimPointOf(Player pl, out Bounds b, bool head = false)
    {
        if (head && Features.SilentAimBodyIfLethal && !AimHeadFor(pl))
            head = false;
        if (GetPlayerBounds(pl, out b))
            return ChestPoint(pl, b, head);
        return pl.Transform.position + Vector3.up * (head ? 1.6f : 1.35f);
    }

    private static Vector3 ChestPoint(Player pl, Bounds b, bool head = false)
    {
        float baseY = pl.Transform.position.y;
        float top = Mathf.Min(b.max.y, baseY + 2.0f);
        float y = head ? top - 0.08f : Mathf.Lerp(baseY + 0.8f, top, 0.65f);
        return new Vector3(b.center.x, y, b.center.z);
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
