using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HTFMod;

/// <summary>全部作弊逻辑。绝大多数操作要求服务器权威(自己是主机,或通过联机桥由主机代执行)。</summary>
public static class Features
{
    // ────────────────────────── 状态 ──────────────────────────

    public static bool GodMode;        // 上帝模式:只保护自己(伤害入口拦截)
    public static bool FullnessLock;   // 锁满饱食度:只锁自己
    public static bool BuiltInCheats;
    public static bool DripSpawn;
    public static bool RouletteAutoWin;
    public static bool NoRecoil;
    public static bool NoSpread;
    public static bool InfiniteAmmo;
    public static bool KeepInventory;   // 死亡/复活不掉落物品
    public static bool SelfOneShot;     // 仅自己秒杀(本地放大自己子弹伤害)
    public static bool Aimbot;          // 自瞄玩家(按住右键瞄准时激活)
    public static float AimbotStrength = 0.5f;
    public static float AimbotMaxAngle = 30f;
    public static bool PlayerEsp;
    public static bool BoatFlight;     // 载具飞行:Space 上升 / Ctrl 下降,静态悬停
    public static bool PlayerFlight;   // 玩家飞行:Space 上升 / Ctrl 下降(开船时自动让位)
    public static bool ThirdPerson;    // 第三人称视角
    public static float ThirdPersonDist = 3f;
    public static float FovMult = 1f;  // FOV 倍率
    public static bool IslandLabels;   // 屏幕显示所有岛屿方向与距离
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

    // ────────────────────────── 基础 ──────────────────────────

    public static bool IsHost
    {
        get
        {
            var s = Server.Instance;
            return s != null && s.IsServerInitialized;
        }
    }

    /// <summary>本机能否执行服务器权威操作(自己是主机,或主机也装了 mod 走联机桥)。</summary>
    public static bool CanHostAct => IsHost || Multiplayer.HostModOnline;

    /// <summary>统一入口:主机直调,客户端走联机桥发给主机代执行。</summary>
    public static bool HostAct(CheatAction action, bool flag = false, int amount = 0, byte currency = 0, int itemId = 0)
    {
        if (IsHost)
        {
            try { Multiplayer.Execute(action, flag, amount, currency, itemId, Local); return true; }
            catch (Exception e) { Loader.Log("执行失败: " + e); return false; }
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
        ModCore.Controller?.ShowToast("需要主机也安装本 mod");
        return false;
    }

    public static bool InGame => Player.LocalPlayer != null;

    public static Player Local => Player.LocalPlayer;

    public static Camera Cam => GameInfo.CurCamera != null ? GameInfo.CurCamera : Camera.main;

    // ────────────────────────── 玩家 ──────────────────────────

    public static void TickVitals()
    {
        if (!IsHost)
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
            Loader.Log("TickVitals 失败: " + e);
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

    // ────────────────────────── 金钱 ──────────────────────────

    public static void AddMoney(int amount, Currency currency)
    {
        if (!IsHost)
            return;
        try { MoneyManager.AddMoney(amount, Local, currency); }
        catch (Exception e) { Loader.Log("AddMoney 失败: " + e); }
    }

    public static void SetBuiltInCheats(bool on)
    {
        try { ClientSettings.ToggleCheats(on); BuiltInCheats = on; }
        catch (Exception e) { Loader.Log("ToggleCheats 失败: " + e); }
    }

    // ────────────────────────── 物品 ──────────────────────────

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

    /// <summary>按扩散模式生成 count 个,返回成功数。</summary>
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

    /// <summary>联机桥:按物品 ID 在指定玩家周围生成。</summary>
    public static int SpawnItemByIdAt(byte id, Vector3 pos, int count)
    {
        if (!IsHost)
            return 0;
        var prefab = GameInfo.IDToItem(id);
        if (prefab == null)
        {
            Loader.Log("联机生成: 找不到物品 ID " + id);
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

    /// <summary>联机桥:在指定原点周围下鱼雨,返回成功数。</summary>
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

            // 额外属性逐项兜底:任何一项失败都不影响生成本身
            if (DripSpawn)
            {
                try
                {
                    var cr = it.Creature != null ? it.Creature : it as Creature;
                    if (cr != null)
                        cr.SetDrip();
                }
                catch (Exception e) { Loader.Log("SetDrip 失败: " + e); }
            }

            if (GiantWeight > 1.01f)
            {
                try { it._syncedRandomWeight.Value = GiantWeight; }
                catch (Exception e) { Loader.Log("SetWeight 失败: " + e); }
            }

            return it;
        }
        catch (Exception e)
        {
            Loader.Log("SpawnOne 失败: " + e);
            return null;
        }
    }

    // ────────────────────────── 载具 ──────────────────────────

    private static Rigidbody _flightRb;
    private static FieldInfo _fiCurMotor;
    private static float _nextIslandPreload;

    /// <summary>当前船的真实船头方向 = -螺旋桨.right(推进力方向)。</summary>
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
    /// 载具飞行(强制位移模式,零惯性零物理):
    /// 船头同步玩家视线方向;W/S 沿视线前后,Space 升,Ctrl 降;松开全部键立即静止。
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
            rb.isKinematic = true; // 物理引擎完全接管前先解放:动力学引擎不再作用这条船
            _flightRb = rb;
        }

        // kinematic 下力与重力全部无效,位置/旋转只听我们的
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        var camT = Local.Camera != null ? Local.Camera.CamTransform : null;

        // 船头同步玩家视线水平朝向(按真实船头 -Propeller.right 与视线的夹角旋转)
        // (同步逻辑已并入上方位移段,MoveRotation 随帧平滑)
        if (camT == null)
        {
            Vector3 bowNow = GetBoatBow(boat, rb);
            Vector3 want = Vector3.forward;
            float ang = Vector3.SignedAngle(bowNow, want, Vector3.up);
            if (Mathf.Abs(ang) > 0.01f)
                rb.MoveRotation(Quaternion.AngleAxis(ang, Vector3.up) * rb.rotation);
        }

        // 强制位移:W/S 前后、A/D 横移(沿视线),Space/Ctrl 垂直,零惯性
        // 速度完全跟随船速倍率;高速时靠"前方岛屿预加载"保证流式加载跟得上
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
        if (InputX.KeyHeld(KeyCode.Space)) move += Vector3.up;
        if (InputX.KeyHeld(KeyCode.LeftControl)) move -= Vector3.up;
        if (move.sqrMagnitude > 0.0001f)
        {
            // MovePosition/MoveRotation 走物理插值:船上物品平滑跟随,不瞬移不卡顿
            rb.MovePosition(rb.position + move.normalized * (sp * Time.deltaTime));
        }

        // 高速飞行预加载:把玩家周围 250m 内"尚未加载"的岛屿请求加载(每 1.5s 一次)
        // 已加载的岛屿绝不能重复 LoadIsland,否则岛上内容(收音机等)会再生成一份
        if (Time.unscaledTime >= _nextIslandPreload)
        {
            _nextIslandPreload = Time.unscaledTime + 1.5f;
            try
            {
                var pp = Local.Transform.position;

                // 当前已加载岛屿的位置集合
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
                        continue; // 玩家所在的当前岛,必然已加载

                    bool alreadyLoaded = false;
                    foreach (var l in loadedXZ)
                    {
                        float dx = ipXZ.x - l.x, dz = ipXZ.y - l.y;
                        if (dx * dx + dz * dz < 400f) // 20m 内有已加载的岛体 = 同一座岛
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

        // 船头同步(放位移之后,同一帧内完成)
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

    // ────────────────────────── 第三人称身体克隆 ──────────────────────────
    // 游戏初始化时会把本地玩家的身体模型直接销毁(只留第一人称的手),
    // 所以从其他玩家的模型克隆一份,并把 SkinnedMeshRenderer 重绑到本地骨架。

    private static GameObject _bodyClone;
    private static float _tpNextTry;
    private static readonly List<Renderer> _hiddenRenderers = new();

    /// <summary>第三人称开启期间每秒尝试:确保克隆存在 + 隐藏第一人称手部。</summary>
    public static void TickThirdPerson()
    {
        if (Time.realtimeSinceStartup < _tpNextTry)
            return;
        _tpNextTry = Time.realtimeSinceStartup + 1f;

        if (_bodyClone == null && EnsureBodyClone())
            _bodyClone.SetActive(true);
        HideLocalHands(true);
    }

    /// <summary>第三人称开关:显示/隐藏克隆身体,并恢复手部。</summary>
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

            // 模板:其他玩家当前的角色模型
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
                return false; // 场景里没有其他玩家可作模板

            var clone = UnityEngine.Object.Instantiate(tmpl, local.Body.transform);
            clone.name = "HTF_BodyClone";
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;

            // 骨骼重绑:按名字把克隆的蒙皮网格绑到本地骨架
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
                    UnityEngine.Object.Destroy(smr); // 绑不上的部件丢弃
            }

            // 删掉克隆里带出来的名牌 Canvas
            foreach (var b in clone.GetComponentsInChildren<Behaviour>(true))
                if (b != null && b.GetType().Name == "Canvas")
                    UnityEngine.Object.Destroy(b.gameObject);

            foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            _bodyClone = clone;
            Loader.Log("第三人称:身体模型克隆完成");
            return true;
        }
        catch (Exception e)
        {
            Loader.Log("身体克隆失败: " + e);
            return false;
        }
    }

    /// <summary>
    /// 隐藏/恢复第一人称手部。只关闭渲染器组件,绝不动 GameObject 激活状态 ——
    /// 相机刚性挂在同一层级里,直接 SetActive 会把相机一起藏掉(黑屏)。
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
                    as System.Collections.Generic.IEnumerable<GameObject>;
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

    private static void RestoreFlightRb()
    {
        if (_flightRb != null)
        {
            try
            {
                _flightRb.isKinematic = false;
                _flightRb.useGravity = true;
            }
            catch { /* 船刚被销毁 */ }
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
            catch { /* 对象销毁竞争 */ }
        }

        // 清理已销毁的
        var dead = _boatOrig.Keys.Where(k => !seen.Contains(k) || k == null).ToList();
        foreach (var d in dead)
            _boatOrig.Remove(d);
    }

    // ────────────────────────── 玩乐 ──────────────────────────

    public static void NextIsland(bool prev)
    {
        if (!IsHost)
            return;
        try { OnlineIslandManager.TpToNextIsland(prev); }
        catch (Exception e) { Loader.Log("TpToNextIsland 失败: " + e); }
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
        catch (Exception e) { Loader.Log("KillBoss 失败: " + e); }
    }

    public static void ToggleAllCreatures(bool killed, bool drip)
    {
        if (!IsHost)
            return;
        try
        {
            GameInfo.ToggleAllCreaturesKilled(killed, drip); // 图鉴/存档标记
            int n = killed ? KillAllCreatures() : SpawnCreaturesAround(Local.Transform.position, 12);
            Loader.Log((killed ? "清空生物: 击杀 " : "复活生物: 补充 ") + n + " 只");
        }
        catch (Exception e) { Loader.Log("ToggleAllCreatures 失败: " + e); }
    }

    /// <summary>
    /// 击杀世界中所有生物。全场景扫描(比只扫 CreatureManager 注册表更干净),
    /// 未联网的"幽灵实体"(本地克隆,怎么打都没伤害)直接本地销毁。
    /// 联机桥也会调用。
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
                    UnityEngine.Object.Destroy(c.gameObject); // 幽灵实体:本地克隆
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

    /// <summary>在指定位置周围补充随机生物(模拟"复活/回填")。联机桥也会调用。</summary>
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
            Loader.Log("UnlockAllSkins 失败: " + e);
            return -1;
        }
    }
}

/// <summary>物品 prefab 缓存(Resources/Items)。</summary>
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
            Creatures = All.Where(i => i.Creature != null || i is Creature).ToList();
            Loaded = true;
            Loader.Log($"物品库缓存完成: {All.Count} 项, {Creatures.Count} 生物");
        }
        catch (Exception e)
        {
            Loader.Log("ItemDb.Load 失败: " + e);
        }
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
