using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HTFMod;

/// <summary>
/// All cheat logic. Most operations require server authority: either this machine is
/// the host, or the action is relayed through the online bridge to the host.
/// </summary>
public static class Features
{
    // ────────────────────────── state ──────────────────────────

    public static bool GodMode;          // self-only, enforced at the damage entries
    public static bool FullnessLock;     // self-only fullness/health lock
    public static bool BuiltInCheats;
    public static bool DripSpawn;
    public static bool RouletteAutoWin;
    public static bool NoRecoil;
    public static bool NoSpread;
    public static bool InfiniteAmmo;
    public static bool FastFire;           // apply FireRateMult to every weapon
    public static float FireRateMult = 1f; // every weapon's shot interval divider
    public static int FireMode = 1;        // 0 = semi (click) / 1 = auto (hold) / 2 = auto + instant click
    public static bool KeepInventory;    // no drops on death/respawn
    public static bool SelfOneShot;      // own attacks deal fixed 100000 damage
    public static bool Aimbot;           // aim players while holding RMB
    public static bool Crosshair;        // muzzle-direction crosshair
    public static bool SilentAim;             // redirect bullets to the cone target, no camera move
    public static bool SilentAimPredict = true; // lead the target by its velocity
    public static int SilentAimPriority = 0;  // 0 crosshair / 1 distance / 2 lowest HP
    public static int SilentAimTarget = 2;    // 0 players / 1 creatures / 2 both (legacy)
    public static bool SilentAimPlayers = true;
    public static bool SilentAimCreatures = true;
    public static bool SilentAimHead;         // aim at the head instead of the chest (legacy)
    public static int SilentAimPlayerPoint;   // 0 body / 1 head (legacy exclusive)
    public static bool SilentAimAimBody = true;  // allow body as a player aim point
    public static bool SilentAimAimHead;         // allow head as a player aim point
    public static bool SilentAimBodyIfLethal = true; // if a body shot already kills, prefer body
    public static bool ForceCrit;                    // creature hits always count as headshots
    public static float SilentAimAngle = 60f; // target search cone (deg)
    public static bool CreatureEsp;             // creature boxes + name tags through walls
    public static bool CreatureLabel = true;    // name+distance text over creature boxes
    public static bool CreatureFrame = true;    // corner frame
    public static bool CreatureFill;            // translucent AABB fill
    public static bool CreatureSilhouette;      // outermost mesh outline
    public static float CreatureEspDist = 150f; // creature label max distance
    public static bool ItemEsp;                 // dropped item labels through walls
    public static bool ItemLabel = true;        // name+distance text over items
    public static bool ItemFrame;               // corner frame
    public static bool ItemFill;                // translucent fill
    public static int LabelSize = 12;           // ESP label font size (players/creatures/items)
    public static int ItemEspStyle = 0;         // 0 label only / 1 corners
    public static float AimbotStrength = 0.5f;
    public static float AimbotMaxAngle = 30f;
    public static bool PlayerEsp;
    public static bool PlayerLabel = true;  // name+distance text over player boxes
    public static bool PlayerSkeleton;      // draw bone lines for other players
    public static bool PlayerHealth;        // health bar above the box
    public static int EspBoxColor = 0xF24C4C;      // packed 0xRRGGBB
    public static int EspSkeletonColor = 0x4CE65E; // packed 0xRRGGBB
    public static int EspCreatureColor = 0xCC73F2; // packed 0xRRGGBB, muted purple
    public static int EspItemColor = 0xE0B85C;     // warm gold, distinct from water/UI teal
    public static int CrosshairColor = 0xF2F2F2;   // packed 0xRRGGBB
    public static int EspFillColor = 0xF24C4C;      // player box fill
    public static int EspCreatureFillColor = 0xCC73F2; // creature box fill
    public static int EspItemFillColor = 0xE0B85C;     // item box fill
    public static float EspOutlineWidth = 1f;       // ESP outline thickness
    public static int PlayerBoxStyle = 0;   // 0 = full 2D box / 1 = corners only / 2 = translucent fill
    public static int CreatureEspStyle = 0; // 0 corners / 1 2D box / 2 silhouette / 3 label
    public static int CrosshairStyle = 0;   // 0 = cross / 1 = dot
    public static float CrosshairLen = 7f;  // crosshair tick length
    public static float CrosshairGap = 5f;  // crosshair center gap
    public static float ItemEspDist = 150f; // item label max distance
    public static float SilentAimHeight = 0.45f; // silent aim impact height (0 feet - 1 head)
    public static float EspBoxAlpha = 0.70f;     // player box outline alpha
    public static float EspFillAlpha = 0.08f;    // player box fill alpha
    public static float CreatureEspAlpha = 0.70f;  // creature outline alpha
    public static float CreatureFillAlpha = 0.07f; // creature fill alpha
    public static float ItemEspAlpha = 0.62f;      // item outline alpha
    public static float ItemFillAlpha = 0.14f;     // item fill alpha
    public static bool BoatFlight;       // W/A/S/D + Space/Ctrl, bow follows view
    public static bool PlayerFlight;     // Space/Ctrl flight; yields while driving
    public static bool ThirdPerson;      // camera behind the player
    public static float ThirdPersonDist = 3f;
    public static float FovMult = 1f;    // camera FOV multiplier
    public static bool IslandLabels;     // all islands direction + distance on screen
    public static bool SlotPredict;      // slot machine prediction HUD
    public static int SlotCheatRarity;   // 0 off / 1 rare / 2 legendary (host)
    public static int SlotSkinItemId = -1;  // forced prize item id (-1 = none)
    public static int SlotSkinIndex;        // forced prize skin index
    public static bool BaitFish;         // spawn the selected creature onto the hook
    public static bool BaitInstant;      // instant random fish on an empty underwater bait
    public static float TpIsland = 1f;   // teleport target island (1-based)
    public static float SpeedMult = 1f;
    public static float JumpMult = 1f;
    public static float BoatMult = 1f;
    public static float GiantWeight = 10f;
    public static Item Selected;
    public static string Search = "";
    public static int SpawnCount = 1;

    private static readonly Dictionary<PlayerMovement, float[]> _moveOrig = new();
    private static readonly Dictionary<BoatMotor, float> _boatOrig = new();
    private static FieldInfo _fiForce;

    // ────────────────────────── basics ──────────────────────────

    public static bool IsHost
    {
        get
        {
            var s = Server.Instance;
            return s != null && s.IsServerInitialized;
        }
    }

    /// <summary>True when server-authoritative actions are possible (host, or host mod bridged).</summary>
    public static bool CanHostAct => IsHost || Multiplayer.HostModOnline;

    /// <summary>
    /// Unified entry: executes directly on the host; as a client relays the request
    /// to the host through the online bridge.
    /// </summary>
    public static bool HostAct(CheatAction action, bool flag = false, int amount = 0, byte currency = 0, int itemId = 0)
    {
        if (IsHost)
        {
            try { Multiplayer.Execute(action, flag, amount, currency, itemId, Local); return true; }
            catch (Exception e) { Loader.Log("Execute failed: " + e); return false; }
        }
        if (Multiplayer.HostModOnline)
            return Multiplayer.Send(new CheatRequest
            {
                Action = (byte)action,
                Flag = flag,
                Amount = amount,
                Currency = currency,
                ItemId = itemId,
            });
        ModCore.Controller?.ShowToast(L.T(
            "需要主机也安装本 mod", "需要主機也安裝本 mod", "The host needs this mod too"));
        return false;
    }

    public static bool InGame => Player.LocalPlayer != null;

    public static Player Local => Player.LocalPlayer;

    public static Camera Cam => GameInfo.CurCamera != null ? GameInfo.CurCamera : Camera.main;

    // ────────────────────────── player ──────────────────────────

    public static void TickVitals()
    {
        // The game pre-hosts a local FishNet server on the main menu (IsHost is true
        // there), so InGame must be checked first or Local.Vitals throws on null player.
        if (!InGame || !IsHost)
            return;
        if (FullnessLock)
            WriteVitals(Local.Vitals);
        foreach (var p in Multiplayer.VitalsLockPlayers)
        {
            if (p != null)
                WriteVitals(p.Vitals);
        }
    }

    private static void WriteVitals(PlayerVitals v)
    {
        if (v == null)
            return;
        try
        {
            v._syncedHealth.Value = 100;
            v._syncedFullness.Value = 100;
            v._syncedPoison.Value = 0;
            v._syncedFire.Value = 0;
        }
        catch (Exception e)
        {
            Loader.Log("TickVitals failed: " + e);
        }
    }

    public static void TickMovement()
    {
        if (!InGame || Local.Movement == null)
            return;

        var m = Local.Movement;

        if (Math.Abs(SpeedMult - 1f) < 0.001f && Math.Abs(JumpMult - 1f) < 0.001f)
        {
            RestoreMovement(m);
            return;
        }

        if (!_moveOrig.TryGetValue(m, out var orig))
        {
            orig = new[] { GetF(m, "_walkSpeed"), GetF(m, "_sprintSpeed"), GetF(m, "_jumpForce") };
            _moveOrig[m] = orig;
        }

        SetF(m, "_walkSpeed", orig[0] * SpeedMult);
        SetF(m, "_sprintSpeed", orig[1] * SpeedMult);
        SetF(m, "_jumpForce", orig[2] * JumpMult);
    }

    private static void RestoreMovement(PlayerMovement m)
    {
        if (!_moveOrig.TryGetValue(m, out var orig))
            return;
        SetF(m, "_walkSpeed", orig[0]);
        SetF(m, "_sprintSpeed", orig[1]);
        SetF(m, "_jumpForce", orig[2]);
        _moveOrig.Remove(m);
    }

    private static float GetF(object o, string name)
    {
        var f = o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        return f != null ? Convert.ToSingle(f.GetValue(o)) : 0f;
    }

    private static void SetF(object o, string name, float v)
    {
        var f = o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        f?.SetValue(o, v);
    }

    // ────────────────────────── money ──────────────────────────

    public static void AddMoney(int amount, Currency currency)
    {
        if (!IsHost)
            return;
        try { MoneyManager.AddMoney(amount, Local, currency); }
        catch (Exception e) { Loader.Log("AddMoney failed: " + e); }
    }

    public static void SetBuiltInCheats(bool on)
    {
        try { ClientSettings.ToggleCheats(on); BuiltInCheats = on; }
        catch (Exception e) { Loader.Log("ToggleCheats failed: " + e); }
    }

    // ────────────────────────── items ──────────────────────────

    public static void SpawnSelected(int count)
    {
        var prefab = Selected;
        if (prefab == null || !IsHost)
            return;

        var cam = Cam;
        if (cam == null)
            return;

        Vector3 basePos = cam.transform.position + cam.transform.forward * 3f;
        Quaternion rot = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
        SpawnSpread(prefab, basePos, rot, count);
    }

    /// <summary>Spawns count items in a spread pattern, returns the success count.</summary>
    public static int SpawnSpread(Item prefab, Vector3 basePos, Quaternion rot, int count)
    {
        int n = 0;
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = new Vector3(
                UnityEngine.Random.Range(-1.2f, 1.2f) * i,
                0.4f + i * 0.35f,
                UnityEngine.Random.Range(-1.2f, 1.2f) * i);
            if (SpawnOne(prefab, basePos + offset, rot) != null)
                n++;
        }
        return n;
    }

    /// <summary>Bridge: spawn by item id around the given position.</summary>
    public static int SpawnItemByIdAt(byte id, Vector3 pos, int count)
    {
        if (!IsHost)
            return 0;
        var prefab = GameInfo.IDToItem(id);
        if (prefab == null)
        {
            Loader.Log("Bridge spawn: unknown item id " + id);
            return 0;
        }
        var cam = Cam;
        Quaternion rot = Quaternion.Euler(0f, cam != null ? cam.transform.eulerAngles.y : 0f, 0f);
        return SpawnSpread(prefab, pos + Vector3.up * 0.4f, rot, count);
    }

    public static void FishRain(int count)
    {
        if (!IsHost || !InGame)
            return;
        FishRainAt(Local.Transform.position + Vector3.up * 9f, count);
    }

    /// <summary>Bridge: fish rain around the given origin, returns the success count.</summary>
    public static int FishRainAt(Vector3 origin, int count)
    {
        if (!IsHost)
            return 0;

        var pool = ItemDb.Creatures;
        if (pool == null || pool.Count == 0)
            return 0;

        int n = 0;
        for (int i = 0; i < count; i++)
        {
            var prefab = pool[UnityEngine.Random.Range(0, pool.Count)];
            Vector2 ring = UnityEngine.Random.insideUnitCircle * 9f;
            Vector3 pos = origin + new Vector3(ring.x, UnityEngine.Random.Range(0f, 4f), ring.y);
            Quaternion rot = Quaternion.Euler(UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(0f, 360f));
            if (SpawnOne(prefab, pos, rot) != null)
                n++;
        }
        return n;
    }

    private static Item SpawnOne(Item prefab, Vector3 pos, Quaternion rot)
    {
        try
        {
            var mgr = ItemManager.Instance;
            if (mgr == null)
                return null;

            var it = mgr.SpawnNewItem(prefab, pos, rot);
            if (it == null)
                return null;

            // Extras are each guarded: a failure never breaks the spawn itself.
            if (DripSpawn)
            {
                try
                {
                    var cr = it.Creature != null ? it.Creature : it as Creature;
                    if (cr != null)
                        cr.SetDrip();
                }
                catch (Exception e) { Loader.Log("SetDrip failed: " + e); }
            }

            if (GiantWeight > 1.01f)
            {
                try { it._syncedRandomWeight.Value = GiantWeight; }
                catch (Exception e) { Loader.Log("SetWeight failed: " + e); }
            }

            return it;
        }
        catch (Exception e)
        {
            Loader.Log("SpawnOne failed: " + e);
            return null;
        }
    }

    // ────────────────────────── vehicles ──────────────────────────

    private static Rigidbody _flightRb;
    private static FieldInfo _fiCurMotor;
    private static float _nextIslandPreload;

    /// <summary>Real bow direction of the boat = -Propeller.right (thrust direction).</summary>
    private static Vector3 GetBoatBow(Boat boat, Rigidbody rb)
    {
        try
        {
            _fiCurMotor ??= AccessTools.Field(typeof(Boat), "_curMotor");
            var motor = _fiCurMotor?.GetValue(boat) as BoatMotor;
            var prop = motor != null ? motor.Propeller : null;
            if (prop != null)
            {
                Vector3 bow = -prop.right;
                bow.y = 0f;
                if (bow.sqrMagnitude > 0.0001f)
                    return bow.normalized;
            }
        }
        catch { }
        if (boat.VisualBoat != null)
        {
            Vector3 bow = boat.VisualBoat.forward;
            bow.y = 0f;
            if (bow.sqrMagnitude > 0.0001f)
                return bow.normalized;
        }
        Vector3 f = rb.rotation * Vector3.forward;
        f.y = 0f;
        return f.normalized;
    }

    /// <summary>
    /// Boat flight (hard displacement mode: zero inertia, physics fully ignored).
    /// The bow follows the driver's view; W/S move along the view, A/D strafe,
    /// Space rises, Ctrl descends; releasing all keys freezes the boat instantly.
    /// Islands within 250m are preloaded so streaming keeps up at high speed.
    /// </summary>
    public static void TickBoatFlight()
    {
        if (!InGame)
        {
            RestoreFlightRb();
            return;
        }

        var boat = BoatManager.Boat;
        if (!BoatFlight || boat == null || !Boat.IsDrivingLocally)
        {
            RestoreFlightRb();
            return;
        }

        Rigidbody rb = boat.HiddenPhysicsRig != null ? boat.HiddenPhysicsRig : boat.VisualPhysicsRig;
        if (rb == null)
            return;

        if (_flightRb != rb)
        {
            RestoreFlightRb();
            rb.isKinematic = true; // the physics engine fully ignores the boat while flying
            _flightRb = rb;
        }

        // Kinematic: forces/gravity are dead, position and rotation obey only us.
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        var camT = Local.Camera != null ? Local.Camera.CamTransform : null;

        // Movement: W/S forward/back, A/D strafe (relative to the view), Space/Ctrl vertical.
        Vector3 fwd = Vector3.forward;
        Vector3 rightV = Vector3.right;
        if (camT != null)
        {
            fwd = camT.forward;
            fwd.y = 0f;
            fwd.Normalize();
            rightV = camT.right;
            rightV.y = 0f;
            rightV.Normalize();
        }
        float sp = 18f * BoatMult;
        Vector3 move = Vector3.zero;
        if (InputX.KeyHeld(KeyCode.W)) move += fwd;
        if (InputX.KeyHeld(KeyCode.S)) move -= fwd;
        if (InputX.KeyHeld(KeyCode.D)) move += rightV;
        if (InputX.KeyHeld(KeyCode.A)) move -= rightV;
        if (InputX.KeyHeld(Hotkeys.Key(Hotkeys.BoatFlightUp))) move += Vector3.up;
        if (InputX.KeyHeld(Hotkeys.Key(Hotkeys.BoatFlightDown))) move -= Vector3.up;
        if (move.sqrMagnitude > 0.0001f)
        {
            // MovePosition/MoveRotation go through physics interpolation: items on the
            // boat follow smoothly instead of teleporting.
            rb.MovePosition(rb.position + move.normalized * (sp * Time.deltaTime));
        }

        // High-speed preload: request every not-yet-loaded island within 250m (every 1.5s).
        // Already-loaded islands must NEVER be LoadIsland'ed again — island content
        // (radios etc.) would spawn a duplicate copy.
        if (Time.unscaledTime >= _nextIslandPreload)
        {
            _nextIslandPreload = Time.unscaledTime + 1.5f;
            try
            {
                var pp = Local.Transform.position;

                // Positions of currently loaded islands.
                var loadedXZ = new List<Vector2>();
                foreach (var isl in UnityEngine.Object.FindObjectsByType<Island>(FindObjectsInactive.Exclude))
                {
                    if (isl == null)
                        continue;
                    var p2 = isl.transform.position;
                    loadedXZ.Add(new Vector2(p2.x, p2.z));
                }

                int max = OnlineIslandManager.MaxIslandUnlocked;
                for (int i = 0; i < max; i++)
                {
                    var info = IslandManager.GetIslandInfo(i);
                    if (info == null)
                        continue;
                    var ip = info.IslandPosition;
                    var ipXZ = new Vector2(ip.x, ip.z);

                    float dxp = ip.x - pp.x, dzp = ip.z - pp.z;
                    if (dxp * dxp + dzp * dzp < Island.IslandSize * Island.IslandSize)
                        continue; // the island the player stands on is loaded by definition

                    bool alreadyLoaded = false;
                    foreach (var l in loadedXZ)
                    {
                        float dx = ipXZ.x - l.x, dz = ipXZ.y - l.y;
                        if (dx * dx + dz * dz < 400f) // a loaded island body within 20m = same island
                        {
                            alreadyLoaded = true;
                            break;
                        }
                    }
                    if (alreadyLoaded)
                        continue;

                    float dSqr = dxp * dxp + dzp * dzp;
                    if (dSqr < 250f * 250f)
                        IslandManager.LoadIsland((byte)i);
                }
            }
            catch { }
        }

        // Bow sync (after movement, within the same frame).
        if (camT != null)
        {
            Vector3 bowNow = GetBoatBow(boat, rb);
            Vector3 want = camT.forward;
            want.y = 0f;
            want.Normalize();
            float ang = Vector3.SignedAngle(bowNow, want, Vector3.up);
            if (Mathf.Abs(ang) > 0.01f)
                rb.MoveRotation(Quaternion.AngleAxis(ang, Vector3.up) * rb.rotation);
        }
    }

    private static void RestoreFlightRb()
    {
        if (_flightRb != null)
        {
            try
            {
                _flightRb.isKinematic = false;
                _flightRb.useGravity = true;
            }
            catch { /* boat just destroyed */ }
            _flightRb = null;
        }
    }

    public static void TickBoats()
    {
        if (_fiForce == null)
            _fiForce = typeof(BoatMotor).GetField("<Force>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance);

        if (Math.Abs(BoatMult - 1f) < 0.001f)
        {
            foreach (var kv in _boatOrig)
            {
                if (kv.Key != null)
                    _fiForce?.SetValue(kv.Key, kv.Value);
            }
            _boatOrig.Clear();
            return;
        }

        var motors = UnityEngine.Object.FindObjectsByType<BoatMotor>(FindObjectsInactive.Exclude);
        var seen = new HashSet<BoatMotor>();

        foreach (var m in motors)
        {
            if (m == null)
                continue;
            seen.Add(m);
            if (!_boatOrig.TryGetValue(m, out float orig))
            {
                orig = m.Force;
                _boatOrig[m] = orig;
            }
            try { _fiForce?.SetValue(m, orig * BoatMult); }
            catch { /* destroyed-object race */ }
        }

        // Prune destroyed motors.
        var dead = _boatOrig.Keys.Where(k => !seen.Contains(k) || k == null).ToList();
        foreach (var d in dead)
            _boatOrig.Remove(d);
    }

    // ────────────────────────── third-person body clone ──────────────────────────
    // The game destroys the local player's body model at init (only first-person hands
    // survive), so a body is cloned from another player and its SkinnedMeshRenderers
    // are rebound to the local skeleton by bone name.

    private static GameObject _bodyClone;
    private static float _tpNextTry;
    private static readonly List<Renderer> _hiddenRenderers = new();

    /// <summary>While third person is on: ensure the clone exists and hide the first-person hands.</summary>
    public static void TickThirdPerson()
    {
        if (Time.realtimeSinceStartup < _tpNextTry)
            return;
        _tpNextTry = Time.realtimeSinceStartup + 1f;

        if (_bodyClone == null && EnsureBodyClone())
            _bodyClone.SetActive(true);
        HideLocalHands(true);
    }

    /// <summary>Third-person toggle: show/hide the cloned body and restore the hands.</summary>
    public static void SetLocalBodyVisible(bool on)
    {
        if (on)
        {
            if (EnsureBodyClone())
                _bodyClone.SetActive(true);
            HideLocalHands(true);
        }
        else
        {
            if (_bodyClone != null)
                _bodyClone.SetActive(false);
            HideLocalHands(false);
        }
    }

    private static bool EnsureBodyClone()
    {
        try
        {
            var local = Player.LocalPlayer;
            if (local == null || local.Body == null)
                return false;

            var existing = local.Body.transform.Find("HTF_BodyClone");
            if (existing != null)
            {
                _bodyClone = existing.gameObject;
                return true;
            }

            // Template: the current character model of any other player.
            GameObject tmpl = null;
            foreach (var pl in PlayerManager.OtherPlayers)
            {
                if (pl == null || pl.Body == null)
                    continue;
                tmpl = AccessTools.Field(typeof(PlayerBody), "_newCharacter")?.GetValue(pl.Body) as GameObject;
                if (tmpl != null)
                    break;
            }
            if (tmpl == null)
                return false; // no other player in the scene to clone from

            var clone = UnityEngine.Object.Instantiate(tmpl, local.Body.transform);
            clone.name = "HTF_BodyClone";
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;

            // Bone rebind: map cloned skinned meshes onto the local skeleton by name.
            var map = new Dictionary<string, Transform>();
            foreach (var t in local.Body.GetComponentsInChildren<Transform>(true))
                if (!map.ContainsKey(t.name))
                    map[t.name] = t;

            foreach (var smr in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = smr.bones;
                var mapped = new Transform[bones.Length];
                bool ok = bones.Length > 0;
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] != null && map.TryGetValue(bones[i].name, out var m))
                        mapped[i] = m;
                    else { ok = false; break; }
                }
                if (ok)
                {
                    smr.bones = mapped;
                    if (smr.rootBone != null && map.TryGetValue(smr.rootBone.name, out var rbMap))
                        smr.rootBone = rbMap;
                }
                else
                    UnityEngine.Object.Destroy(smr); // drop parts that cannot be rebound
            }

            // Remove the name-tag canvas cloned along with the template.
            foreach (var b in clone.GetComponentsInChildren<Behaviour>(true))
                if (b != null && b.GetType().Name == "Canvas")
                    UnityEngine.Object.Destroy(b.gameObject);

            foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            _bodyClone = clone;
            Loader.Log("Third person: body clone created");
            return true;
        }
        catch (Exception e)
        {
            Loader.Log("Body clone failed: " + e);
            return false;
        }
    }

    /// <summary>
    /// Hide/restore the first-person hands. Only the Renderer components are disabled —
    /// never SetActive on the GameObjects, because the camera rig lives in the same
    /// hierarchy and disabling it turns the screen black.
    /// </summary>
    private static void HideLocalHands(bool hide)
    {
        try
        {
            var local = Player.LocalPlayer;
            if (local == null)
                return;

            if (hide)
            {
                if (_hiddenRenderers.Count > 0)
                    return;
                var objs = AccessTools.Field(typeof(Player), "_localObjects")?.GetValue(local)
                    as IEnumerable<GameObject>;
                if (objs == null)
                    return;
                foreach (var o in objs)
                {
                    if (o == null)
                        continue;
                    foreach (var r in o.GetComponentsInChildren<Renderer>(true))
                    {
                        _hiddenRenderers.Add(r);
                        r.enabled = false;
                    }
                }
            }
            else
            {
                foreach (var r in _hiddenRenderers)
                    if (r != null)
                        r.enabled = true;
                _hiddenRenderers.Clear();
            }
        }
        catch { }
    }

    // ────────────────────────── fun ──────────────────────────

    public static void NextIsland(bool prev)
    {
        if (!IsHost)
            return;
        try { OnlineIslandManager.TpToNextIsland(prev); }
        catch (Exception e) { Loader.Log("TpToNextIsland failed: " + e); }
    }

    /// <summary>Main island count (the last build index is the mini-island scene).</summary>
    public static int RealIslandCount()
    {
        try { return Mathf.Max(1, IslandManager.TotalIslands - 1); }
        catch { return 6; }
    }

    /// <summary>
    /// Teleport everyone to a specific island through the game's own flow
    /// (spawns the target island scene, then moves the players) — host only.
    /// Raw position writes don't work: the target island scene isn't loaded yet.
    /// </summary>
    public static bool TeleportToIsland(int island)
    {
        if (!IsHost)
            return false;
        try
        {
            OnlineIslandManager.TpToSpecificIsland((byte)(island - 1));
            return true;
        }
        catch (Exception e)
        {
            Loader.Log("TeleportToIsland failed: " + e);
            return false;
        }
    }

    /// <summary>Teleport the local player next to another player.</summary>
    public static bool TeleportToPlayer(Player target)
    {
        if (!InGame || target == null || Local.Movement == null)
            return false;
        try
        {
            Local.Movement.Teleport(target.Transform.position + Vector3.up * 1.5f);
            return true;
        }
        catch (Exception e)
        {
            Loader.Log("TeleportToPlayer failed: " + e);
            return false;
        }
    }

    /// <summary>
    /// Destroy every dropped item in the world (host only). Items held by players
    /// or stored in inventories are untouched; keepWeapons spares guns and rods.
    /// </summary>
    public static int ClearDroppedItems(bool keepWeapons)
    {
        if (!IsHost)
            return -1;
        int n = 0;
        try
        {
            foreach (var it in UnityEngine.Object.FindObjectsByType<Item>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (it == null || it.IsDeinitializing)
                    continue;

                // dead creatures are drops too — clear them as well
                if (it is Creature crc)
                {
                    try
                    {
                        if (crc.IsDead)
                        {
                            crc.Despawn(crc.gameObject);
                            n++;
                        }
                    }
                    catch { }
                    continue;
                }

                try
                {
                    if (it.IsInInventory || it.Holder != null)
                        continue;
                    if (keepWeapons && it.Weapon != null)
                        continue;
                    it.Despawn(it.gameObject);
                    n++;
                }
                catch { }
            }
        }
        catch (Exception e)
        {
            Loader.Log("ClearDroppedItems failed: " + e);
        }
        return n;
    }

    private static float _nextBaitFish;

    /// <summary>
    /// Hook control: instant mode attaches a random spawnable fish the moment the
    /// bait is underwater and empty; selected mode uses the creature picked in the
    /// items tab. Host only (spawning is server authority).
    /// </summary>
    public static void TickBaitFish()
    {
        if ((!BaitFish && !BaitInstant) || !InGame || !IsHost)
            return;
        if (Time.unscaledTime < _nextBaitFish)
            return;
        try
        {
            var held = Local.Holding != null ? Local.Holding.HeldItem : null;
            var rod = held as FishingRod ?? (held != null ? held.GetComponent<FishingRod>() : null);
            if (rod == null)
                return;
            var bait = AccessTools.Field(typeof(FishingRod), "_bait")?.GetValue(rod) as Bait;
            if (bait == null || bait.ItemOnBait != null || bait.ServerItemOnBait != null)
                return;
            if (!Bait.BaitsUnderWater.Contains(bait))
                return;

            Item prefab;
            if (BaitFish && Selected != null && (Selected.Creature != null || Selected is Creature))
                prefab = Selected;
            else if (BaitInstant && ItemDb.Creatures.Count > 0)
                prefab = ItemDb.Creatures[UnityEngine.Random.Range(0, ItemDb.Creatures.Count)];
            else
                return;

            _nextBaitFish = Time.unscaledTime + (BaitFish ? 3f : 0.8f);
            var fish = SpawnOne(prefab, bait.Rig.position + Vector3.up * 0.6f, Quaternion.identity);
            if (fish != null)
                bait.AddItemOnBait(fish);
        }
        catch { }
    }

    public static void KillBoss()
    {
        if (!IsHost)
            return;
        try
        {
            var boss = BossManager.Boss;
            if (boss == null)
                return;
            boss.LocalHit(boss.transform, boss.transform.position, Vector3.up,
                Local, 999999, false, Vector3.zero);
        }
        catch (Exception e) { Loader.Log("KillBoss failed: " + e); }
    }

    public static void ToggleAllCreatures(bool killed, bool drip)
    {
        if (!IsHost)
            return;
        try
        {
            GameInfo.ToggleAllCreaturesKilled(killed, drip); // journal/save flags
            int n = killed ? KillAllCreatures() : SpawnCreaturesAround(Local.Transform.position, 12);
            Loader.Log((killed ? "Creatures killed: " : "Creatures respawned: ") + n);
        }
        catch (Exception e) { Loader.Log("ToggleAllCreatures failed: " + e); }
    }

    /// <summary>
    /// Kills every creature in the world. Scans the whole scene (cleaner than the
    /// CreatureManager registry alone). Non-networked "ghost" entities (local clones
    /// that ignore all damage) are destroyed locally instead.
    /// Also used by the online bridge.
    /// </summary>
    public static int KillAllCreatures()
    {
        int n = 0;
        var creatures = UnityEngine.Object.FindObjectsByType<Creature>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var c in creatures)
        {
            if (c == null || IsDeadCreature(c))
                continue;
            try
            {
                if (!c.IsSpawned)
                {
                    UnityEngine.Object.Destroy(c.gameObject); // ghost entity: local clone
                    n++;
                    continue;
                }
                c.LocalHit(c.transform, c.transform.position, Vector3.up,
                    Local, 999999, false, Vector3.zero);
                n++;
            }
            catch
            {
                try { c.Despawn(c.gameObject); n++; } catch { }
            }
        }
        return n;
    }

    private static bool IsDeadCreature(Creature c)
    {
        try { return c.IsDead; }
        catch { return true; }
    }

    /// <summary>Spawns random creatures around a position (respawn/backfill). Also used by the bridge.</summary>
    public static int SpawnCreaturesAround(Vector3 origin, int count)
    {
        if (!IsHost || ItemDb.Creatures.Count == 0)
            return 0;

        int n = 0;
        for (int i = 0; i < count; i++)
        {
            var prefab = ItemDb.Creatures[UnityEngine.Random.Range(0, ItemDb.Creatures.Count)];
            Vector2 ring = UnityEngine.Random.insideUnitCircle * 7f;
            Vector3 pos = origin + new Vector3(ring.x, 4.5f, ring.y);
            Quaternion rot = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            if (SpawnOne(prefab, pos, rot) != null)
                n++;
        }
        return n;
    }

    public static int UnlockAllSkins()
    {
        try
        {
            int count = 0;
            SaveManager.LockAllSkins();
            var items = GameInfo.ItemWithSkinsforCommands;
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null || item.SkinPreset == null)
                        continue;
                    for (byte j = 0; j < item.SkinPreset.Skins.Count; j++)
                    {
                        SaveManager.UnlockSkin(item.ID, j);
                        count++;
                    }
                }
            }
            var boat = BoatManager.Boat;
            if (boat != null && boat.SkinPreset != null)
            {
                for (byte k = 0; k < boat.SkinPreset.Skins.Count; k++)
                {
                    SaveManager.UnlockSkin(byte.MaxValue, k);
                    count++;
                }
            }
            return count;
        }
        catch (Exception e)
        {
            Loader.Log("UnlockAllSkins failed: " + e);
            return -1;
        }
    }
}

/// <summary>Cache of item prefabs from Resources/Items.</summary>
public static class ItemDb
{
    public static List<Item> All { get; private set; } = new();
    public static List<Item> Creatures { get; private set; } = new();
    public static bool Loaded { get; private set; }

    public static void Load()
    {
        if (Loaded)
            return;
        try
        {
            var items = Resources.LoadAll<Item>("Items");
            All = items.Where(i => i != null).OrderBy(NameOf, StringComparer.OrdinalIgnoreCase).ToList();

            // Creatures: prefer the game's own spawnable registry (the same set its
            // spawn commands use). The raw Resources scan also catches decorative
            // creatures that have no health bar and take no damage from any hit.
            Creatures = SpawnableCreatures();
            if (Creatures.Count == 0)
                Creatures = All.Where(i => i.Creature != null || i is Creature).ToList();

            Loaded = true;
            Loader.Log($"Item DB cached: {All.Count} items, {Creatures.Count} creatures");
        }
        catch (Exception e)
        {
            Loader.Log("ItemDb.Load failed: " + e);
        }
    }

    /// <summary>Creature items from GameInfo's spawnable registry (reflected static dict).</summary>
    private static List<Item> SpawnableCreatures()
    {
        try
        {
            var f = AccessTools.Field(typeof(GameInfo), "_idToSpawnable");
            if (f?.GetValue(null) is System.Collections.IDictionary dict)
            {
                var list = new List<Item>();
                var seen = new HashSet<Item>();
                foreach (var v in dict.Values)
                {
                    if (v is Item it && (it.Creature != null || it is Creature) && seen.Add(it))
                        list.Add(it);
                }
                return list.OrderBy(NameOf, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
        catch
        {
        }
        return new List<Item>();
    }

    public static string NameOf(Item i)
    {
        try
        {
            string n = i.GetName();
            return string.IsNullOrEmpty(n) ? i.name : n;
        }
        catch
        {
            return i != null ? i.name : "?";
        }
    }
}
