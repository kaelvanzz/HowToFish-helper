using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace HTFMod;

/// <summary>
/// Dark-theme IMGUI menu. INSERT toggles, title bar draggable.
/// All user-facing strings resolve through L.T (Simplified / Traditional Chinese / English);
/// the language cycles via the button in the bottom-right corner and persists to the config.
/// </summary>
public static class MenuUi
{
    // ────────────────────────── theme ──────────────────────────
    private static readonly Color CPanel = new(0.043f, 0.055f, 0.068f, 0.955f);
    private static readonly Color CRow = new(0.085f, 0.105f, 0.125f, 1f);
    private static readonly Color CRowHover = new(0.115f, 0.140f, 0.165f, 1f);
    private static readonly Color CAccent = new(0.24f, 0.88f, 0.82f, 1f);
    private static readonly Color CAccentDim = new(0.24f, 0.88f, 0.82f, 0.14f);
    private static readonly Color CText = new(0.93f, 0.95f, 0.96f, 1f);
    private static readonly Color CTextDim = new(0.56f, 0.61f, 0.65f, 1f);
    private static readonly Color CWarn = new(0.96f, 0.78f, 0.38f, 1f);
    private static readonly Color CDanger = new(0.94f, 0.44f, 0.44f, 1f);

    private const float WinW = 470f;

    /// <summary>Window height: tall enough for the biggest tab on 1080p+,
    /// shrinks on small screens; the tab scrollview handles any leftover overflow.</summary>
    private static float WinH => Mathf.Min(800f, Screen.height - 110f);

    private static Rect _win = new Rect(80, 80, WinW, WinH);
    private static int _page;
    private static Vector2 _scroll;
    private static Vector2 _tabScroll;

    private class PageDef
    {
        public string Label;
        public Action<CheatController> Draw;
        public PageDef(string label, Action<CheatController> draw) { Label = label; Draw = draw; }
    }

    /// <summary>Sidebar navigation pages (regrouped: money→world, fun→player/world).</summary>
    private static List<PageDef> Pages => new List<PageDef>
    {
        new PageDef(L.T("玩家", "玩家", "Player"), TabPlayer),
        new PageDef(L.T("金钱", "金錢", "Money"), TabMoney),
        new PageDef(L.T("战斗", "戰鬥", "Combat"), TabCombat),
        new PageDef(L.T("视觉", "視覺", "Visual"), TabVisual),
        new PageDef(L.T("物品", "物品", "Items"), TabItems),
        new PageDef(L.T("世界", "世界", "World"), TabWorld),
        new PageDef(L.T("赌场", "賭場", "Casino"), TabCasino),
        new PageDef(L.T("钓鱼", "釣魚", "Fishing"), TabFishing),
        new PageDef(L.T("载具", "載具", "Vehicle"), TabBoat),
        new PageDef(L.T("按键", "按鍵", "Keys"), TabKeys),
    };

    private const float NavW = 104f;
    private const float NavGap = 16f;

    // hotkey rebinding: which action is waiting for a key press (null = none)
    private static string _listening;
    private static string _pickerAction; // action owning the expanded color picker
    private static string _pickerSlot;   // which color slot is expanded ("box"/"fill"/...)

    private static Font _font;
    private static bool _stylesReady;
    private static GUI.WindowFunction _windowFn;

    private static GUIStyle _panel, _label, _labelDim, _labelSmall, _title, _tabOn, _tabOff,
        _btn, _btnAccent, _btnDisabled, _search, _chip, _chipOn, _value, _pillOn, _pillOff,
        _sliderBg, _sliderThumb, _hint;

    private static Texture2D _texPanel, _texRow, _texRowHover, _texBtn, _texBtnHover,
        _texBtnAccent, _texPillOn, _texPillOff, _texSliderBg, _texSliderThumb, _texChip, _texKnob;

    public static void Draw(CheatController c)
    {
        EnsureFont();
        EnsureStyles();

        // hotkey rebinding: while listening, swallow the next key/mouse press globally
        HandleBindCapture();

        // bind popup: click outside closes it, Esc closes when not listening
        if (_bindPopup != null)
        {
            var e = Event.current;
            if (e != null && e.type == EventType.MouseDown && !_bindPopupRect.Contains(e.mousePosition))
                _bindPopup = null;
            else if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && _listening == null)
            {
                _bindPopup = null;
                e.Use();
            }
        }

        if (c.MenuOpen)
        {
            if (_windowFn == null)
                _windowFn = id => WindowFunc(id, c);
            _win = GUI.Window(0x485446, _win, _windowFn, "", _panel);
            ClampWindow();
        }
        else
        {
            DrawHudHint(c);
        }

        DrawBindPopupWindow();

        DrawIslandLabels();
        DrawPlayerEsp();
        DrawLootEsp();
        DrawCrosshair(c);
        DrawActiveHud(c);
        DrawRouletteHud();
        DrawSlotsHud();
        DrawToast(c);
    }

    // ────────────────────────── item / creature ESP ──────────────────────────

    private sealed class LootEntry
    {
        public Component Obj;
        public string Name;
        public bool Creature;
        public bool Dead;            // dead creature (loot)
        public Collider[] Cols;      // all colliders — live .bounds every frame (hitbox)
        public Renderer[] Rends;     // fallback when there is no collider
        public Bounds Bounds;        // last-scan AABB, only used if colliders/renderers vanish
        public Vector3 BoundsOffset; // box center relative to the object position
    }

    private static readonly List<LootEntry> _loot = new();
    private static float _lootNextScan;
    private static bool _scanCreaturesNow = true;

    /// <summary>
    /// Creature/item ESP. Positions are read every frame from live transforms;
    /// object lists are scanned on a timer. Default look is a tight 2D outline
    /// with the name above the box and distance below — unobtrusive, readable.
    /// </summary>
    private static void DrawLootEsp()
    {
        if (!Features.CreatureEsp && !Features.ItemEsp)
        {
            _loot.Clear();
            return;
        }
        var cam = GameInfo.CurCamera;
        if (cam == null || Player.LocalPlayer == null)
            return;

        // time-sliced scan: one phase (creatures OR items) per tick — the old
        // full-scan-in-one-frame caused the periodic hitch
        if (Time.unscaledTime >= _lootNextScan)
        {
            _lootNextScan = Time.unscaledTime + 0.18f;
            _scanCreaturesNow = !_scanCreaturesNow;
            RescanLoot(_scanCreaturesNow);
        }

        for (int i = _loot.Count - 1; i >= 0; i--)
        {
            var e = _loot[i];
            if (e.Obj == null)
            {
                _loot.RemoveAt(i);
                continue;
            }
            if (!TryLootBounds(e, out var bb))
                continue;

            Vector3 sp = cam.WorldToScreenPoint(bb.center);
            if (sp.z <= 0.1f)
                continue;
            float maxDist = e.Creature ? Features.CreatureEspDist : Features.ItemEspDist;
            float d = Vector3.Distance(Player.LocalPlayer.Transform.position, bb.center);
            if (d > maxDist)
                continue;

            float fade = EspDistanceFade(d, maxDist);
            var lc = EspColor(e.Creature ? Features.EspCreatureColor : Features.EspItemColor);
            var lineCol = lc;
            lineCol.a = Mathf.Clamp01(e.Creature ? Features.CreatureEspAlpha : Features.ItemEspAlpha) * fade;

            Rect boxR;
            if (!ProjectBounds(bb, cam, out boxR, out _))
                boxR = new Rect(sp.x - 5f, Screen.height - sp.y - 5f, 10f, 10f);

            if (e.Creature)
            {
                if (Features.CreatureFill)
                {
                    var fillCol = EspColor(Features.EspCreatureFillColor);
                    fillCol.a = Mathf.Clamp01(Features.CreatureFillAlpha) * fade;
                    DrawEspSoftFill(boxR, fillCol);
                }
                if (Features.CreatureFrame)
                    DrawEspFrame(boxR, lineCol, true);
                if (Features.CreatureSilhouette)
                    DrawCreatureSilhouette(e.Obj, cam, lineCol);
            }
            else
            {
                bool frame = Features.ItemFrame || Features.ItemEspStyle >= 1;
                if (Features.ItemFill)
                {
                    var fillCol = EspColor(Features.EspItemFillColor);
                    fillCol.a = Mathf.Clamp01(Features.ItemFillAlpha) * fade;
                    DrawEspSoftFill(boxR, fillCol);
                }
                if (frame)
                    DrawEspFrame(boxR, lineCol, true);
            }

            if (e.Creature ? Features.CreatureLabel : Features.ItemLabel)
                DrawEspNameplate(cam, bb, e.Name, d, lc, fade);
        }
    }

    private static bool TryLootBounds(LootEntry e, out Bounds bb)
    {
        if (TryLiveBounds(e, out bb))
            return true;
        if (e.Bounds.size.sqrMagnitude > 0.0001f)
        {
            bb = new Bounds(e.Obj.transform.position + e.BoundsOffset, e.Bounds.size);
            return true;
        }
        bb = new Bounds(e.Obj.transform.position + Vector3.up * 0.2f, new Vector3(0.3f, 0.3f, 0.3f));
        return true;
    }

    /// <summary>
    /// Live world AABB: union of every enabled collider (the real hitbox). Falls back
    /// to renderers so items without a collider still hug the mesh, including when
    /// you walk right up to them.
    /// </summary>
    private static bool TryLiveBounds(LootEntry e, out Bounds bb)
    {
        bb = default;
        bool any = false;
        if (e.Cols != null)
        {
            for (int i = 0; i < e.Cols.Length; i++)
            {
                var c = e.Cols[i];
                if (c == null || !c.enabled) continue;
                if (!any) { bb = c.bounds; any = true; }
                else bb.Encapsulate(c.bounds);
            }
        }
        if (any && bb.size.sqrMagnitude > 0.0001f)
            return true;

        any = false;
        Vector3 origin = e.Obj != null ? e.Obj.transform.position : Vector3.zero;
        if (e.Rends != null)
        {
            for (int i = 0; i < e.Rends.Length; i++)
            {
                var r = e.Rends[i];
                if (r == null || !r.enabled) continue;
                var rb = r.bounds;
                if ((rb.center - origin).sqrMagnitude > 25f) continue;
                if (!any) { bb = rb; any = true; }
                else bb.Encapsulate(rb);
            }
        }
        return any && bb.size.sqrMagnitude > 0.0001f;
    }

    /// <summary>Project the 8 corners of a world AABB to a tight screen-space rect.</summary>
    private static bool ProjectBounds(Bounds bb, Camera cam, out Rect box, out float dist)
    {
        box = default;
        Vector3 c = bb.center;
        Vector3 e = bb.extents;
        Vector3 spc = cam.WorldToScreenPoint(c);
        dist = spc.z;
        if (dist <= 0.1f)
            return false;

        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        int hits = 0;
        for (int i = 0; i < 8; i++)
        {
            var p = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);
            Vector3 s = cam.WorldToScreenPoint(p);
            if (s.z <= 0.05f)
                continue;
            float y = Screen.height - s.y;
            if (s.x < minX) minX = s.x;
            if (s.x > maxX) maxX = s.x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
            hits++;
        }
        if (hits == 0)
            return false;

        const float pad = 2f;
        minX -= pad; maxX += pad; minY -= pad; maxY += pad;
        float rawW = maxX - minX;
        float rawH = maxY - minY;
        float w = Mathf.Max(10f, rawW);
        float h = Mathf.Max(10f, rawH);
        minX -= (w - rawW) * 0.5f;
        minY -= (h - rawH) * 0.5f;
        box = new Rect(minX, minY, w, h);
        if (box.xMax < -40f || box.x > Screen.width + 40f || box.yMax < -40f || box.y > Screen.height + 40f)
            return false;
        return true;
    }

    private static float EspDistanceFade(float d, float maxDist)
    {
        if (d <= 90f) return 1f;
        float far = Mathf.Max(maxDist, 91f);
        return Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(far, 90f, d));
    }

    private static void DrawEspFrame(Rect r, Color color, bool corners)
    {
        var under = new Color(0f, 0f, 0f, Mathf.Clamp01(color.a) * 0.5f);
        if (corners)
        {
            DrawEspCorners(r, under);
            DrawEspCorners(r, color);
        }
        else
        {
            DrawEspOutline(r, under, 2f);
            DrawEspOutline(r, color, 1f);
        }
    }

    private static GUIStyle _espDistLabel;
    private static int _espDistBuilt = -1;

    private static GUIStyle EspDistLabel()
    {
        int sz = Mathf.Clamp(Features.LabelSize - 2, 9, 16);
        if (_espDistLabel == null || _espDistBuilt != sz)
        {
            _espDistLabel = Style(null, CText, sz, rich: true);
            _espDistLabel.alignment = TextAnchor.MiddleCenter;
            _espDistBuilt = sz;
        }
        return _espDistLabel;
    }

    /// <summary>
    /// Name above the entity, distance below. Anchored to the world AABB top/bottom
    /// (not the 2D box width) so rotating fish don't shake the text.
    /// </summary>
    private static void DrawEspNameplate(Camera cam, Bounds bb, string name, float dist, Color color, float fade)
    {
        if (string.IsNullOrEmpty(name) || cam == null)
            return;
        name = name.Replace("<", "");
        string hex = ColorUtility.ToHtmlStringRGB(color);
        float a = Mathf.Clamp01(fade);
        int nameH = Mathf.Clamp(Features.LabelSize, 9, 24) + 3;
        int distH = Mathf.Clamp(Features.LabelSize - 2, 9, 16) + 2;
        string distStr = ((int)(dist + 0.5f)).ToString() + "m";

        float nameW = EspLabel().CalcSize(new GUIContent(name)).x + 8f;
        float distW = EspDistLabel().CalcSize(new GUIContent(distStr)).x + 8f;

        Vector3 st = cam.WorldToScreenPoint(new Vector3(bb.center.x, bb.max.y, bb.center.z));
        Vector3 sb = cam.WorldToScreenPoint(new Vector3(bb.center.x, bb.min.y, bb.center.z));
        if (st.z > 0.1f)
        {
            float cx = Mathf.Round(st.x);
            float y = Mathf.Round(Screen.height - st.y);
            DrawEspText(new Rect(cx - nameW * 0.5f, y - nameH - 1f, nameW, nameH),
                name, hex, a, EspLabel());
        }
        if (sb.z > 0.1f)
        {
            var distCol = Color.Lerp(new Color(0.62f, 0.66f, 0.70f), color, 0.35f);
            string distHex = ColorUtility.ToHtmlStringRGB(distCol);
            float cx = Mathf.Round(sb.x);
            float y = Mathf.Round(Screen.height - sb.y);
            DrawEspText(new Rect(cx - distW * 0.5f, y + 1f, distW, distH),
                distStr, distHex, a * 0.78f, EspDistLabel());
        }
    }

    private static void DrawEspText(Rect r, string text, string hex, float alpha, GUIStyle style)
    {
        int sa = Mathf.Clamp(Mathf.RoundToInt(170f * alpha), 0, 255);
        int ma = Mathf.Clamp(Mathf.RoundToInt(255f * alpha), 0, 255);
        GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height),
            $"<color=#000000{sa:X2}>{text}</color>", style);
        GUI.Label(r, $"<color=#{hex}{ma:X2}>{text}</color>", style);
    }

    /// <summary>
    /// Scan one loot phase (creatures or items), alternating so the
    /// FindObjectsByType spike never lands twice in the same frame.
    /// Existing entries are MERGED in place — nothing flickers between scans.
    /// Caps: 20 nearest creatures / 25 nearest items+dead-fish.
    /// </summary>
    private static void RescanLoot(bool creatures)
    {
        try
        {
            if (creatures && Features.CreatureEsp)
            {
                // creatures: merge, keep the 20 nearest
                var cand = new List<(Creature cr, float d)>();
                foreach (var cr in UnityEngine.Object.FindObjectsByType<Creature>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (cr == null)
                        continue;
                    try
                    {
                        if (!cr.IsSpawned || cr.IsDead)
                            continue;
                        var d = (cr.transform.position - Player.LocalPlayer.Transform.position).sqrMagnitude;
                        if (d < Features.CreatureEspDist * Features.CreatureEspDist)
                            cand.Add((cr, d));
                    }
                    catch { continue; }
                }
                var seenC = new HashSet<Component>();
                var nameCount = new Dictionary<string, int>();
                var pickedC = new List<Creature>();
                foreach (var (cr, _) in cand.OrderBy(c => c.d))
                {
                    string nm = ItemDb.NameOf(cr) ?? "";
                    nameCount.TryGetValue(nm, out int n);
                    if (n >= 6) continue; // same-species cap — a piranha ball stays quiet
                    nameCount[nm] = n + 1;
                    pickedC.Add(cr);
                    if (pickedC.Count >= 20) break;
                }
                foreach (var cr in pickedC)
                {
                    try
                    {
                        seenC.Add(cr);
                        var e = _loot.FirstOrDefault(x => x.Obj == cr);
                        if (e == null)
                        {
                            e = new LootEntry { Obj = cr, Creature = true };
                            _loot.Add(e);
                        }
                        e.Name = ItemDb.NameOf(cr);
                        e.Cols = cr.GetComponentsInChildren<Collider>();
                        e.Rends = cr.GetComponentsInChildren<Renderer>();
                        if (TryLiveBounds(e, out var bbScan))
                        {
                            e.Bounds = bbScan;
                            e.BoundsOffset = bbScan.center - cr.transform.position;
                        }
                    }
                    catch { continue; }
                }
                for (int i = _loot.Count - 1; i >= 0; i--)
                {
                    var x = _loot[i];
                    if (x.Creature && (x.Obj == null || !seenC.Contains(x.Obj)))
                        _loot.RemoveAt(i);
                }
            }

            if (!creatures && Features.ItemEsp)
            {
                // items + dead fish: merge, keep the 80 nearest
                var cand = new List<(Component obj, float d, bool dead)>();
                foreach (var it in UnityEngine.Object.FindObjectsByType<Item>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (it == null)
                        continue;
                    try
                    {
                        if (it.IsInInventory || it.IsDeinitializing || !it.IsSpawned)
                            continue;
                        var h = it.SyncedHolder ?? it.Holder;
                        if (h != null)
                            continue;
                        bool dead = false;
                        if (it is Creature crc)
                        {
                            if (!crc.IsDead)
                                continue; // live creatures belong to the creature phase
                            dead = true;
                        }
                        var d = (it.transform.position - Player.LocalPlayer.Transform.position).sqrMagnitude;
                        if (d < Features.ItemEspDist * Features.ItemEspDist)
                            cand.Add((it, d, dead));
                    }
                    catch { continue; }
                }
                var seenI = new HashSet<Component>();
                foreach (var (obj, _, dead) in cand.OrderBy(c => c.d).Take(80))
                {
                    try
                    {
                        seenI.Add(obj);
                        var e = _loot.FirstOrDefault(x => x.Obj == obj);
                        if (e == null)
                        {
                            e = new LootEntry { Obj = obj, Creature = false };
                            _loot.Add(e);
                        }
                        e.Name = ItemDb.NameOf((Item)obj);
                        e.Dead = dead;
                        e.Cols = obj.GetComponentsInChildren<Collider>();
                        e.Rends = obj.GetComponentsInChildren<Renderer>();
                        if (TryLiveBounds(e, out var bbScan))
                        {
                            e.Bounds = bbScan;
                            e.BoundsOffset = bbScan.center - obj.transform.position;
                        }
                    }
                    catch { continue; }
                }
                for (int i = _loot.Count - 1; i >= 0; i--)
                {
                    var x = _loot[i];
                    if (!x.Creature && (x.Obj == null || !seenI.Contains(x.Obj)))
                        _loot.RemoveAt(i);
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Topology for a true silhouette: vertices welded by position (so UV seams
    /// do not become "every triangle is a boundary"), then only edges whose two
    /// faces disagree on camera-facing are drawn. No fill.
    /// </summary>
    private sealed class SilCache
    {
        public Mesh Bake = new Mesh { hideFlags = HideFlags.HideAndDontSave };
        public int[] Weld;
        public int WeldCount;
        public int[] EA, EB, ET1, ET2;
        public int[] T0, T1, T2;
        public int OrigVc, OrigTc;
        public Vector3[] World;
        public bool[] Front;
    }

    private static readonly Dictionary<SkinnedMeshRenderer, SilCache> _silCache = new();
    private static readonly Dictionary<MeshFilter, SilCache> _silCacheMf = new();
    private static readonly Dictionary<long, int> _weldMap = new();
    private static float _silPruneAt;

    private static SilCache NewSilCache() =>
        new SilCache { Bake = new Mesh { hideFlags = HideFlags.HideAndDontSave } };

    private static void DrawCreatureSilhouette(Component obj, Camera cam, Color col)
    {
        if (Event.current != null && Event.current.type != EventType.Repaint)
            return;

        SilCache sc;
        Mesh mesh;
        Transform xform;
        var smr = obj.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null)
        {
            if (!_silCache.TryGetValue(smr, out sc))
                _silCache[smr] = sc = NewSilCache();
            smr.BakeMesh(sc.Bake);
            mesh = sc.Bake;
            xform = smr.transform;
        }
        else
        {
            var mf = obj.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                return;
            if (!_silCacheMf.TryGetValue(mf, out sc))
                _silCacheMf[mf] = sc = NewSilCache();
            mesh = mf.sharedMesh;
            xform = mf.transform;
        }

        var verts = mesh.vertices;
        int vc = verts.Length;
        if (vc < 3 || vc > 8000)
            return;

        if (sc.Weld == null || sc.OrigVc != vc)
        {
            int[] tris = CollectTriangles(mesh);
            if (tris == null || tris.Length < 3)
                return;
            BuildSilTopology(sc, verts, tris);
        }
        int tc = sc.OrigTc;

        var l2w = xform.localToWorldMatrix;
        if (sc.World == null || sc.World.Length < sc.WeldCount)
            sc.World = new Vector3[sc.WeldCount];
        for (int i = 0; i < vc; i++)
            sc.World[sc.Weld[i]] = l2w.MultiplyPoint3x4(verts[i]);

        Vector3 camPos = cam.transform.position;
        if (sc.Front == null || sc.Front.Length < tc)
            sc.Front = new bool[tc];
        for (int t = 0; t < tc; t++)
        {
            Vector3 a = sc.World[sc.T0[t]];
            Vector3 b = sc.World[sc.T1[t]];
            Vector3 c = sc.World[sc.T2[t]];
            Vector3 n = Vector3.Cross(b - a, c - a);
            sc.Front[t] = Vector3.Dot(n, camPos - a) > 0f;
        }

        EnsureLineMat();
        _lineMat.SetPass(0);
        GL.PushMatrix();
        GL.LoadPixelMatrix();
        int sh = Screen.height;
        // dark underlay then color — silhouette stays readable over water/sky
        GL.Begin(GL.LINES);
        GL.Color(new Color(0f, 0f, 0f, Mathf.Clamp(col.a * 0.45f, 0.18f, 0.4f)));
        EmitSilhouetteEdges(sc, cam, sh, 1f);
        GL.End();
        GL.Begin(GL.LINES);
        GL.Color(new Color(col.r, col.g, col.b, Mathf.Clamp(col.a * 0.92f, 0.4f, 0.95f)));
        EmitSilhouetteEdges(sc, cam, sh, 0f);
        GL.End();
        GL.PopMatrix();

        if (Time.unscaledTime >= _silPruneAt)
        {
            _silPruneAt = Time.unscaledTime + 5f;
            PruneSilCache();
        }
    }

    private static void EmitSilhouetteEdges(SilCache sc, Camera cam, int sh, float pixelOffset)
    {
        for (int i = 0; i < sc.EA.Length; i++)
        {
            bool f1 = sc.ET1[i] >= 0 && sc.Front[sc.ET1[i]];
            bool f2 = sc.ET2[i] >= 0 && sc.Front[sc.ET2[i]];
            if (f1 == f2)
                continue;
            Vector3 sa = cam.WorldToScreenPoint(sc.World[sc.EA[i]]);
            Vector3 sb = cam.WorldToScreenPoint(sc.World[sc.EB[i]]);
            if (sa.z <= 0.1f || sb.z <= 0.1f)
                continue;
            GL.Vertex3(sa.x + pixelOffset, sh - sa.y + pixelOffset, 0);
            GL.Vertex3(sb.x + pixelOffset, sh - sb.y + pixelOffset, 0);
        }
    }

    private static int[] CollectTriangles(Mesh mesh)
    {
        if (mesh.subMeshCount <= 1)
            return mesh.triangles;
        var all = new List<int>(mesh.triangles);
        for (int s = 1; s < mesh.subMeshCount; s++)
            all.AddRange(mesh.GetTriangles(s));
        return all.ToArray();
    }

    private static void BuildSilTopology(SilCache sc, Vector3[] verts, int[] tris)
    {
        int vc = verts.Length;
        int tc = tris.Length / 3;
        sc.OrigVc = vc;
        sc.OrigTc = tc;
        sc.Weld = new int[vc];
        _weldMap.Clear();
        int wc = 0;
        for (int i = 0; i < vc; i++)
        {
            long k = HashVert(verts[i]);
            if (!_weldMap.TryGetValue(k, out int w))
            {
                w = wc++;
                _weldMap[k] = w;
            }
            sc.Weld[i] = w;
        }
        sc.WeldCount = wc;
        sc.T0 = new int[tc];
        sc.T1 = new int[tc];
        sc.T2 = new int[tc];

        var edges = new Dictionary<long, int>(tc * 2);
        var ea = new List<int>(tc * 2);
        var eb = new List<int>(tc * 2);
        var et1 = new List<int>(tc * 2);
        var et2 = new List<int>(tc * 2);

        void AddEdge(int a, int b, int tri)
        {
            if (a == b) return;
            int lo = a < b ? a : b;
            int hi = a < b ? b : a;
            long key = ((long)lo << 32) | (uint)hi;
            if (edges.TryGetValue(key, out int idx))
            {
                if (et2[idx] < 0)
                    et2[idx] = tri;
            }
            else
            {
                edges[key] = ea.Count;
                ea.Add(lo);
                eb.Add(hi);
                et1.Add(tri);
                et2.Add(-1);
            }
        }

        for (int t = 0; t < tc; t++)
        {
            int i0 = sc.Weld[tris[t * 3]];
            int i1 = sc.Weld[tris[t * 3 + 1]];
            int i2 = sc.Weld[tris[t * 3 + 2]];
            sc.T0[t] = i0;
            sc.T1[t] = i1;
            sc.T2[t] = i2;
            AddEdge(i0, i1, t);
            AddEdge(i1, i2, t);
            AddEdge(i2, i0, t);
        }

        sc.EA = ea.ToArray();
        sc.EB = eb.ToArray();
        sc.ET1 = et1.ToArray();
        sc.ET2 = et2.ToArray();
    }

    private static long HashVert(Vector3 p)
    {
        int x = (int)Mathf.Round(p.x * 1000f) + 500000;
        int y = (int)Mathf.Round(p.y * 1000f) + 500000;
        int z = (int)Mathf.Round(p.z * 1000f) + 500000;
        return ((long)x * 1000003L + y) * 1000033L + z;
    }

    private static void PruneSilCache()
    {
        if (_silCache.Count > 0)
        {
            List<SkinnedMeshRenderer> gone = null;
            foreach (var k in _silCache.Keys)
                if (k == null) (gone ??= new List<SkinnedMeshRenderer>()).Add(k);
            if (gone != null)
                foreach (var k in gone) _silCache.Remove(k);
        }
        if (_silCacheMf.Count > 0)
        {
            List<MeshFilter> gone = null;
            foreach (var k in _silCacheMf.Keys)
                if (k == null) (gone ??= new List<MeshFilter>()).Add(k);
            if (gone != null)
                foreach (var k in gone) _silCacheMf.Remove(k);
        }
    }

    /// <summary>Top-left watermark: enabled toggles, transparent light-gray text.</summary>
    private static void DrawActiveHud(CheatController c)
    {
        var lines = new List<string>();
        foreach (var id in FeatureActions.All)
            if (FeatureActions.Get(id))
                lines.Add(ActionLabel(id));
        if (lines.Count == 0)
            return;

        for (int i = 0; i < lines.Count; i++)
        {
            var shadow = new Rect(11, 11 + i * 17, 320, 17);
            GUI.Label(shadow, $"<color=#000000>{lines[i]}</color>", _labelSmall);
            var main = new Rect(10, 10 + i * 17, 320, 17);
            GUI.Label(main, $"<color=#c8cdd0>{lines[i]}</color>", _labelSmall);
        }
    }

    /// <summary>Slot machine prediction HUD: shows the captured winning result for 6s.</summary>
    private static void DrawSlotsHud()
    {
        if (!Features.SlotPredict || SlotsPrediction.ResultText == null
            || Time.unscaledTime > SlotsPrediction.Until)
            return;

        string text = L.T("老虎机 · ", "老虎機 · ", "Slots · ") + SlotsPrediction.ResultText
            + "   (" + SlotsPrediction.RollerName + ")";
        var size = _label.CalcSize(new GUIContent(text));
        float w = size.x + 36, h = 34;
        var r = new Rect((Screen.width - w) / 2f, 134, w, h);
        DrawFill(r, CPanel);
        DrawFill(new Rect(r.x, r.yMax - 2, r.width, 2), CRouletteGreen);
        GUI.Label(new Rect(r.x, r.y + 6, r.width, 22), text, _label);
    }

    /// <summary>
    /// Crosshair that follows the gun's muzzle ray — the same line the laser sight
    /// points along — projected to screen. NOT the screen center: where the gun
    /// points is where bullets go, so the marker sits exactly there.
    /// </summary>
    private static void DrawCrosshair(CheatController c)
    {
        if (!Features.Crosshair || c.MenuOpen)
            return;
        var p = Player.LocalPlayer;
        if (p == null)
            return;
        var held = p.Holding != null ? p.Holding.HeldItem : null;
        var w = held != null ? held.Weapon : null;
        if (w == null || w.IsAds)
            return;
        var pc = p.Camera;
        if (pc == null || !pc.MouseLocked)
            return;
        var cam = GameInfo.CurCamera;
        if (cam == null)
            return;

        Vector2 sp;
        try
        {
            var fp = w.Attachments != null ? w.Attachments.FirePoint : null;
            Vector3 origin = fp != null ? fp.position : cam.transform.position;
            Vector3 dir = fp != null ? fp.forward : cam.transform.forward;

            Vector3 world = origin + dir * 250f;
            if (Physics.Raycast(origin, dir, out var hit, 250f,
                    GameInfo.ProjectileHitLayer, QueryTriggerInteraction.Ignore))
                world = hit.point;

            Vector3 s = cam.WorldToScreenPoint(world);
            if (s.z <= 0.1f)
                return; // pointing away from the camera view
            sp = new Vector2(s.x, Screen.height - s.y);
        }
        catch
        {
            return;
        }

        float cx = sp.x, cy = sp.y;
        var cc = EspColor(Features.CrosshairColor);
        var prev = GUI.color;
        GUI.color = cc;
        float dot = 2.5f, gap = Features.CrosshairGap, len = Features.CrosshairLen, t = 1.6f;
        GUI.DrawTexture(new Rect(cx - dot / 2, cy - dot / 2, dot, dot), Texture2D.whiteTexture);
        if (Features.CrosshairStyle == 0)
        {
            GUI.DrawTexture(new Rect(cx - t / 2, cy - gap - len, t, len), Texture2D.whiteTexture); // up
            GUI.DrawTexture(new Rect(cx - t / 2, cy + gap, t, len), Texture2D.whiteTexture);       // down
            GUI.DrawTexture(new Rect(cx - gap - len, cy - t / 2, len, t), Texture2D.whiteTexture); // left
            GUI.DrawTexture(new Rect(cx + gap, cy - t / 2, len, t), Texture2D.whiteTexture);       // right
        }
        GUI.color = prev;
    }

    // ────────────────────────── bind popup (neverlose style) ──────────────────────────

    private static string[] _bindPopup;      // actions shown in the popup
    private static string _bindPopupTitle;
    private static Rect _bindPopupRect = new Rect(200, 200, 230, 100);
    private static GUI.WindowFunction _bindPopupFn;

    private static void OpenBindPopup(string[] actions, Vector2 winLocalMouse)
    {
        _bindPopup = actions;
        _bindPopupTitle = BindTitle(actions);
        _bindPopupRect = new Rect(_win.x + winLocalMouse.x + 8, _win.y + winLocalMouse.y + 8, 320, 200);
        // height is corrected inside BindPopupFunc every frame (color section expands)
        _listening = null;
        _pickDrag = 0;
        _pickerAction = null;
        _pickerSlot = null;
        _pvSyncColor = -1; // force picker resync to this action's color
    }

    /// <summary>Extra settings block height for an action (drawn inside its popup).</summary>
    private static float ActionSettingsHeight(string action)
    {
        switch (action)
        {
            case "PlayerEsp": return 52;
            case "CreatureEsp": return 130;
            case "Crosshair": return 74;
            case "SilentAim": return 130;
            case "ItemEsp": return 104;
            default: return 0;
        }
    }

    /// <summary>Draw the extra settings block for an action inside its popup.</summary>
    private static void DrawActionSettings(string action, float y, float width, out float nextY)
    {
        nextY = y;
        switch (action)
        {
            case "PlayerEsp":
                PopupOptionRow(y, width, L.T("样式", "樣式", "Style"),
                    new[] { L.T("框线", "框線", "Full"), L.T("四角", "四角", "Corners"), L.T("填充", "填充", "Fill") },
                    ref Features.PlayerBoxStyle);
                PopupToggleRow(y + 30, width, L.T("名字标签", "名字標籤", "Name label"), ref Features.PlayerLabel);
                nextY = y + 52;
                break;
            case "CreatureEsp":
                PopupToggleRow(y, width, L.T("标签", "標籤", "Label"), ref Features.CreatureLabel);
                PopupToggleRow(y + 26, width, L.T("外框", "外框", "Frame"), ref Features.CreatureFrame);
                PopupToggleRow(y + 52, width, L.T("填充", "填充", "Fill"), ref Features.CreatureFill);
                PopupToggleRow(y + 78, width, L.T("轮廓", "輪廓", "Outline"), ref Features.CreatureSilhouette);
                PopupSliderRow(y + 104, width, L.T("显示距离", "顯示距離", "Distance"), ref Features.CreatureEspDist, 30f, 250f);
                nextY = y + 130;
                break;
            case "Crosshair":
                PopupOptionRow(y, width, L.T("样式", "樣式", "Style"),
                    new[] { L.T("十字", "十字", "Cross"), L.T("单点", "單點", "Dot") },
                    ref Features.CrosshairStyle);
                PopupSliderRow(y + 30, width, L.T("长度", "長度", "Length"), ref Features.CrosshairLen, 3f, 14f);
                PopupSliderRow(y + 52, width, L.T("间隙", "間隙", "Gap"), ref Features.CrosshairGap, 0f, 14f);
                nextY = y + 74;
                break;
            case "ItemEsp":
                PopupToggleRow(y, width, L.T("仅标签", "僅標籤", "Label"), ref Features.ItemLabel);
                PopupToggleRow(y + 26, width, L.T("外框", "外框", "Frame"), ref Features.ItemFrame);
                PopupToggleRow(y + 52, width, L.T("填充", "填充", "Fill"), ref Features.ItemFill);
                PopupSliderRow(y + 78, width, L.T("显示距离", "顯示距離", "Distance"), ref Features.ItemEspDist, 20f, 250f);
                Features.ItemEspStyle = Features.ItemFrame ? 1 : 0;
                nextY = y + 104;
                break;
            case "SilentAim":
                PopupToggleRow(y, width, L.T("玩家", "玩家", "Players"), ref Features.SilentAimPlayers);
                PopupToggleRow(y + 26, width, L.T("生物", "生物", "Creatures"), ref Features.SilentAimCreatures);
                PopupOptionRow(y + 52, width, L.T("优先", "優先", "Priority"),
                    new[]
                    {
                        L.T("准星", "準星", "Crosshair"),
                        L.T("距离", "距離", "Distance"),
                        L.T("血量", "血量", "HP"),
                    },
                    ref Features.SilentAimPriority);
                PopupToggleRow(y + 78, width, L.T("移动预判", "移動預判", "Prediction"), ref Features.SilentAimPredict);
                PopupSliderRow(y + 104, width, L.T("搜索锥角", "搜索錐角", "Cone"), ref Features.SilentAimAngle, 10f, 180f);
                Features.SilentAimTarget = Features.SilentAimPlayers
                    ? (Features.SilentAimCreatures ? 2 : 0)
                    : 1;
                nextY = y + 130;
                break;
        }
    }

    /// <summary>Mini toggle row for popups.</summary>
    private static void PopupToggleRow(float y, float width, string label, ref bool value)
    {
        GUI.Label(new Rect(10, y + 4, width - 70, 18), label, _label);
        var pill = new Rect(width - 58, y + 2, 44, 22);
        if (GUI.Button(pill, "", value ? _pillOn : _pillOff))
        {
            value = !value;
            Settings.MarkDirty();
        }
        DrawKnob(pill, value, true);
    }

    /// <summary>Mini segmented option picker for popups (returns true when changed).</summary>
    private static void PopupOptionRow(float y, float width, string label, string[] options, ref int index)
    {
        GUI.Label(new Rect(10, y + 4, 60, 18), label, _label);
        float bw = (width - 70 - 6 * options.Length) / options.Length + 6;
        for (int i = 0; i < options.Length; i++)
        {
            var br = new Rect(width - 10 - (options.Length - i) * (bw + 3), y + 2, bw, 22);
            if (GUI.Button(br, options[i], index == i ? _btnAccent : _btn) && index != i)
            {
                index = i;
                Settings.MarkDirty();
            }
        }
    }

    /// <summary>Mini label+slider row for popups.</summary>
    private static void PopupSliderRow(float y, float width, string label, ref float value, float min, float max)
    {
        GUI.Label(new Rect(10, y + 3, 60, 18), label, _label);
        var bar = new Rect(74, y + 5, width - 74 - 44, 14);
        float nv = GUI.HorizontalSlider(bar, value, min, max, _sliderBg, _sliderThumb);
        nv = Mathf.Round(nv * 10f) / 10f;
        if (!Mathf.Approximately(nv, value))
            Settings.MarkDirty();
        value = nv;
        GUI.Label(new Rect(width - 44, y + 3, 40, 18), $"{value:0.#}", _value);
    }

    /// <summary>Actions that own an ESP color (packed 0xRRGGBB); -1 = none.</summary>
    private static int GetActionColor(string action)
    {
        switch (action)
        {
            case "PlayerEsp": return Features.EspBoxColor;
            case "PlayerSkeleton": return Features.EspSkeletonColor;
            case "CreatureEsp": return Features.EspCreatureColor;
            case "ItemEsp": return Features.EspItemColor;
            case "Crosshair": return Features.CrosshairColor;
            default: return -1;
        }
    }

    private static void SetActionColor(string action, int rgb)
    {
        switch (action)
        {
            case "PlayerEsp": Features.EspBoxColor = rgb; break;
            case "PlayerSkeleton": Features.EspSkeletonColor = rgb; break;
            case "CreatureEsp": Features.EspCreatureColor = rgb; break;
            case "ItemEsp": Features.EspItemColor = rgb; break;
            case "Crosshair": Features.CrosshairColor = rgb; break;
        }
        Settings.MarkDirty();
    }

    private static string BindTitle(string[] actions)
    {
        return ActionLabel(actions[0]);
    }

    private static void DrawBindPopupWindow()
    {
        if (_bindPopup == null)
            return;
        if (_bindPopupFn == null)
            _bindPopupFn = id => BindPopupFunc(id);

        // precompute the full content height (the color section may be expanded)
        var slots = ColorSlots(_bindPopup[0]);
        bool hasColor = slots.Length > 0;
        bool expanded = _pickerSlot != null && _pickerAction == _bindPopup[0];
        float contentH = 28 + 14 + _bindPopup.Length * 30
            + (ActionSettingsHeight(_bindPopup[0]) > 0 ? 14 + ActionSettingsHeight(_bindPopup[0]) : 0)
            + (hasColor ? 14 + slots.Length * 22 + (expanded ? 132 : 0) : 0)
            + 30;
        _bindPopupRect.height = contentH;

        // keep the whole popup on screen
        _bindPopupRect.x = Mathf.Clamp(_bindPopupRect.x, 4, Screen.width - _bindPopupRect.width - 4);
        _bindPopupRect.y = Mathf.Clamp(_bindPopupRect.y, 4, Screen.height - _bindPopupRect.height - 4);

        // regular window + forced front — ModalWindow would swallow outside clicks,
        // which is exactly how the popup is supposed to close
        _bindPopupRect = GUI.Window(0x485447, _bindPopupRect, _bindPopupFn, "", _panel);
        GUI.BringWindowToFront(0x485447);
    }

    private static void BindPopupFunc(int id)
    {
        var w = _bindPopupRect;
        GUI.DragWindow(new Rect(0, 0, w.width, 22));

        GUI.Label(new Rect(10, 5, w.width - 20, 20), _bindPopupTitle, _title);
        float y = 28;

        GUI.Label(new Rect(10, y, w.width - 20, 12), L.T("绑定", "綁定", "BIND"), _hint);
        y += 14;
        foreach (var a in _bindPopup)
        {
            GUI.Label(new Rect(10, y + 5, 96, 20), ActionLabel(a), _label);
            bool listening = _listening == a;
            string txt = listening
                ? L.T("按任意键…", "按任意鍵…", "Press any key…")
                : KeyName(Hotkeys.Key(a));
            if (GUI.Button(new Rect(w.width - 122, y + 2, 112, 24), txt, listening ? _btnAccent : _btn))
            {
                if (listening)
                    _listening = null; // click again while listening → cancel
                else if (_listening == null)
                    _listening = a;
            }
            y += 30;
        }

        // per-feature settings (style, sizes, filters) — right inside the popup
        float setH = ActionSettingsHeight(_bindPopup[0]);
        if (setH > 0)
        {
            GUI.Label(new Rect(10, y, w.width - 20, 12), L.T("设置", "設定", "SETTINGS"), _hint);
            y += 14;
        }
        DrawActionSettings(_bindPopup[0], y, w.width - 20, out float setEnd);
        y = setEnd;

        // colors: one swatch per slot, click to expand the HSV picker for that slot
        var slots = ColorSlots(_bindPopup[0]);
        if (slots.Length > 0)
        {
            GUI.Label(new Rect(10, y, w.width - 20, 12), L.T("颜色", "顏色", "COLOR"), _hint);
            y += 14;
            foreach (var (slotLabel, slotId) in slots)
            {
                int packed = GetSlotColor(_bindPopup[0], slotId);
                var cCol = EspColor(packed);
                GUI.Label(new Rect(10, y + 4, w.width - 110, 18), slotLabel, _label);
                var sw = new Rect(w.width - 92, y + 2, 60, 18);
                var prevC = GUI.color;
                GUI.color = cCol;
                GUI.DrawTexture(sw, Texture2D.whiteTexture);
                GUI.color = prevC;
                DrawEspOutline(sw, new Color(1f, 1f, 1f, 0.7f), 1f);
                if (Event.current.type == EventType.MouseDown && sw.Contains(Event.current.mousePosition))
                {
                    // toggle: clicking the expanded slot collapses its picker
                    if (_pickerSlot == slotId && _pickerAction == _bindPopup[0])
                    {
                        _pickerSlot = null;
                        _pickerAction = null;
                    }
                    else
                    {
                        _pickerSlot = slotId;
                        _pickerAction = _bindPopup[0];
                    }
                    _pvSyncColor = -1;
                    Event.current.Use();
                }
                y += 22;
            }
            if (_pickerSlot != null && _pickerAction == _bindPopup[0])
            {
                DrawColorPicker(_bindPopup[0], _pickerSlot, y, w.width - 20);
                y += 132;
            }
        }

        // clear all binds shown in this popup
        if (GUI.Button(new Rect(10, y + 1, 92, 22), L.T("清除绑定", "清除綁定", "Clear binds"), _btn))
        {
            foreach (var a in _bindPopup)
                Hotkeys.Set(a, KeyCode.None);
            _listening = null;
        }

        GUI.Label(new Rect(110, y + 5, w.width - 120, 18),
            L.T("点击按钮后按新键 · Esc 取消", "點擊按鈕後按新鍵 · Esc 取消", "Click a button, press the new key · Esc cancels"), _hint);

        // dynamic popup height — the color section expands/collapses at runtime
        _bindPopupRect.height = y + 34;
    }

    /// <summary>Color slots (label + id) for an action — each gets its own swatch.</summary>
    private static (string label, string slot)[] ColorSlots(string action)
    {
        switch (action)
        {
            case "PlayerEsp":
                return new[]
                {
                    (L.T("框颜色", "框顏色", "Box color"), "box"),
                    (L.T("填充颜色", "填充顏色", "Fill color"), "fill"),
                };
            case "PlayerSkeleton":
                return new[] { (L.T("骨骼颜色", "骨骼顏色", "Skeleton color"), "main") };
            case "CreatureEsp":
                return new[]
                {
                    (L.T("生物颜色", "生物顏色", "Creature color"), "box"),
                    (L.T("生物填充", "生物填充", "Creature fill"), "cfill"),
                };
            case "ItemEsp":
                return new[]
                {
                    (L.T("物品颜色", "物品顏色", "Item color"), "ibox"),
                    (L.T("物品填充", "物品填充", "Item fill"), "ifill"),
                };
            case "Crosshair":
                return new[] { (L.T("准星颜色", "準星顏色", "Crosshair color"), "cross") };
            default:
                return Array.Empty<(string, string)>();
        }
    }

    private static int GetSlotColor(string action, string slot)
    {
        if (action == "PlayerEsp") return slot == "fill" ? Features.EspFillColor : Features.EspBoxColor;
        if (action == "PlayerSkeleton") return Features.EspSkeletonColor;
        if (action == "CreatureEsp") return slot == "cfill" ? Features.EspCreatureFillColor : Features.EspCreatureColor;
        if (action == "ItemEsp") return slot == "ifill" ? Features.EspItemFillColor : Features.EspItemColor;
        if (action == "Crosshair") return Features.CrosshairColor;
        return -1;
    }

    private static void SetSlotColor(string action, string slot, int rgb)
    {
        if (action == "PlayerEsp") { if (slot == "fill") Features.EspFillColor = rgb; else Features.EspBoxColor = rgb; }
        else if (action == "PlayerSkeleton") Features.EspSkeletonColor = rgb;
        else if (action == "CreatureEsp") { if (slot == "cfill") Features.EspCreatureFillColor = rgb; else Features.EspCreatureColor = rgb; }
        else if (action == "ItemEsp") { if (slot == "ifill") Features.EspItemFillColor = rgb; else Features.EspItemColor = rgb; }
        else if (action == "Crosshair") Features.CrosshairColor = rgb;
        Settings.MarkDirty();
    }

    // ────────────────── HSV color picker ──────────────────

    private static Texture2D _svTex, _hueTex;
    private static float _pvH, _pvS, _pvV;
    private static float _pickerH = -1f; // hue the SV texture was last built for
    private static int _pvSyncColor = -1;
    private static int _pickDrag; // 0 none, 1 hue bar, 2 SV square

    private static void EnsurePickerTextures()
    {
        if (_hueTex != null)
            return;
        _hueTex = new Texture2D(64, 1, TextureFormat.RGBA32, false);
        for (int x = 0; x < 64; x++)
            _hueTex.SetPixel(x, 0, Color.HSVToRGB(x / 63f, 1f, 1f));
        _hueTex.Apply();
        _hueTex.wrapMode = TextureWrapMode.Clamp;
        _hueTex.hideFlags = HideFlags.HideAndDontSave;
    }

    private static void BuildSvTexture()
    {
        if (_svTex == null)
        {
            _svTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            _svTex.wrapMode = TextureWrapMode.Clamp;
            _svTex.hideFlags = HideFlags.HideAndDontSave;
            _pickerH = -1f;
        }
        if (Mathf.Approximately(_pickerH, _pvH))
            return;
        _pickerH = _pvH;
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                _svTex.SetPixel(x, y, Color.HSVToRGB(_pvH, x / 63f, y / 63f)); // top row = bright (v=1)
        _svTex.Apply();
    }

    private static void ApplyPicker(string act, string slot)
    {
        var c = Color.HSVToRGB(_pvH, _pvS, _pvV);
        int packed = (Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255) << 16)
                   | (Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255) << 8)
                   | Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255);
        SetSlotColor(act, slot, packed);
        _pvSyncColor = packed;
    }

    private static void DrawColorPicker(string act, string slot, float y, float width)
    {
        int packed = GetSlotColor(act, slot);
        if (packed != _pvSyncColor)
        {
            var c = EspColor(packed);
            Color.RGBToHSV(new Color(c.r, c.g, c.b), out _pvH, out _pvS, out _pvV);
            _pvSyncColor = packed;
        }
        EnsurePickerTextures();
        BuildSvTexture();

        float cy = y;

        // preset swatches
        float sw = 22;
        for (int i = 0; i < Palette.Length; i++)
        {
            var sr = new Rect(10 + i * (sw + 5), cy, sw, 18);
            var prev = GUI.color;
            GUI.color = new Color(((Palette[i] >> 16) & 255) / 255f, ((Palette[i] >> 8) & 255) / 255f, (Palette[i] & 255) / 255f);
            GUI.DrawTexture(sr, Texture2D.whiteTexture);
            GUI.color = prev;
            if (Event.current.type == EventType.MouseDown && sr.Contains(Event.current.mousePosition))
            {
                SetSlotColor(act, slot, Palette[i]);
                Event.current.Use();
            }
        }
        cy += 26;

        // saturation/value square
        var svRect = new Rect(10, cy, width, 84);
        GUI.DrawTexture(svRect, _svTex);
        cy += svRect.height + 5;

        // hue bar
        var hueRect = new Rect(10, cy, width, 12);
        GUI.DrawTexture(hueRect, _hueTex);

        // interaction
        var e = Event.current;
        if (e.type == EventType.MouseDown)
        {
            if (svRect.Contains(e.mousePosition)) { _pickDrag = 2; e.Use(); }
            else if (hueRect.Contains(e.mousePosition)) { _pickDrag = 1; e.Use(); }
        }
        else if (e.type == EventType.MouseDrag && _pickDrag != 0)
        {
            e.Use();
        }
        else if (e.type == EventType.MouseUp && _pickDrag != 0)
        {
            _pickDrag = 0;
            e.Use();
        }

        if (_pickDrag == 2)
        {
            _pvS = Mathf.Clamp01((e.mousePosition.x - svRect.x) / svRect.width);
                _pvV = Mathf.Clamp01(1f - (e.mousePosition.y - svRect.y) / svRect.height);
                ApplyPicker(act, slot);
        }
        else if (_pickDrag == 1)
        {
                _pvH = Mathf.Clamp01((e.mousePosition.x - hueRect.x) / hueRect.width);
                ApplyPicker(act, slot);
        }

        // position indicators
        DrawEspOutline(new Rect(svRect.x + _pvS * svRect.width - 3, svRect.y + (1f - _pvV) * svRect.height - 3, 6, 6), Color.white);
        DrawEspOutline(new Rect(hueRect.x + _pvH * hueRect.width - 2, hueRect.y - 1, 4, hueRect.height + 2), Color.white);
    }

    /// <summary>Esc layering while the menu is open: cancel listening → close the
    /// bind popup → (caller closes the menu). Returns true when a layer was closed.</summary>
    public static bool HandleEscape()
    {
        if (_listening != null)
        {
            _listening = null;
            return true;
        }
        if (_bindPopup != null)
        {
            _bindPopup = null;
            return true;
        }
        return false;
    }

    /// <summary>Global capture for the "press any key" state (works for both the
    /// Keys tab and the right-click popup, inside or outside the menu window).</summary>
    private static void HandleBindCapture()
    {
        if (_listening == null)
            return;
        var ev = Event.current;
        if (ev == null)
            return;
        if (ev.type == EventType.KeyDown && ev.keyCode != KeyCode.None)
        {
            if (ev.keyCode != KeyCode.Escape)
                Hotkeys.Set(_listening, ev.keyCode);
            _listening = null;
            ev.Use();
        }
        else if (ev.type == EventType.MouseDown)
        {
            // Inside the bind popup only LEFT clicks are UI interaction (buttons);
            // middle/right clicks still count as key captures (scroll-wheel binds).
            if (ev.button == 0 && _bindPopup != null && _bindPopupRect.Contains(ev.mousePosition))
                return;

            KeyCode mk = ev.button switch
            {
                0 => KeyCode.Mouse0,
                1 => KeyCode.Mouse1,
                2 => KeyCode.Mouse2,
                _ => KeyCode.None,
            };
            if (mk != KeyCode.None)
                Hotkeys.Set(_listening, mk);
            _listening = null;
            ev.Use();
        }
    }

    // ────────────────────────── island labels ──────────────────────────

    private static RadarUI _radarRef;
    private static Color[] _islandColors;
    private static bool _colorsTried;

    /// <summary>Cache each island's dot color from the radar UI (1:1 with radar dots).</summary>
    private static void TryCacheIslandColors()
    {
        if (_colorsTried)
            return;
        _colorsTried = true;
        try
        {
            var radar = UnityEngine.Object.FindFirstObjectByType<RadarUI>();
            if (radar == null)
            {
                _colorsTried = false; // radar not spawned yet, retry next frame
                return;
            }
            _radarRef = radar;
            var dots = AccessTools.Field(typeof(RadarUI), "_islandDots")?.GetValue(radar) as Array;
            var fiDot = AccessTools.Field(typeof(MapDot), "_dot");
            if (dots == null || fiDot == null)
                return;

            var list = new List<Color>();
            foreach (var d in dots)
            {
                var c = Color.white;
                var img = fiDot.GetValue(d) as Component;
                if (img != null)
                {
                    var p = img.GetType().GetProperty("color");
                    if (p != null)
                        c = (Color)p.GetValue(img);
                }
                list.Add(new Color(c.r, c.g, c.b, 1f)); // radar hides dots via alpha; force opaque
            }
            _islandColors = list.ToArray();
        }
        catch { }
    }

    private static void DrawIslandLabels()
    {
        if (!Features.IslandLabels)
            return;
        var cam = GameInfo.CurCamera;
        if (cam == null || Player.LocalPlayer == null)
            return;

        TryCacheIslandColors();

        try
        {
            Vector2 playerXZ = new Vector2(Player.LocalPlayer.Transform.position.x,
                Player.LocalPlayer.Transform.position.z);
            float onIsland = Island.IslandSize * 0.8f;

            int max = OnlineIslandManager.MaxIslandUnlocked;
            for (int i = 0; i < max; i++)
            {
                Vector3 pos;
                try
                {
                    var info = IslandManager.GetIslandInfo(i);
                    if (info == null)
                        continue;
                    pos = info.IslandPosition;
                }
                catch { continue; }

                // Player stands on this island → skip its own label
                if ((new Vector2(pos.x, pos.z) - playerXZ).sqrMagnitude < onIsland * onIsland)
                    continue;

                Color col = _islandColors != null && i < _islandColors.Length
                    ? _islandColors[i]
                    : new Color(0.5f, 0.83f, 1f, 1f);
                string hex = ColorUtility.ToHtmlStringRGB(col);
                string label = L.T("岛 ", "島 ", "Isle ") + (i + 1) + "  " +
                    Vector3.Distance(cam.transform.position, pos).ToString("0") + "m";

                Vector3 vp = cam.WorldToViewportPoint(pos + Vector3.up * 12f);
                bool onScreen = vp.z > 0f && vp.x > 0.02f && vp.x < 0.98f && vp.y > 0.06f && vp.y < 0.94f;

                if (onScreen)
                {
                    var sp = new Vector2(vp.x * Screen.width, Screen.height - vp.y * Screen.height);
                    var prev = GUI.color;
                    GUI.color = col;
                    GUI.DrawTexture(new Rect(sp.x - 3, sp.y + 9, 6, 6), Texture2D.whiteTexture);
                    GUI.color = prev;
                    GUI.Label(new Rect(sp.x - 42, sp.y - 12, 130, 18),
                        $"<color=#{hex}>{label}</color>", _label);
                }
                else
                {
                    // Off-screen: project the direction to the screen edge
                    // (mirror-flip when the point is behind the camera)
                    float x = vp.x, y = vp.y;
                    if (vp.z < 0f) { x = 1f - x; y = 1f - y; }
                    Vector2 dir = new Vector2(x - 0.5f, y - 0.5f);
                    if (dir.sqrMagnitude < 1e-6f)
                        dir = Vector2.up;
                    dir.Normalize();

                    float rad = Mathf.Min(Screen.width, Screen.height) * 0.42f;
                    var c = new Vector2(Screen.width / 2f, Screen.height / 2f) + dir * rad;

                    var prev = GUI.color;
                    GUI.color = col;
                    GUI.DrawTexture(new Rect(c.x - 4, c.y - 4, 8, 8), Texture2D.whiteTexture);
                    GUI.color = prev;
                    GUI.Label(new Rect(c.x - 55, c.y + 6, 110, 18),
                        $"<color=#{hex}>{label}</color>", _label);
                }
            }
        }
        catch { /* scene transition moment, ignore */ }
    }

    // ────────────────────────── player ESP ──────────────────────────

    /// <summary>ESP preset palette (packed 0xRRGGBB, picked via swatches in the bind popup).</summary>
    private static readonly int[] Palette =
    {
        0xF24C4C, // red
        0x4CE65E, // green
        0x5C8DF2, // blue
        0xE0B85C, // gold
        0xF2994C, // orange
        0xCC73F2, // purple
        0xF2DB4C, // yellow
        0xF2F2F2, // white
    };

    private static Color EspColor(int rgb)
    {
        return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 0.9f);
    }

    private static GUIStyle _espLabel;
    private static int _espLabelBuilt = -1;

    /// <summary>Shared ESP label style, rebuilt when the label size setting changes.</summary>
    private static GUIStyle EspLabel()
    {
        if (_espLabel == null || _espLabelBuilt != Features.LabelSize)
        {
            _espLabel = Style(null, CText, Mathf.Clamp(Features.LabelSize, 9, 24), rich: true);
            _espLabel.alignment = TextAnchor.MiddleCenter;
            _espLabelBuilt = Features.LabelSize;
        }
        return _espLabel;
    }

    private static void DrawPlayerEsp()
    {
        bool any = Features.PlayerEsp || Features.PlayerSkeleton || Features.PlayerHealth;
        if (!any)
            return;
        var cam = GameInfo.CurCamera;
        if (cam == null || Player.LocalPlayer == null)
            return;

        foreach (var pl in PlayerManager.AlivePlayers)
        {
            if (pl == null || pl == Player.LocalPlayer)
                continue;
            try
            {
                if (!Combat.GetPlayerBounds(pl, out var b))
                    continue;
                if (!ProjectBounds(b, cam, out var r, out float dist))
                    continue;
                if (dist > 250f)
                    continue;

                if (Features.PlayerSkeleton)
                    DrawSkeleton(pl, cam);

                if (Features.PlayerEsp)
                {
                    var bc = EspColor(Features.EspBoxColor);
                    bc.a = Mathf.Clamp01(Features.EspBoxAlpha);
                    if (Features.PlayerBoxStyle == 2)
                    {
                        var fc = EspColor(Features.EspFillColor);
                        fc.a = Mathf.Clamp01(Features.EspFillAlpha);
                        DrawEspFilled(r, bc, fc, Features.EspOutlineWidth);
                    }
                    else
                        DrawEspFrame(r, bc, Features.PlayerBoxStyle == 1);

                    if (Features.PlayerLabel)
                    {
                        string name = (pl.SteamName ?? L.T("玩家", "玩家", "Player")).Replace("<", "");
                        DrawEspNameplate(cam, b, name, dist, bc, 1f);
                    }
                }

                if (Features.PlayerHealth)
                {
                    float hpOff = (Features.PlayerEsp && Features.PlayerLabel)
                        ? Mathf.Clamp(Features.LabelSize, 9, 24) + 6f : 0f;
                    DrawHealthBar(new Rect(r.x, r.y - hpOff, r.width, r.height), GetHealth(pl));
                }
            }
            catch { /* player just left, ignore */ }
        }
    }

    /// <summary>Health (0-100) of a player; -1 when unavailable.</summary>
    private static int GetHealth(Player pl)
    {
        try { return pl.Vitals != null ? pl.Vitals._syncedHealth.Value : -1; }
        catch { return -1; }
    }

    /// <summary>Small health bar floating above the box, green→red by remaining HP.</summary>
    private static void DrawHealthBar(Rect box, int health)
    {
        if (health < 0)
            return;
        float w = Mathf.Max(box.width * 0.7f, 30f);
        float h = 4f;
        float x = box.center.x - w / 2f;
        float y = box.y - 10f;
        DrawFill(new Rect(x - 1, y - 1, w + 2, h + 2), new Color(0, 0, 0, 0.7f));
        float frac = Mathf.Clamp01(health / 100f);
        var col = Color.Lerp(new Color(0.92f, 0.25f, 0.25f), new Color(0.30f, 0.90f, 0.35f), frac);
        if (frac > 0.001f)
            DrawFill(new Rect(x, y, w * frac, h), col);
    }

    /// <summary>Skeleton ESP: a line from every bone to its parent bone (any rig).</summary>
    private static void DrawSkeleton(Player pl, Camera cam)
    {
        try
        {
            var body = pl.Body;
            if (body == null)
                return;
            var newChar = AccessTools.Field(typeof(PlayerBody), "_newCharacter")?.GetValue(body) as GameObject;
            if (newChar == null || !newChar.activeInHierarchy)
                return;
            var smr = newChar.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null)
                return;
            var bones = smr.bones;
            if (bones == null || bones.Length == 0)
                return;

            var boneSet = new HashSet<Transform>(bones);
            var col = EspColor(Features.EspSkeletonColor);
            var prev = GUI.color;
            GUI.color = col;

            foreach (var bone in bones)
            {
                if (bone == null || bone.parent == null || !boneSet.Contains(bone.parent))
                    continue;

                Vector3 sa = cam.WorldToScreenPoint(bone.position);
                Vector3 sb = cam.WorldToScreenPoint(bone.parent.position);
                if (sa.z <= 0.1f || sb.z <= 0.1f)
                    continue;

                DrawLine(new Vector2(sa.x, Screen.height - sa.y),
                         new Vector2(sb.x, Screen.height - sb.y), col);
            }

            GUI.color = prev;
        }
        catch { }
    }

    private static Material _lineMat;

    private static void EnsureLineMat()
    {
        if (_lineMat != null)
            return;
        _lineMat = new Material(Shader.Find("Hidden/Internal-Colored"));
        _lineMat.hideFlags = HideFlags.HideAndDontSave;
        _lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _lineMat.SetInt("_ZWrite", 0);
    }
    /// <summary>1px GL line in GUI pixel space (IMGUI has no line primitive).</summary>
    private static void DrawLine(Vector2 a, Vector2 b, Color c)
    {
        EnsureLineMat();
        _lineMat.SetPass(0);
        GL.PushMatrix();
        GL.LoadPixelMatrix();
        GL.Begin(GL.LINES);
        GL.Color(c);
        GL.Vertex3(a.x, a.y, 0);
        GL.Vertex3(b.x, b.y, 0);
        GL.End();
        GL.PopMatrix();
    }

    private static void DrawEspOutline(Rect r, Color color, float width = 1f)
    {
        var prev = GUI.color;
        GUI.color = color;
        float t = Mathf.Clamp(width, 1f, 2f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    /// <summary>
    /// Soft AABB fill. Near / large boxes fade so they don't paint over the model;
    /// tiny far boxes stay a touch more visible.
    /// </summary>
    private static void DrawEspSoftFill(Rect r, Color fill)
    {
        float area = r.width * r.height;
        float cover = area / Mathf.Max(1f, Screen.width * (float)Screen.height);
        float fa = Mathf.Clamp(fill.a, 0f, 0.18f) * Mathf.Clamp01(1.08f - cover * 18f);
        if (fa < 0.012f)
            return;
        var prev = GUI.color;
        GUI.color = new Color(fill.r, fill.g, fill.b, fa);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    /// <summary>Translucent fill + outline. Fill alpha is respected and capped so it stays quiet.</summary>
    private static void DrawEspFilled(Rect r, Color outline, Color fill, float width = 1f)
    {
        DrawEspSoftFill(r, fill);
        DrawEspOutline(r, outline, width);
    }

    /// <summary>Corner-only box — stays readable on large close objects without covering them.</summary>
    private static void DrawEspCorners(Rect r, Color color)
    {
        var prev = GUI.color;
        GUI.color = color;
        float t = 1.2f;
        float len = Mathf.Clamp(Mathf.Min(r.width, r.height) * 0.28f, 5f, 16f);
        len4(r, t, len);
        GUI.color = prev;
    }

    private static void len4(Rect r, float t, float len)
    {
        // 4 corners, 8 segments
        GUI.DrawTexture(new Rect(r.x, r.y, len, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, t, len), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - len, r.y, len, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, len), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, len, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - len, t, len), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - len, r.yMax - t, len, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - t, r.yMax - len, t, len), Texture2D.whiteTexture);
    }

    // ────────────────────────── roulette prediction HUD ──────────────────────────

    private static readonly Color CRouletteRed = new(0.82f, 0.18f, 0.18f, 1f);
    private static readonly Color CRouletteBlack = new(0.09f, 0.09f, 0.10f, 1f);
    private static readonly Color CRouletteGreen = new(0.13f, 0.68f, 0.25f, 1f);

    private static void DrawRouletteHud()
    {
        if (!Roulette.Active)
            return;

        BetColor col = Roulette.Color;
        float dwell = Roulette.Dwell;
        bool settled = dwell >= 2f;

        Color chip = col == BetColor.Red ? CRouletteRed
                   : col == BetColor.Black ? CRouletteBlack
                   : CRouletteGreen;
        string name = col == BetColor.Red ? L.T("红", "紅", "Red")
                    : col == BetColor.Black ? L.T("黑", "黑", "Black")
                    : L.T("绿 (×35)", "綠 (×35)", "Green (×35)");
        string status = settled
            ? L.T("已落定 ✓", "已落定 ✓", "Settled ✓")
            : string.Format(L.T("稳定 {0:0.0} / 2.0s", "穩定 {0:0.0} / 2.0s", "Settle {0:0.0} / 2.0s"), dwell);

        float w = 240, h = 44;
        float x = (Screen.width - w) / 2f;
        float y = 90;
        var r = new Rect(x, y, w, h);

        DrawFill(r, CPanel);
        DrawFill(new Rect(r.x, r.y, 3, r.height), chip);

        // color chip
        var chipRect = new Rect(r.x + 12, r.y + 9, 26, 26);
        DrawFill(chipRect, chip);
        if (col == BetColor.Black)
            DrawFill(new Rect(chipRect.x, chipRect.y, chipRect.width, 1.5f), CTextDim);

        GUI.Label(new Rect(r.x + 48, r.y + 4, 130, 20), L.T("轮盘预测 · ", "輪盤預測 · ", "Roulette · ") + name, _label);
        GUI.Label(new Rect(r.x + 48, r.y + 23, 150, 16), status, settled ? _value : _labelDim);
    }

    // ────────────────────────── window ──────────────────────────

    private static void WindowFunc(int id, CheatController c)
    {
        var w = new Rect(0, 0, WinW, WinH);

        // draggable title bar
        GUI.DragWindow(new Rect(0, 0, w.width, 36));

        // Language dropdown event handling — processed BEFORE other widgets so that
        // clicks on popup rows never leak to the controls visually underneath.
        var evt = Event.current;
        if (_langOpen && evt.type == EventType.MouseDown)
        {
            var pr = LangPopupRect(w.width, w.height);
            if (pr.Contains(evt.mousePosition))
            {
                int idx = Mathf.Clamp((int)((evt.mousePosition.y - pr.y - 4) / 24f), 0, LangKeys.Length - 1);
                L.ApplyKey(LangKeys[idx]);
                _langOpen = false;
                evt.Use();
            }
            else if (!LangBtnRect(w.width, w.height).Contains(evt.mousePosition))
            {
                _langOpen = false; // outside click closes the dropdown, event passes through
            }
        }

        float x = 14, y = 6;

        // title
        GUI.Label(new Rect(x, y, 220, 24), L.T("◆ 鱼塘改造器", "◆ 魚塘改造器", "◆ How to Fish Mod"), _title);
        string host = Features.IsHost ? L.T("主机", "主機", "Host")
            : (Multiplayer.HostModOnline ? L.T("联机桥", "聯機橋", "Bridge") : L.T("客户端", "客戶端", "Client"));
        var chipStyle = Features.CanHostAct ? _chipOn : _chip;
        var size = chipStyle.CalcSize(new GUIContent(host));
        GUI.Label(new Rect(w.width - 14 - size.x, y + 3, size.x, 20), host, chipStyle);

        y += 30;

        // content: left nav sidebar + right page area (scrollable)
        float bodyY = y + 8;
        float bodyH = w.height - bodyY - 26;
        var navRect = new Rect(x, bodyY, NavW, bodyH);
        float pageX = x + NavW + NavGap;
        var pageRect = new Rect(pageX, bodyY, w.width - pageX - 14, bodyH);

        var pages = Pages;
        for (int i = 0; i < pages.Count; i++)
        {
            var nr = new Rect(navRect.x, navRect.y + i * 32, navRect.width, 28);
            bool on = _page == i;
            if (on)
                DrawFill(nr, CAccentDim);
            if (GUI.Button(nr, pages[i].Label, on ? _tabOn : _tabOff) && _page != i)
            {
                _page = i;
                _bindPopup = null; // switching pages closes the settings popup
                _listening = null;
            }
        }

        GUILayout.BeginArea(pageRect);
        _tabScroll = GUILayout.BeginScrollView(_tabScroll, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        pages[_page].Draw(c);
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        // bottom bar: hint (left) + language dropdown (right)
        string hint = Features.IsHost
            ? L.T("INSERT 开关菜单 · 你是主机,全部功能可用",
                  "INSERT 開關選單 · 你是主機,全部功能可用",
                  "INSERT — menu · Host: everything works")
            : Multiplayer.HostModOnline
                ? L.T("INSERT 开关菜单 · 联机桥已连接,全功能可用",
                      "INSERT 開關選單 · 聯機橋已連線,全功能可用",
                      "INSERT — menu · Host mod connected: everything works")
                : L.T("INSERT 开关菜单 · 房主未装mod:战斗页本地功能可用,其余置灰",
                      "INSERT 開關選單 · 房主未裝mod:戰鬥頁本地功能可用,其餘置灰",
                      "INSERT — menu · Host has no mod: combat page only");
        GUI.Label(new Rect(14, w.height - 24, w.width - 128, 18), hint, _hint);

        string langKey = L.SettingKey(L.Setting);
        if (GUI.Button(LangBtnRect(w.width, w.height), LangDisplayName(langKey) + " ▾", _chip))
            _langOpen = !_langOpen;

        DrawLangDropdown(w.width, w.height);
    }

    // ────────────────────────── language dropdown ──────────────────────────

    private static bool _langOpen;
    private static readonly string[] LangKeys = { "auto", "zh-hans", "zh-hant", "en" };

    private static Rect LangBtnRect(float winW, float winH)
    {
        return new Rect(winW - 108, winH - 27, 96, 21);
    }

    private static Rect LangPopupRect(float winW, float winH)
    {
        var b = LangBtnRect(winW, winH);
        return new Rect(b.x + b.width - 130f, b.y - 104f - 4f, 130f, 104f);
    }

    /// <summary>"Auto" is localized; the concrete language names stay in their own language.</summary>
    private static string LangDisplayName(string key)
    {
        return key == "auto" ? L.T("自动", "自動", "Auto") : L.DisplayName(key);
    }

    private static void DrawLangDropdown(float winW, float winH)
    {
        if (!_langOpen)
            return;

        var pr = LangPopupRect(winW, winH);
        DrawFill(pr, CPanel);
        DrawFill(new Rect(pr.x, pr.y, pr.width, 1.5f), CAccent);

        string cur = L.SettingKey(L.Setting);
        for (int i = 0; i < LangKeys.Length; i++)
        {
            var rr = new Rect(pr.x + 4, pr.y + 4 + i * 24, pr.width - 8, 22);
            bool sel = LangKeys[i] == cur;
            bool hover = rr.Contains(Event.current.mousePosition);
            DrawFill(rr, sel ? CAccentDim : (hover ? CRowHover : CRow));
            GUI.Label(new Rect(rr.x + 8, rr.y + 3, rr.width - 12, 18),
                LangDisplayName(LangKeys[i]) + (sel ? "  ✓" : ""), sel ? _tabOn : _labelSmall);
        }
    }

    private static void ClampWindow()
    {
        _win.x = Mathf.Clamp(_win.x, -WinW + 60, Screen.width - 60);
        _win.y = Mathf.Clamp(_win.y, 0, Screen.height - 40);
    }

    // ────────────────────────── tab: player ──────────────────────────

    private static void TabPlayer(CheatController c)
    {
        BeginSection(L.T("生存", "生存", "Survival"));
        ToggleRow(L.T("上帝模式", "上帝模式", "God mode"),
            L.T("免疫所有伤害(火/毒/溺水/攻击)", "免疫所有傷害(火/毒/溺水/攻擊)", "Immune to fire, poison, drowning and hits"),
            Features.GodMode, true, v => FeatureActions.Apply("GodMode", v),
            new[] { "GodMode" });
        ToggleRow(L.T("锁满饱食度", "鎖滿飽食度", "No hunger"),
            L.T("不掉饱食 / 中毒 / 着火", "不掉飽食 / 中毒 / 著火", "Hunger, poison and fire stay frozen"),
            Features.FullnessLock, Features.CanHostAct, v => FeatureActions.Apply("FullnessLock", v),
            new[] { "FullnessLock" });
        ToggleRow(L.T("保留物品栏", "保留物品欄", "Keep inventory"),
            L.T("死亡/复活不掉落物品", "死亡/復活不掉落物品", "Nothing drops when you die"),
            Features.KeepInventory, true, v => FeatureActions.Apply("KeepInventory", v),
            new[] { "KeepInventory" });
        EndSection();

        BeginSection(L.T("运动", "運動", "Movement"));
        ToggleRow(L.T("玩家飞行", "玩家飛行", "Flight"),
            L.T("Space 升 / Ctrl 降,悬停;开船时自动让位", "Space 升 / Ctrl 降,懸停;開船時自動讓位", "Space up / Ctrl down, hover. Pauses while driving"),
            Features.PlayerFlight, true, v => FeatureActions.Apply("PlayerFlight", v),
            new[] { "PlayerFlight", Hotkeys.PlayerFlightUp, Hotkeys.PlayerFlightDown });
        SliderRow(L.T("移速倍率", "移速倍率", "Move speed"), ref Features.SpeedMult, 0.3f, 5f, "x");
        SliderRow(L.T("跳跃倍率", "跳躍倍率", "Jump height"), ref Features.JumpMult, 0.3f, 5f, "x");
        EndSection();

        BeginSection(L.T("视角", "視角", "Camera"));
        ToggleRow(L.T("第三人称", "第三人稱", "Third person"),
            L.T("相机拉到角色身后跟随视角", "相機拉到角色身後跟隨視角", "See your own character (clones a friend's body)"),
            Features.ThirdPerson, true, v => FeatureActions.Apply("ThirdPerson", v),
            new[] { "ThirdPerson" });
        SliderRow(L.T("第三人称距离", "第三人稱距離", "Camera distance"), ref Features.ThirdPersonDist, 0.5f, 8f, "m");
        SliderRow(L.T("FOV 倍率", "FOV 倍率", "FOV"), ref Features.FovMult, 0.5f, 2f, "x");
        EndSection();

        BeginSection(L.T("传送到岛屿", "傳送到島嶼", "Islands"));
        int n = Features.RealIslandCount();
        const float bs = 34f, gap = 5f, per = 6f;
        int rows = Mathf.CeilToInt(n / per);
        var grid = GUILayoutUtility.GetRect(0, rows * (bs + 4), GUILayout.ExpandWidth(true));
        float cell = (grid.width - (per - 1) * gap) / per;
        for (int i = 0; i < n; i++)
        {
            int row = i / (int)per, col = i % (int)per;
            var br = new Rect(grid.x + col * (cell + gap), grid.y + row * (bs + 4), cell, bs);
            int island = i + 1;
            if (GUI.Button(br, island.ToString(), _btn))
            {
                if (Features.TeleportToIsland(island))
                    c.ShowToast(L.T("全员传送至 ", "全員傳送至 ", "Everyone warped to island ") + island);
                else
                    c.ShowToast(L.T("需要主机", "需要主機", "Host required"));
            }
        }
        Hint(L.T("走游戏原生换岛流程,会带上所有玩家(需主机)。", "走遊戲原生換島流程,會帶上所有玩家(需主機)。", "Uses the game's island swap — brings everyone (host only)."));
        EndSection();

        BeginSection(L.T("传送到玩家", "傳送到玩家", "Teleport to player"));
        bool anyTp = false;
        if (Player.LocalPlayer != null)
        {
            foreach (var pl in PlayerManager.AlivePlayers)
            {
                if (pl == null || pl == Player.LocalPlayer)
                    continue;
                anyTp = true;
                string nm = (pl.SteamName ?? L.T("玩家", "玩家", "Player")).Replace("<", "");
                var captured = pl;
                ButtonRow("→ " + nm, null, true, () =>
                {
                    if (Features.TeleportToPlayer(captured))
                        c.ShowToast(L.T("已传送到 ", "已傳送到 ", "Teleported to ") + nm);
                }, null);
            }
        }
        if (!anyTp)
            Hint(L.T("附近没有其他玩家。", "附近沒有其他玩家。", "No other players around."));
        EndSection();
    }

    // ────────────────────────── tab: money ──────────────────────────

    private static string _moneyAmt = "99999";

    private static void TabMoney(CheatController c)
    {
        BeginSection(L.T("钱包", "錢包", "Wallet"));
        if (!_fold)
        {
            GUILayout.Label(L.T("现金", "現金", "Cash") + $"  <color=#3DE0D1>${MoneyManager.Money:N0}</color>", _label);
            GUILayout.Label(L.T("欧元", "歐元", "Euros") + $"  <color=#3DE0D1>€{MoneyManager.Euro:N0}</color>", _label);
            GUILayout.Space(6);
        }
        EndSection();

        BeginSection(L.T("加钱", "加錢", "Add Money"));
        ButtonRow("+$99,999", "€999", Features.CanHostAct, () =>
        {
            if (Features.HostAct(CheatAction.AddMoney, amount: 99999, currency: (byte)Currency.Dollar))
                c.ShowToast(L.T("已加 $99,999", "已加 $99,999", "Added $99,999"));
        }, () =>
        {
            if (Features.HostAct(CheatAction.AddMoney, amount: 999, currency: (byte)Currency.Euro))
                c.ShowToast(L.T("已加 €999", "已加 €999", "Added €999"));
        });
        if (!_fold)
        {
            var r = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
            GUI.Label(new Rect(r.x, r.y + 6, 54, 20), L.T("数量", "數量", "Amount"), _label);
            _moneyAmt = GUI.TextField(new Rect(r.x + 58, r.y + 4, 90, 24), _moneyAmt, 9, _btn);
            bool ok = int.TryParse(_moneyAmt, out int amt) && amt != 0 && Features.CanHostAct;
            DrawButton(new Rect(r.xMax - 132, r.y + 2, 62, 26), "+$", ok, () =>
            {
                if (Features.HostAct(CheatAction.AddMoney, amount: amt, currency: (byte)Currency.Dollar))
                    c.ShowToast(L.T("已加 $", "已加 $", "Added $") + amt.ToString("N0"));
            });
            DrawButton(new Rect(r.xMax - 66, r.y + 2, 62, 26), "+€", ok, () =>
            {
                if (Features.HostAct(CheatAction.AddMoney, amount: amt, currency: (byte)Currency.Euro))
                    c.ShowToast(L.T("已加 €", "已加 €", "Added €") + amt.ToString("N0"));
            });
        }
        EndSection();

        BeginSection(L.T("开关", "開關", "Toggles"));
        ToggleRow(L.T("游戏内置作弊", "遊戲內置作弊", "Game cheats (M/N)"),
            L.T("启用后 M=+$99999 / N=-$99999 / Ctrl+M=+€999", "啟用後 M=+$99999 / N=-$99999 / Ctrl+M=+€999", "The game's built-in money keys"),
            Features.BuiltInCheats, Features.CanHostAct, v =>
        {
            if (Features.HostAct(CheatAction.BuiltInCheats, flag: v))
            {
                Features.BuiltInCheats = v;
                c.ShowToast(v
                    ? L.T("内置作弊已启用 (M/N 键)", "內置作弊已啟用 (M/N 鍵)", "Built-in cheats ON (M/N keys)")
                    : L.T("内置作弊已关闭", "內置作弊已關閉", "Built-in cheats OFF"));
            }
        }, new[] { "BuiltInCheats" });
        EndSection();
    }

    // ────────────────────────── tab: visual ──────────────────────────

    private static void TabVisual(CheatController c)
    {
        BeginSection(L.T("标签", "標籤", "Labels"));
        float lblSize = Features.LabelSize;
        SliderRow(L.T("标签大小", "標籤大小", "Label size"), ref lblSize, 10f, 20f, "px");
        int nv = Mathf.RoundToInt(lblSize);
        if (nv != Features.LabelSize)
        {
            Features.LabelSize = nv;
            Settings.MarkDirty();
        }
        EndSection();

        BeginSection(L.T("玩家", "玩家", "Players"));
        ToggleRow(L.T("玩家透视", "玩家透視", "Player boxes"),
            L.T("隔墙显示其他玩家的框与名字(右键改色/样式)", "隔牆顯示其他玩家的框與名字(右鍵改色/樣式)", "Boxes + names through walls (right-click: color & style)"),
            Features.PlayerEsp, true, v => FeatureActions.Apply("PlayerEsp", v),
            new[] { "PlayerEsp" });
        ToggleRow(L.T("玩家骨骼", "玩家骨骼", "Skeleton"),
            L.T("隔墙显示骨架线条(右键行改色)", "隔牆顯示骨架線條(右鍵行改色)", "Bone lines through walls (right-click for color)"),
            Features.PlayerSkeleton, true, v => FeatureActions.Apply("PlayerSkeleton", v),
            new[] { "PlayerSkeleton" });
        ToggleRow(L.T("玩家血量", "玩家血量", "Health bars"),
            L.T("框顶显示当前血量", "框頂顯示當前血量", "HP bar over each box"),
            Features.PlayerHealth, true, v => FeatureActions.Apply("PlayerHealth", v),
            new[] { "PlayerHealth" });
        EndSection();

        BeginSection(L.T("生物与掉落物", "生物與掉落物", "Creatures & drops"));
        ToggleRow(L.T("生物透视", "生物透視", "Creature ESP"),
            L.T("隔墙显示生物(右键改色/样式)", "隔牆顯示生物(右鍵改色/樣式)", "Creatures through walls (right-click: color & style)"),
            Features.CreatureEsp, true, v => FeatureActions.Apply("CreatureEsp", v),
            new[] { "CreatureEsp" });
        ToggleRow(L.T("物品透视", "物品透視", "Item ESP"),
            L.T("隔墙显示掉落物(右键改色/距离)", "隔牆顯示掉落物(右鍵改色/距離)", "Dropped items through walls (right-click: color & distance)"),
            Features.ItemEsp, true, v => FeatureActions.Apply("ItemEsp", v),
            new[] { "ItemEsp" });
        EndSection();

        BeginSection(L.T("准星", "準星", "Crosshair"));
        ToggleRow(L.T("屏幕准星", "螢幕準星", "Crosshair"),
            L.T("不开镜时显示枪口指向的准星(右键改色/样式/大小)", "不開鏡時顯示槍口指向的準星(右鍵改色/樣式/大小)", "Muzzle-direction crosshair (right-click: color, style, size)"),
            Features.Crosshair, true, v => FeatureActions.Apply("Crosshair", v),
            new[] { "Crosshair" });
        EndSection();

        BeginSection(L.T("岛屿标签", "島嶼標籤", "Island labels"));
        ToggleRow(L.T("岛屿标签", "島嶼標籤", "Island labels"),
            L.T("屏幕显示所有岛屿的方向与距离", "螢幕顯示所有島嶼的方向與距離", "Every island's direction and distance on screen"),
            Features.IslandLabels, true, v => FeatureActions.Apply("IslandLabels", v),
            new[] { "IslandLabels" });
        EndSection();
    }

    // ────────────────────────── tab: items ──────────────────────────

    private static void TabItems(CheatController c)
    {
        if (!ItemDb.Loaded)
            ItemDb.Load();

        BeginSection(L.T("生成选项", "生成選項", "Spawn Options"));
        SliderRow(L.T("巨型倍率", "巨型倍率", "Giant size"), ref Features.GiantWeight, 1f, 50f, "x");
        ToggleRowInline(L.T("闪亮鱼", "閃亮魚", "Drip Fish"), Features.DripSpawn,
            v => FeatureActions.Apply("DripSpawn", v), new[] { "DripSpawn" });
        EndSection();

        // search
        BeginSection(null);
        var searchRect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
        Features.Search = GUI.TextField(searchRect, Features.Search ?? "", _search);
        if (string.IsNullOrEmpty(Features.Search))
            GUI.Label(Shrink(searchRect, 10, 4), L.T("搜索物品名称…", "搜尋物品名稱…", "Search items…"), _labelDim);
        EndSection();

        // list
        var listRect = GUILayoutUtility.GetRect(0, 250, GUILayout.ExpandWidth(true));
        GUI.Box(listRect, "", _search);
        float rowH = 24f;
        float pad = 6f;

        var items = FilterItems();
        float contentH = Mathf.Max(items.Count * rowH, listRect.height);
        _scroll = GUI.BeginScrollView(listRect, _scroll, new Rect(0, 0, listRect.width - 14, contentH));

        int first = Mathf.Max(0, (int)(_scroll.y / rowH) - 1);
        int last = Mathf.Min(items.Count, first + (int)(listRect.height / rowH) + 3);

        for (int i = first; i < last; i++)
        {
            var item = items[i];
            var r = new Rect(pad, i * rowH, listRect.width - pad * 2 - 12, rowH - 2);
            bool sel = Features.Selected == item;

            DrawFill(r, sel ? CAccentDim : (i % 2 == 1 ? CRow : new Color(0, 0, 0, 0)));

            GUI.Label(new Rect(r.x + 8, r.y, r.width * 0.6f, r.height), ItemDb.NameOf(item),
                sel ? _tabOn : _labelSmall);

            string tag = (item.Creature != null || item is Creature)
                ? L.T("生物", "生物", "Creature")
                : L.T("物品", "物品", "Item");
            string worth = $"  {tag} · ${item.DefaultWorth}";
            GUI.Label(new Rect(r.x + r.width - 190, r.y, 182, r.height), worth, _labelDim);

            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                Features.Selected = item;
                Event.current.Use();
            }
        }
        GUI.EndScrollView();

        if (items.Count == 0)
            GUI.Label(new Rect(listRect.x + 10, listRect.y + listRect.height / 2 - 10, listRect.width - 20, 20),
                L.T("没有匹配的物品", "沒有匹配的物品", "Nothing found"), _labelDim);

        // spawn buttons
        GUILayout.Space(8);
        bool canSpawn = Features.Selected != null && Features.CanHostAct;
        ButtonRow(
            canSpawn
                ? L.T("刷 1 个", "刷 1 個", "Spawn 1")
                : L.T("刷 1 个 (需选中+主机/联机桥)", "刷 1 個 (需選中+主機/聯機橋)", "Spawn 1 (pick an item first)"),
            L.T("刷 5 个", "刷 5 個", "Spawn 5"),
            canSpawn,
            () => { if (Features.HostAct(CheatAction.SpawnItem, amount: 1, itemId: Features.Selected.ID)) c.ShowToast(L.T("已生成 ", "已生成 ", "Spawned ") + ItemDb.NameOf(Features.Selected)); },
            () => { if (Features.HostAct(CheatAction.SpawnItem, amount: 5, itemId: Features.Selected.ID)) c.ShowToast(L.T("已生成 5 个 ", "已生成 5 個 ", "Spawned 5× ") + ItemDb.NameOf(Features.Selected)); });

        ButtonRow(L.T("鱼雨 ×25 (随机生物)", "魚雨 ×25 (隨機生物)", "Fish Rain ×25 (random)"), null, Features.CanHostAct, () =>
        {
            if (Features.HostAct(CheatAction.FishRain, amount: 25))
                c.ShowToast(L.T("天上掉鱼啦!", "天上掉魚啦!", "Fish rain!"));
        }, null);

        if (Features.Selected != null)
            GUILayout.Label(L.T("已选中: ", "已選中: ", "Selected: ") + ItemDb.NameOf(Features.Selected), _labelDim);
    }

    private static List<Item> FilterItems()
    {
        string q = Features.Search?.Trim();
        if (string.IsNullOrEmpty(q))
            return ItemDb.All;
        return ItemDb.All.Where(i => ItemDb.NameOf(i).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
    }

    // ────────────────────────── tab: combat ──────────────────────────

    private static void TabCombat(CheatController c)
    {
        BeginSection(L.T("伤害", "傷害", "Damage"));
        ToggleRow(L.T("秒杀", "秒殺", "One-shot"),
            L.T("你打出的每一击都是十万伤害", "你打出的每一擊都是十萬傷害", "Everything you hit takes 100,000 damage"),
            Features.SelfOneShot, true, v => FeatureActions.Apply("SelfOneShot", v),
            new[] { "SelfOneShot" });
        ToggleRow(L.T("强制暴击", "強制暴擊", "Force crit"),
            L.T("打生物无论打到哪都按爆头结算", "打生物無論打到哪都按爆頭結算", "Creature hits always count as headshots"),
            Features.ForceCrit, true, v => { Features.ForceCrit = v; Settings.MarkDirty(); });
        EndSection();

        BeginSection(L.T("射击", "射擊", "Shooting"));
        ToggleRow(L.T("无后座", "無後座", "No recoil"),
            L.T("视角与枪模都不再抖动", "視角與槍模都不再抖動", "Zero kick on your view and gun"),
            Features.NoRecoil, true, v => FeatureActions.Apply("NoRecoil", v),
            new[] { "NoRecoil" });
        ToggleRow(L.T("无扩散", "無擴散", "No Spread"),
            L.T("散布归零,子弹沿枪口直线飞出(与镭射一致)", "散佈歸零,子彈沿槍口直線飛出(與鐳射一致)", "Bullets go exactly where the muzzle points"),
            Features.NoSpread, true, v => FeatureActions.Apply("NoSpread", v),
            new[] { "NoSpread" });
        ToggleRow(L.T("无限子弹", "無限子彈", "Infinite ammo"),
            L.T("弹匣永远满,自动跳过换弹动作", "彈匣永遠滿,自動跳過換彈動作", "Magazine always full, reloads are skipped"),
            Features.InfiniteAmmo, true, v => FeatureActions.Apply("InfiniteAmmo", v),
            new[] { "InfiniteAmmo" });
        ToggleRow(L.T("快速射击", "快速射擊", "Fast Fire"),
            L.T("所有枪械按下方倍率提升射速", "所有槍械按下方倍率提升射速", "Every gun uses the rate multiplier"),
            Features.FastFire, true, v => FeatureActions.Apply("FastFire", v),
            new[] { "FastFire" });
        SliderRow(L.T("射速倍率", "射速倍率", "Fire Rate"), ref Features.FireRateMult, 1f, 10f, "x");
        OptionRow(L.T("开火模式", "開火模式", "Fire Mode"),
            new[]
            {
                L.T("点击", "點擊", "Click"),
                L.T("长按", "長按", "Hold"),
                L.T("长按+点击", "長按+點擊", "Hold+Click"),
            },
            ref Features.FireMode);
        EndSection();

        BeginSection(L.T("自瞄", "自瞄", "Aimbot"));
        ToggleRow(L.T("自瞄玩家", "自瞄玩家", "Aim Players"),
            L.T("按住触发键自动跟踪其他玩家(默认右键,右键行可改绑)", "按住觸發鍵自動跟蹤其他玩家(預設右鍵,右鍵行可改綁)", "Hold the trigger to lock the nearest player"),
            Features.Aimbot, true, v => FeatureActions.Apply("Aimbot", v),
            new[] { "Aimbot", Hotkeys.AimbotTrigger });
        ChipToggleRow(L.T("瞄准部位", "瞄準部位", "Aim point"),
            L.T("身体", "身體", "Body"), ref Features.SilentAimAimBody,
            L.T("头部", "頭部", "Head"), ref Features.SilentAimAimHead);
        Features.SilentAimHead = Features.SilentAimAimHead;
        Features.SilentAimPlayerPoint = Features.SilentAimAimHead && !Features.SilentAimAimBody ? 1 : 0;
        ToggleRow(L.T("致命时打身体", "致命時打身體", "Body if lethal"),
            L.T("打身体已经能打死时优先打身体; 多选部位时还会避开掩体", "打身體已經能打死時優先打身體; 多選部位時還會避開掩體", "Prefer body when a body shot already kills; with both points on, skip covered parts"),
            Features.SilentAimBodyIfLethal, true, v => { Features.SilentAimBodyIfLethal = v; Settings.MarkDirty(); });
        SliderRow(L.T("自瞄强度", "自瞄強度", "Smoothing"), ref Features.AimbotStrength, 0.1f, 1f, "");
        SliderRow(L.T("瞄准范围", "瞄準範圍", "Lock cone"), ref Features.AimbotMaxAngle, 5f, 90f, "°");
        Hint(L.T("按住右键瞄准时锁定离准星最近的玩家并持续跟踪,打死或目标离开范围才换目标。",
                "按住右鍵瞄準時鎖定離準星最近的玩家並持續跟蹤,打死或目標離開範圍才換目標。",
                "Locks the player closest to your crosshair and follows until they die or leave the cone."));
        ToggleRow(L.T("静默自瞄", "靜默自瞄", "Silent aim"),
            L.T("子弹自动飞向准星附近的目标,视角不动", "子彈自動飛向準星附近的目標,視角不動", "Bullets curve to the nearest target — your view stays still"),
            Features.SilentAim, true, v => FeatureActions.Apply("SilentAim", v),
            new[] { "SilentAim" });
        EndSection();


    }

    // ────────────────────────── tab: vehicle ──────────────────────────

    private static void TabBoat(CheatController c)
    {
        BeginSection(L.T("船", "船", "Boat"));
        SliderRow(L.T("船速倍率", "船速倍率", "Boat Speed"), ref Features.BoatMult, 1f, 8f, "x");
        ToggleRow(L.T("载具飞行", "載具飛行", "Vehicle Flight"),
            L.T("W前 S后 A左 D右 / Space升 Ctrl降,船头随视线", "W前 S後 A左 D右 / Space升 Ctrl降,船頭隨視線", "Fly in 6 directions — the bow follows your view"),
            Features.BoatFlight, true, v => FeatureActions.Apply("BoatFlight", v),
            new[] { "BoatFlight", Hotkeys.BoatFlightUp, Hotkeys.BoatFlightDown });
        EndSection();



        BeginSection(null);
        Hint(L.T("发动油门时按倍率推进,关闭滑条或调回 1x 自动还原。",
                "發動油門時按倍率推進,關閉滑條或調回 1x 自動還原。",
                "Boat speed = throttle × multiplier. Back to 1x = stock."));
        EndSection();
    }

    // ────────────────────────── tab: fun ──────────────────────────


    private static void TabWorld(CheatController c)
    {
        BeginSection(L.T("世界", "世界", "World"));
        ButtonRow(L.T("秒杀当前 Boss", "秒殺當前 Boss", "Kill Current Boss"), null, Features.CanHostAct, () =>
        {
            if (Features.HostAct(CheatAction.KillBoss))
                c.ShowToast(L.T("Boss 已被安排", "Boss 已被安排", "Boss taken care of"));
        }, null);

        ButtonRow(L.T("清空所有生物", "清空所有生物", "Kill All Creatures"), L.T("复活所有生物", "復活所有生物", "Respawn Creatures"), Features.CanHostAct, () =>
        {
            if (Features.HostAct(CheatAction.KillAllCreatures))
                c.ShowToast(L.T("世界已清净", "世界已清淨", "World cleared"));
        }, () =>
        {
            if (Features.HostAct(CheatAction.RespawnCreatures))
                c.ShowToast(L.T("万物复苏", "萬物復甦", "Creatures respawned"));
        });
        EndSection();

        BeginSection(L.T("掉落物", "掉落物", "Dropped items"));
        ButtonRow(L.T("清除所有掉落物", "清除所有掉落物", "Clear all drops"),
            L.T("清除(保留武器)", "清除(保留武器)", "Clear (keep guns)"), Features.CanHostAct, () =>
        {
            int n = Features.ClearDroppedItems(false);
            c.ShowToast(string.Format(L.T("已清除 {0} 个掉落物", "已清除 {0} 個掉落物", "Cleared {0} drops"), n));
        }, () =>
        {
            int n = Features.ClearDroppedItems(true);
            c.ShowToast(string.Format(L.T("已清除 {0} 个掉落物(武器保留)", "已清除 {0} 個掉落物(武器保留)", "Cleared {0} drops (guns kept)"), n));
        });
        EndSection();

        BeginSection(L.T("收藏", "收藏", "Collection"));
        ButtonRow(L.T("解锁全部皮肤", "解鎖全部皮膚", "Unlock All Skins"), null, true, () =>
        {
            int n = Features.UnlockAllSkins();
            c.ShowToast(n >= 0
                ? string.Format(L.T("已解锁 {0} 个皮肤", "已解鎖 {0} 個皮膚", "{0} skins unlocked"), n)
                : L.T("解锁失败,看日志", "解鎖失敗,看日誌", "Unlock failed — check the log"));
        }, null);
        EndSection();
    }

    // ────────────────────────── tab: casino ──────────────────────────

    private static void TabCasino(CheatController c)
    {
        BeginSection(L.T("轮盘赌", "輪盤賭", "Roulette"));
        ToggleRow(L.T("轮盘必中", "輪盤必中", "Roulette: always win"),
            L.T("球出手后强制滚到下注颜色(物理干预)", "球出手後強制滾到下注顏色(物理干預)", "Physically forces the ball into your bet color"),
            Features.RouletteAutoWin, Features.CanHostAct, v =>
        {
            Features.RouletteAutoWin = v;
            Features.HostAct(CheatAction.RouletteAutoWin, flag: v);
        });
        Hint(L.T("赌局旋转时顶部自动显示落点预测与稳定倒计时(球停稳 2 秒判胜)。",
                "賭局旋轉時頂部自動顯示落點預測與穩定倒數(球停穩 2 秒判勝)。",
                "While the wheel spins, the overlay shows the landing color and a settle countdown (2s = result)."));
        EndSection();

        BeginSection(L.T("老虎机", "老虎機", "Slot machine"));
        ToggleRow(L.T("结果预测", "結果預測", "Spin preview"),
            L.T("开转瞬间显示中奖结果(人人可见)", "開轉瞬間顯示中獎結果(人人可見)", "Shows the winning prize the moment the spin starts"),
            Features.SlotPredict, true, v => FeatureActions.Apply("SlotPredict", v),
            new[] { "SlotPredict" });
        OptionRow(L.T("必中品质", "必中品質", "Force rarity"),
            new[]
            {
                L.T("关", "關", "Off"),
                L.T("稀有", "稀有", "Rare"),
                L.T("传说", "傳說", "Legendary"),
            },
            ref Features.SlotCheatRarity);
        Hint(L.T("必中需要主机;从奖品池里随机挑一件对应品质。指定皮肤优先于品质。",
                "必中需要主機;從獎品池裡隨機挑一件對應品質。指定皮膚優先於品質。",
                "Host only; a random prize of that rarity. An exact skin pick below wins over this."));
        EndSection();

        BeginSection(L.T("指定皮肤", "指定皮膚", "Exact prize"));
        var items = SlotMachine.AvailableItems;
        if (items == null || items.Length == 0)
        {
            Hint(L.T("先进一次赌场,奖池加载后再来。", "先進一次賭場,獎池加載後再來。", "Enter the casino once — the prize pool loads there."));
        }
        else
        {
            foreach (var it in items)
            {
                if (it == null || it.SkinPreset == null)
                    continue;
                var skins = it.SkinPreset.Skins;
                for (byte i = 0; i < skins.Count; i++)
                {
                    var rar = skins[i].Rarity;
                    string tag = rar == Rarity.Legendary ? L.T("传说", "傳說", "★")
                               : rar == Rarity.Rare ? L.T("稀有", "稀有", "Rare") : "";
                    string txt = ItemDb.NameOf(it) + (tag != "" ? " · " + tag : "");
                    int itemId = it.ID;
                    byte skinIdx = i;
                    ButtonRow(txt, null, true, () =>
                    {
                        Features.SlotSkinItemId = itemId;
                        Features.SlotSkinIndex = skinIdx;
                        Settings.MarkDirty();
                        c.ShowToast(L.T("已指定: ", "已指定: ", "Locked: ") + txt);
                    }, null);
                }
            }
            ButtonRow(L.T("清除指定皮肤", "清除指定皮膚", "Clear exact prize"), null, Features.SlotSkinItemId >= 0, () =>
            {
                Features.SlotSkinItemId = -1;
                Settings.MarkDirty();
                c.ShowToast(L.T("已清除,回到品质模式", "已清除,回到品質模式", "Cleared — back to rarity mode"));
            }, null);
        }
        EndSection();


    }

    // ────────────────────────── tab: fishing ──────────────────────────

    private static void TabFishing(CheatController c)
    {
        BeginSection(L.T("上钩控制", "上鉤控制", "Hook control"));
        ToggleRow(L.T("秒上钩", "秒上鉤", "Instant bite"),
            L.T("鱼饵入水立即挂上一条随机鱼,需主机", "魚餌入水立即掛上一條隨機魚,需主機", "A random fish hooks the moment the bait hits the water (host)"),
            Features.BaitInstant, Features.CanHostAct, v => FeatureActions.Apply("BaitInstant", v),
            new[] { "BaitInstant" });
        ToggleRow(L.T("指定上钩鱼", "指定上鉤魚", "Hook any fish"),
            L.T("上钩的=物品页选中的生物,需主机", "上鉤的=物品頁選中的生物,需主機", "Hooks the creature you picked in Items (host)"),
            Features.BaitFish, Features.CanHostAct, v => FeatureActions.Apply("BaitFish", v),
            new[] { "BaitFish" });
        EndSection();

        BeginSection(null);
        Hint(L.T("去物品页选中一条鱼(带\"生物\"标签的),指定上钩会优先用它。",
                "去物品頁選中一條魚(帶\"生物\"標籤的),指定上鉤會優先用它。",
                "Pick a creature in the Items tab — \"Hook any fish\" uses it when set."));
        EndSection();
    }

    // ────────────────────────── tab: hotkeys ──────────────────────────

    private static string ActionLabel(string action)
    {
        switch (action)
        {
            case Hotkeys.MenuToggle: return L.T("开关菜单", "開關選單", "Menu toggle");
            case Hotkeys.AimbotTrigger: return L.T("自瞄触发", "自瞄觸發", "Aimbot trigger");
            case Hotkeys.PlayerFlightUp: return L.T("玩家飞行 · 上升", "玩家飛行 · 上升", "Player flight · up");
            case Hotkeys.PlayerFlightDown: return L.T("玩家飞行 · 下降", "玩家飛行 · 下降", "Player flight · down");
            case Hotkeys.BoatFlightUp: return L.T("载具飞行 · 上升", "載具飛行 · 上升", "Boat flight · up");
            case Hotkeys.BoatFlightDown: return L.T("载具飞行 · 下降", "載具飛行 · 下降", "Boat flight · down");
            case "GodMode": return L.T("上帝模式", "上帝模式", "God mode");
            case "FullnessLock": return L.T("锁满饱食度", "鎖滿飽食度", "No hunger");
            case "KeepInventory": return L.T("保留物品栏", "保留物品欄", "Keep inventory");
            case "PlayerFlight": return L.T("玩家飞行", "玩家飛行", "Flight");
            case "ThirdPerson": return L.T("第三人称", "第三人稱", "Third person");
            case "SelfOneShot": return L.T("秒杀", "秒殺", "One-shot");
            case "BuiltInCheats": return L.T("游戏内置作弊", "遊戲內置作弊", "Built-in Cheats");
            case "NoRecoil": return L.T("无后座", "無後座", "No recoil");
            case "NoSpread": return L.T("无扩散", "無擴散", "No Spread");
            case "InfiniteAmmo": return L.T("无限子弹", "無限子彈", "Infinite ammo");
            case "Aimbot": return L.T("自瞄玩家", "自瞄玩家", "Aim Players");
            case "PlayerEsp": return L.T("玩家透视", "玩家透視", "Boxes");
            case "PlayerSkeleton": return L.T("玩家骨骼", "玩家骨骼", "Skeleton");
            case "PlayerHealth": return L.T("玩家血量", "玩家血量", "Health bars");
            case "Crosshair": return L.T("屏幕准星", "螢幕準星", "Crosshair");
            case "SilentAim": return L.T("静默自瞄", "靜默自瞄", "Silent aim");
            case "CreatureEsp": return L.T("生物透视", "生物透視", "Creature ESP");
            case "ItemEsp": return L.T("物品透视", "物品透視", "Item ESP");
            case "BoatFlight": return L.T("载具飞行", "載具飛行", "Vehicle Flight");
            case "IslandLabels": return L.T("岛屿标签", "島嶼標籤", "Island labels");
            case "DripSpawn": return L.T("闪亮鱼", "閃亮魚", "Drip Fish");
            case "SlotPredict": return L.T("老虎机预测", "老虎機預測", "Spin preview");
            case "BaitFish": return L.T("指定上钩鱼", "指定上鉤魚", "Hook any fish");
            case "BaitInstant": return L.T("秒上钩", "秒上鉤", "Instant bite");
            default: return action;
        }
    }

    private static string KeyName(KeyCode k)
    {
        if (k == KeyCode.None) return L.T("未设置", "未設置", "None");
        if (k == KeyCode.Mouse0) return L.T("鼠标左键", "鼠標左鍵", "Mouse L");
        if (k == KeyCode.Mouse1) return L.T("鼠标右键", "鼠標右鍵", "Mouse R");
        if (k == KeyCode.Mouse2) return L.T("鼠标中键", "鼠標中鍵", "Mouse M");
        return k.ToString();
    }

    private static void TabKeys(CheatController c)
    {
        BeginSection(L.T("功能键", "功能鍵", "Actions"));
        foreach (var action in Hotkeys.All)
            BindRow(action);
        EndSection();

        BeginSection(L.T("开关热键", "開關熱鍵", "Toggle hotkeys"));
        foreach (var id in Hotkeys.ToggleIds)
            BindRow(id);
        EndSection();

        BeginSection(null);
        Hint(L.T("在功能行上直接右键也能弹出绑定窗口。点击按钮后按下新按键(鼠标键也可以),Esc 取消。",
                "在功能行上直接右鍵也能彈出綁定視窗。點擊按鈕後按下新按鍵(鼠標鍵也可以),Esc 取消。",
                "Right-click any row to rebind it. Click a bind, press the new key (mouse works too). Esc cancels."));
        EndSection();
    }

    private static void BindRow(string action)
    {
        if (_fold)
            return;
        var r = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
        GUI.Label(new Rect(r.x, r.y + 5, r.width - 140, 20), ActionLabel(action), _label);

        bool listening = _listening == action;
        string btnText = listening
            ? L.T("按任意键…", "按任意鍵…", "Press any key…")
            : KeyName(Hotkeys.Key(action));
        var br = new Rect(r.xMax - 132, r.y + 3, 126, 24);
        if (GUI.Button(br, btnText, listening ? _btnAccent : _btn) && !listening && _listening == null)
            _listening = action;
    }

    // ────────────────────────── widgets ──────────────────────────

    private static float _y;

    // collapsible sections: click a header to fold/unfold its content
    private static readonly Dictionary<string, bool> _folded = new();
    private static bool _fold;

    private static void BeginSection(string title)
    {
        GUILayout.Space(4);
        _y = 0;
        if (title == null)
        {
            _fold = false;
            return;
        }

        bool folded = _folded.TryGetValue(title, out var f) && f;
        _fold = folded;

        var r = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
        GUI.Label(new Rect(r.x, r.y + 1, r.width - 20, 18),
            (folded ? "▸  " : "▾  ") + title.ToUpperInvariant(), _labelDim);
        DrawFill(new Rect(r.x, r.yMax - 1, r.width, 1), new Color(1f, 1f, 1f, 0.045f));
        if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
        {
            _folded[title] = !folded;
            Event.current.Use();
        }
        if (!folded)
            GUILayout.Space(2);
    }

    private static void EndSection()
    {
        _fold = false;
        GUILayout.Space(8);
    }

    /// <summary>Muted helper text inside a section; hidden while the section is folded.</summary>
    private static void Hint(string text)
    {
        if (_fold)
            return;
        GUILayout.Label(text, _labelDim);
    }

    private static void ToggleRow(string label, string sub, bool value, bool enabled, Action<bool> onChange,
        string[] bindActions = null)
    {
        if (_fold)
            return;
        // adaptive height: the sub text wraps in the narrower page column
        float subW = WinW - 14f - NavW - NavGap - 14f - 60f;
        float subH = _labelDim.CalcHeight(new GUIContent(sub), subW);
        float rowH = Mathf.Max(36f, 21f + subH + 8f);
        var r = GUILayoutUtility.GetRect(0, rowH, GUILayout.ExpandWidth(true));
        GUI.Label(new Rect(r.x, r.y + 2, r.width - 60, 18), label, _label);
        GUI.Label(new Rect(r.x, r.y + 19, r.width - 60, subH + 3), sub, _labelDim);

        // right-click on a bindable row → neverlose-style keybind popup
        if (bindActions != null && Event.current.type == EventType.MouseDown
            && Event.current.button == 1 && r.Contains(Event.current.mousePosition))
        {
            OpenBindPopup(bindActions, Event.current.mousePosition);
            Event.current.Use();
        }

        var pill = new Rect(r.xMax - 48, r.y + 7, 44, 22);
        if (GUI.Button(pill, "", value ? _pillOn : _pillOff) && enabled)
            onChange(!value);
        DrawKnob(pill, value, enabled);
        if (!enabled)
        {
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            GUI.DrawTexture(pill, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }

    private static void ToggleRowInline(string label, bool value, Action<bool> onChange, string[] bindActions = null)
    {
        if (_fold)
            return;
        var r = GUILayoutUtility.GetRect(0, 26, GUILayout.ExpandWidth(true));
        GUI.Label(new Rect(r.x, r.y + 3, r.width - 60, 20), label, _label);

        // right-click on a bindable row → keybind popup
        if (bindActions != null && Event.current.type == EventType.MouseDown
            && Event.current.button == 1 && r.Contains(Event.current.mousePosition))
        {
            OpenBindPopup(bindActions, Event.current.mousePosition);
            Event.current.Use();
        }

        var pill = new Rect(r.xMax - 48, r.y + 2, 44, 22);
        if (GUI.Button(pill, "", value ? _pillOn : _pillOff))
            onChange(!value);
        DrawKnob(pill, value, true);
    }

    private static void DrawKnob(Rect pill, bool on, bool enabled)
    {
        float size = 14;
        float x = on ? pill.xMax - size - 4 : pill.x + 4;
        var knob = new Rect(x, pill.y + (pill.height - size) / 2, size, size);
        var prev = GUI.color;
        if (!enabled)
            GUI.color = new Color(0.62f, 0.66f, 0.69f, 0.55f);
        else if (on)
            GUI.color = new Color(0.05f, 0.10f, 0.11f, 0.92f);
        else
            GUI.color = new Color(0.58f, 0.62f, 0.65f, 0.85f);
        GUI.DrawTexture(knob, _texKnob);
        GUI.color = prev;
    }

    private static void SliderRow(string label, ref float value, float min, float max, string suffix)
    {
        if (_fold)
            return;
        var r = GUILayoutUtility.GetRect(0, 34, GUILayout.ExpandWidth(true));
        GUI.Label(new Rect(r.x, r.y, r.width - 70, 18), label, _label);
        GUI.Label(new Rect(r.xMax - 66, r.y, 66, 18),
            $"{value:0.0}{suffix}", _value);
        var bar = new Rect(r.x, r.y + 20, r.width, 14);
        float nv = GUI.HorizontalSlider(bar, value, min, max, _sliderBg, _sliderThumb);
        float snapped = Mathf.Round(nv * 10f) / 10f; // snap to 0.1
        if (!Mathf.Approximately(snapped, value))
            Settings.MarkDirty();
        value = snapped;
    }

    /// <summary>Two independent chips; at least one stays on.</summary>
    private static void ChipToggleRow(string label, string a, ref bool va, string b, ref bool vb)
    {
        if (_fold)
            return;
        var r = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
        GUI.Label(new Rect(r.x, r.y + 5, r.width - 205, 20), label, _label);
        float bw = 62;
        var ra = new Rect(r.xMax - 200, r.y + 3, bw, 24);
        var rb = new Rect(r.xMax - 200 + bw + 3, r.y + 3, bw, 24);
        if (GUI.Button(ra, a, va ? _btnAccent : _btn))
        {
            if (!(va && !vb))
            {
                va = !va;
                Settings.MarkDirty();
            }
        }
        if (GUI.Button(rb, b, vb ? _btnAccent : _btn))
        {
            if (!(vb && !va))
            {
                vb = !vb;
                Settings.MarkDirty();
            }
        }
    }

    /// <summary>Segmented option picker (e.g. fire mode: click / hold / hold+click).</summary>
    private static void OptionRow(string label, string[] options, ref int index)
    {
        if (_fold)
            return;
        var r = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
        GUI.Label(new Rect(r.x, r.y + 5, r.width - 205, 20), label, _label);
        float bw = 62;
        for (int i = 0; i < options.Length; i++)
        {
            var br = new Rect(r.xMax - 200 + i * (bw + 3), r.y + 3, bw, 24);
            if (GUI.Button(br, options[i], index == i ? _btnAccent : _btn) && index != i)
            {
                index = i;
                Settings.MarkDirty();
            }
        }
    }

    private static void ButtonRow(string left, string right, bool enabled, Action onLeft, Action onRight)
    {
        if (_fold)
            return;
        var r = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
        float gap = 8;
        float w = right != null ? (r.width - gap) / 2f : r.width;

        DrawButton(new Rect(r.x, r.y, w, r.height), left, enabled, onLeft);
        if (right != null)
            DrawButton(new Rect(r.x + w + gap, r.y, w, r.height), right, enabled, onRight);
    }

    private static void DrawButton(Rect r, string text, bool enabled, Action onClick)
    {
        var style = !enabled ? _btnDisabled : _btn;
        if (enabled && GUI.Button(r, text, _btn))
            onClick();
        else if (!enabled)
            GUI.Button(r, text, _btnDisabled);
    }

    // ────────────────────────── HUD / toast ──────────────────────────

    private static void DrawHudHint(CheatController c)
    {
        string txt = L.T("INSERT 打开菜单 · HTFMod v1.3", "INSERT 開啟選單 · HTFMod v1.3", "INSERT opens menu · HTFMod v1.3");
        var size = _hint.CalcSize(new GUIContent(txt));
        var r = new Rect(Screen.width - size.x - 14, Screen.height - size.y - 10, size.x + 16, size.y + 8);
        DrawFill(r, new Color(0, 0, 0, 0.45f));
        GUI.Label(r, txt, _hint);
    }

    private static void DrawToast(CheatController c)
    {
        if (string.IsNullOrEmpty(c.Toast) || Time.time > c.ToastUntil)
            return;
        var size = _label.CalcSize(new GUIContent(c.Toast));
        float w = size.x + 36, h = 34;
        var r = new Rect((Screen.width - w) / 2, 46, w, h);
        DrawFill(r, CPanel);
        DrawFill(new Rect(r.x, r.yMax - 2, r.width, 2), CAccent);
        GUI.Label(new Rect(r.x, r.y + 6, r.width, 22), c.Toast, _label);
    }

    // ────────────────────────── textures / styles ──────────────────────────

    private static void EnsureFont()
    {
        if (_font != null)
            return;
        try
        {
            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Segoe UI" }, 13);
        }
        catch
        {
            _font = null;
        }
    }

    private static void EnsureStyles()
    {
        if (_stylesReady)
            return;
        _stylesReady = true;

        _texPanel = Rounded(28, 28, 8, CPanel);
        _texRow = Flat(CRow);
        _texRowHover = Flat(CRowHover);
        _texBtn = Rounded(24, 24, 8, CRow);
        _texBtnHover = Rounded(24, 24, 8, CRowHover);
        _texBtnAccent = Rounded(24, 24, 8, new Color(0.24f, 0.88f, 0.82f, 0.30f));
        _texPillOn = Rounded(24, 24, 10, CAccent);
        _texPillOff = Rounded(24, 24, 10, new Color(0.16f, 0.19f, 0.22f, 1f));
        _texSliderBg = Rounded(16, 8, 4, new Color(0.13f, 0.16f, 0.19f, 1f));
        _texSliderThumb = Rounded(14, 14, 7, CAccent);
        _texChip = Rounded(24, 24, 9, new Color(0.14f, 0.17f, 0.20f, 1f));
        _texKnob = Rounded(14, 14, 7, Color.white);

        _panel = Style(_texPanel, CText, 13);
        _panel.border = new RectOffset(9, 9, 9, 9);
        _panel.padding = new RectOffset(14, 14, 10, 10);

        _title = Style(null, CAccent, 14, bold: true);
        _label = Style(null, CText, 13, rich: true);
        _labelDim = Style(null, CTextDim, 11, rich: true);
        _labelDim.wordWrap = true; // long hint lines must wrap, not widen the tab (no h-scrollbar)
        _labelSmall = Style(null, CText, 13);
        _labelSmall.richText = true;
        _value = Style(null, CAccent, 13, align: TextAnchor.UpperRight, rich: true);
        _hint = Style(null, new Color(0.75f, 0.79f, 0.82f, 0.85f), 11);

        _tabOn = Style(null, CAccent, 13, bold: true);
        _tabOff = Style(null, CTextDim, 13);
        _tabOn.alignment = _tabOff.alignment = TextAnchor.MiddleCenter;

        _btn = BtnStyle(_texBtn, _texBtnHover, CText, 12);
        _btnAccent = BtnStyle(_texBtnAccent, _texBtnHover, CAccent, 12);
        _btnDisabled = BtnStyle(_texBtn, null, new Color(0.42f, 0.46f, 0.49f, 0.7f), 12);

        _pillOn = Pill(_texPillOn);
        _pillOff = Pill(_texPillOff);

        _search = Style(_texRow, CText, 12);
        _search.border = new RectOffset(6, 6, 6, 6);
        _search.padding = new RectOffset(8, 8, 5, 5);

        _chip = Style(_texChip, CTextDim, 11);
        _chipOn = Style(_texChip, CAccent, 11);
        _chip.padding = _chipOn.padding = new RectOffset(8, 8, 3, 3);

        _sliderBg = Style(_texSliderBg, Color.white, 8);
        _sliderBg.border = new RectOffset(4, 4, 3, 3);
        _sliderThumb = Style(_texSliderThumb, Color.white, 8);
        _sliderThumb.fixedWidth = 14;
        _sliderThumb.fixedHeight = 14;
    }

    private static GUIStyle BtnStyle(Texture2D tex, Texture2D hover, Color color, int size)
    {
        var s = new GUIStyle();
        s.normal.background = tex;
        if (hover != null) s.hover.background = hover;
        s.active.background = hover ?? tex;
        s.normal.textColor = color;
        s.hover.textColor = color;
        s.active.textColor = color;
        s.font = _font;
        s.fontSize = size;
        s.alignment = TextAnchor.MiddleCenter;
        if (tex != null) s.border = new RectOffset(8, 8, 8, 8);
        s.richText = true;
        return s;
    }

    private static GUIStyle Pill(Texture2D tex)
    {
        var s = new GUIStyle();
        s.normal.background = tex;
        s.hover.background = tex;
        s.active.background = tex;
        s.border = new RectOffset(10, 10, 10, 10);
        return s;
    }

    private static GUIStyle Style(Texture2D tex, Color color, int size,
        TextAnchor align = TextAnchor.UpperLeft, bool bold = false, bool rich = false)
    {
        var s = new GUIStyle();
        s.normal.background = tex;
        s.normal.textColor = color;
        s.hover = s.active = s.focused = s.normal;
        s.font = _font;
        s.fontSize = size;
        s.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        s.alignment = align;
        s.richText = rich;
        if (tex != null)
            s.border = new RectOffset(Mathf.Min(9, tex.width / 2), Mathf.Min(9, tex.width / 2),
                Mathf.Min(9, tex.height / 2), Mathf.Min(9, tex.height / 2));
        return s;
    }

    private static void DrawFill(Rect r, Color c)
    {
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    private static Texture2D Flat(Color c)
    {
        var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = c;
        t.SetPixels(px);
        t.Apply();
        t.hideFlags = HideFlags.HideAndDontSave;
        return t;
    }

    private static Texture2D Rounded(int w, int h, int radius, Color c)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        var px = new Color[w * h];
        float rr = radius + 0.5f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Max(radius - x, x - (w - 1 - radius), 0);
                float dy = Mathf.Max(radius - y, y - (h - 1 - radius), 0);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(rr - d);
                px[y * w + x] = new Color(c.r, c.g, c.b, c.a * a);
            }
        }
        t.SetPixels(px);
        t.Apply();
        t.hideFlags = HideFlags.HideAndDontSave;
        return t;
    }

    private static Rect Shrink(Rect r, float x, float y)
    {
        return new Rect(r.x + x, r.y + y, r.width - x * 2, r.height - y * 2);
    }
}
