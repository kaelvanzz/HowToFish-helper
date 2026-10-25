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

    private const float WinW = 430f;

    /// <summary>Window height: tall enough for the biggest tab on 1080p+,
    /// shrinks on small screens; the tab scrollview handles any leftover overflow.</summary>
    private static float WinH => Mathf.Min(740f, Screen.height - 130f);

    private static Rect _win = new Rect(80, 80, WinW, WinH);
    private static int _tab;
    private static Vector2 _scroll;
    private static Vector2 _tabScroll;

    private static string[] Tabs => new[]
    {
        L.T("玩家", "玩家", "Player"),
        L.T("金钱", "金錢", "Money"),
        L.T("物品", "物品", "Items"),
        L.T("战斗", "戰鬥", "Combat"),
        L.T("载具", "載具", "Vehicle"),
        L.T("玩乐", "玩樂", "Fun"),
        L.T("按键", "按鍵", "Keys"),
    };

    // hotkey rebinding: which action is waiting for a key press (null = none)
    private static string _listening;

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
        DrawCrosshair(c);
        DrawRouletteHud();
        DrawToast(c);
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
        var prev = GUI.color;
        GUI.color = new Color(0.95f, 0.97f, 1f, 0.85f);
        float dot = 2.5f, gap = 5f, len = 7f, t = 1.6f;
        GUI.DrawTexture(new Rect(cx - dot / 2, cy - dot / 2, dot, dot), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - t / 2, cy - gap - len, t, len), Texture2D.whiteTexture); // up
        GUI.DrawTexture(new Rect(cx - t / 2, cy + gap, t, len), Texture2D.whiteTexture);       // down
        GUI.DrawTexture(new Rect(cx - gap - len, cy - t / 2, len, t), Texture2D.whiteTexture); // left
        GUI.DrawTexture(new Rect(cx + gap, cy - t / 2, len, t), Texture2D.whiteTexture);       // right
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
        bool hasColor = GetActionColor(actions[0]) >= 0;
        float h = 30 + actions.Length * 30 + (hasColor ? 140 : 0) + 34;
        _bindPopupRect = new Rect(_win.x + winLocalMouse.x + 8, _win.y + winLocalMouse.y + 8, 230, h);
        _listening = null;
        _pickDrag = 0;
        _pvSyncColor = -1; // force picker resync to this action's color
    }

    /// <summary>Actions that own an ESP color (packed 0xRRGGBB); -1 = none.</summary>
    private static int GetActionColor(string action)
    {
        if (action == "PlayerEsp") return Features.EspBoxColor;
        if (action == "PlayerSkeleton") return Features.EspSkeletonColor;
        return -1;
    }

    private static void SetActionColor(string action, int rgb)
    {
        if (action == "PlayerEsp") Features.EspBoxColor = rgb;
        else if (action == "PlayerSkeleton") Features.EspSkeletonColor = rgb;
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
        _bindPopupRect = GUI.Window(0x485447, _bindPopupRect, _bindPopupFn, "", _panel);
        _bindPopupRect.x = Mathf.Clamp(_bindPopupRect.x, 4, Screen.width - _bindPopupRect.width - 4);
        _bindPopupRect.y = Mathf.Clamp(_bindPopupRect.y, 4, Screen.height - _bindPopupRect.height - 4);
    }

    private static void BindPopupFunc(int id)
    {
        var w = _bindPopupRect;
        GUI.DragWindow(new Rect(0, 0, w.width, 22));

        GUI.Label(new Rect(10, 5, w.width - 20, 20), _bindPopupTitle, _title);
        float y = 28;
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

        // clear all binds shown in this popup
        if (GUI.Button(new Rect(10, y + 1, 92, 22), L.T("清除绑定", "清除綁定", "Clear binds"), _btn))
        {
            foreach (var a in _bindPopup)
                Hotkeys.Set(a, KeyCode.None);
            _listening = null;
        }

        // HSV color picker for color-capable actions (presets + SV square + hue bar)
        if (GetActionColor(_bindPopup[0]) >= 0)
            DrawColorPicker(_bindPopup[0], y + 30, w.width - 20);

        GUI.Label(new Rect(110, y + 5, w.width - 120, 18),
            L.T("点击按钮后按新键 · Esc 取消", "點擊按鈕後按新鍵 · Esc 取消", "Click a button, press the new key · Esc cancels"), _hint);
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
                _svTex.SetPixel(x, y, Color.HSVToRGB(_pvH, x / 63f, 1f - y / 63f));
        _svTex.Apply();
    }

    private static void ApplyPicker(string act)
    {
        var c = Color.HSVToRGB(_pvH, _pvS, _pvV);
        int packed = (Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255) << 16)
                   | (Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255) << 8)
                   | Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255);
        SetActionColor(act, packed);
        _pvSyncColor = packed;
    }

    private static void DrawColorPicker(string act, float y, float width)
    {
        int packed = GetActionColor(act);
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
                SetActionColor(act, Palette[i]);
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
            ApplyPicker(act);
        }
        else if (_pickDrag == 1)
        {
            _pvH = Mathf.Clamp01((e.mousePosition.x - hueRect.x) / hueRect.width);
            ApplyPicker(act);
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
        0x3DE0D1, // teal
        0xF2994C, // orange
        0xCC73F2, // purple
        0xF2DB4C, // yellow
        0xF2F2F2, // white
    };

    private static Color EspColor(int rgb)
    {
        return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 0.9f);
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

                Vector3 head = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
                Vector3 feet = new Vector3(b.center.x, b.min.y, b.center.z);
                Vector3 sh = cam.WorldToScreenPoint(head);
                Vector3 sf = cam.WorldToScreenPoint(feet);
                if (sh.z <= 0.1f || sf.z <= 0.1f)
                    continue; // behind the camera
                if (sh.z > 250f)
                    continue;

                float top = Screen.height - sh.y;
                float bottom = Screen.height - sf.y;
                float h = Mathf.Clamp(bottom - top, 18f, Screen.height);
                float w = Mathf.Clamp(h * 0.45f, 10f, 220f);
                var r = new Rect(sh.x - w / 2f, top, w, h);

                if (Features.PlayerSkeleton)
                    DrawSkeleton(pl, cam);

                if (Features.PlayerEsp)
                {
                    DrawEspOutline(r, EspColor(Features.EspBoxColor));

                    string name = (pl.SteamName ?? L.T("玩家", "玩家", "Player")).Replace("<", "");
                    GUI.Label(new Rect(r.x - 40, r.y - 20, r.width + 80, 18),
                        $"<color=#{ColorUtility.ToHtmlStringRGB(EspColor(Features.EspBoxColor))}>{name}  {sh.z:0}m</color>", _label);
                }

                if (Features.PlayerHealth)
                    DrawHealthBar(r, GetHealth(pl));
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

    /// <summary>1px GL line in GUI pixel space (IMGUI has no line primitive).</summary>
    private static void DrawLine(Vector2 a, Vector2 b, Color c)
    {
        if (_lineMat == null)
        {
            _lineMat = new Material(Shader.Find("Hidden/Internal-Colored"));
            _lineMat.hideFlags = HideFlags.HideAndDontSave;
            _lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _lineMat.SetInt("_ZWrite", 0);
        }
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

    private static void DrawEspOutline(Rect r, Color color)
    {
        var prev = GUI.color;
        GUI.color = color;
        const float t = 1.5f;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.color = prev;
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

        // tab bar
        var tabs = Tabs;
        float tabW = (w.width - 28) / tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
        {
            var r = new Rect(x + i * tabW, y, tabW, 26);
            bool on = _tab == i;
            if (GUI.Button(r, tabs[i], on ? _tabOn : _tabOff))
                _tab = i;
            if (on)
                DrawFill(new Rect(r.x + 10, r.yMax, r.width - 20, 2), CAccent);
        }

        y += 34;

        // content (scrollable — tabs may exceed the window height)
        var content = new Rect(0, y, w.width, w.height - y - 26);
        GUILayout.BeginArea(content);
        _tabScroll = GUILayout.BeginScrollView(_tabScroll, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
        switch (_tab)
        {
            case 0: TabPlayer(c); break;
            case 1: TabMoney(c); break;
            case 2: TabItems(c); break;
            case 3: TabCombat(c); break;
            case 4: TabBoat(c); break;
            case 5: TabFun(c); break;
            case 6: TabKeys(c); break;
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        // bottom bar: hint (left) + language dropdown (right)
        string hint = Features.IsHost
            ? L.T("INSERT 开关菜单 · 你是主机,全部功能可用",
                  "INSERT 開關選單 · 你是主機,全部功能可用",
                  "INSERT toggles the menu · You are the host, everything works")
            : Multiplayer.HostModOnline
                ? L.T("INSERT 开关菜单 · 联机桥已连接,全功能可用",
                      "INSERT 開關選單 · 聯機橋已連線,全功能可用",
                      "INSERT toggles the menu · Bridge connected, full features")
                : L.T("INSERT 开关菜单 · 房主未装mod:战斗页本地功能可用,其余置灰",
                      "INSERT 開關選單 · 房主未裝mod:戰鬥頁本地功能可用,其餘置灰",
                      "INSERT toggles the menu · Host has no mod: local combat features only");
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
        ToggleRow(L.T("上帝模式", "上帝模式", "God Mode"),
            L.T("免疫所有伤害(火/毒/溺水/攻击)", "免疫所有傷害(火/毒/溺水/攻擊)", "Immune to all damage (fire/poison/drowning/attacks)"),
            Features.GodMode, true, v => FeatureActions.Apply("GodMode", v),
            new[] { "GodMode" });
        ToggleRow(L.T("锁满饱食度", "鎖滿飽食度", "Fullness Lock"),
            L.T("不掉饱食 / 中毒 / 着火", "不掉飽食 / 中毒 / 著火", "No hunger, poison or fire"),
            Features.FullnessLock, Features.CanHostAct, v => FeatureActions.Apply("FullnessLock", v),
            new[] { "FullnessLock" });
        ToggleRow(L.T("保留物品栏", "保留物品欄", "Keep Inventory"),
            L.T("死亡/复活不掉落物品", "死亡/復活不掉落物品", "No item drops on death/respawn"),
            Features.KeepInventory, true, v => FeatureActions.Apply("KeepInventory", v),
            new[] { "KeepInventory" });
        EndSection();

        BeginSection(L.T("运动", "運動", "Movement"));
        ToggleRow(L.T("玩家飞行", "玩家飛行", "Player Flight"),
            L.T("Space 升 / Ctrl 降,悬停;开船时自动让位", "Space 升 / Ctrl 降,懸停;開船時自動讓位", "Space up / Ctrl down, hover; yields while driving"),
            Features.PlayerFlight, true, v => FeatureActions.Apply("PlayerFlight", v),
            new[] { "PlayerFlight", Hotkeys.PlayerFlightUp, Hotkeys.PlayerFlightDown });
        SliderRow(L.T("移速倍率", "移速倍率", "Speed"), ref Features.SpeedMult, 0.3f, 5f, "x");
        SliderRow(L.T("跳跃倍率", "跳躍倍率", "Jump"), ref Features.JumpMult, 0.3f, 5f, "x");
        EndSection();

        BeginSection(L.T("视觉", "視覺", "Visual"));
        ToggleRow(L.T("第三人称", "第三人稱", "Third Person"),
            L.T("相机拉到角色身后跟随视角", "相機拉到角色身後跟隨視角", "Camera follows behind your character"),
            Features.ThirdPerson, true, v => FeatureActions.Apply("ThirdPerson", v),
            new[] { "ThirdPerson" });
        SliderRow(L.T("第三人称距离", "第三人稱距離", "TP Distance"), ref Features.ThirdPersonDist, 0.5f, 8f, "m");
        SliderRow(L.T("FOV 倍率", "FOV 倍率", "FOV"), ref Features.FovMult, 0.5f, 2f, "x");
        EndSection();
    }

    // ────────────────────────── tab: money ──────────────────────────

    private static void TabMoney(CheatController c)
    {
        BeginSection(L.T("钱包", "錢包", "Wallet"));
        if (!_fold)
        {
            GUI.Label(new Rect(0, _y, 200, 20), L.T("现金", "現金", "Cash") + $"  <color=#3DE0D1>${MoneyManager.Money:N0}</color>", _label);
            GUI.Label(new Rect(0, _y + 22, 200, 20), L.T("欧元", "歐元", "Euros") + $"  <color=#3DE0D1>€{MoneyManager.Euro:N0}</color>", _label);
            _y += 48;
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
        EndSection();

        BeginSection(L.T("开关", "開關", "Toggles"));
        ToggleRow(L.T("秒杀", "秒殺", "One Shot"),
            L.T("自己打出的伤害固定为 100000", "自己打出的傷害固定為 100000", "Your attacks deal a fixed 100000 damage"),
            Features.SelfOneShot, true, v => FeatureActions.Apply("SelfOneShot", v),
            new[] { "SelfOneShot" });
        ToggleRow(L.T("游戏内置作弊", "遊戲內置作弊", "Built-in Cheats"),
            L.T("启用后 M=+$99999 / N=-$99999 / Ctrl+M=+€999", "啟用後 M=+$99999 / N=-$99999 / Ctrl+M=+€999", "Enables M=+$99999 / N=-$99999 / Ctrl+M=+€999"),
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

    // ────────────────────────── tab: items ──────────────────────────

    private static void TabItems(CheatController c)
    {
        if (!ItemDb.Loaded)
            ItemDb.Load();

        BeginSection(L.T("生成选项", "生成選項", "Spawn Options"));
        SliderRow(L.T("巨型倍率", "巨型倍率", "Giant Weight"), ref Features.GiantWeight, 1f, 50f, "x");
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
                L.T("没有匹配的物品", "沒有匹配的物品", "No matching items"), _labelDim);

        // spawn buttons
        GUILayout.Space(8);
        bool canSpawn = Features.Selected != null && Features.CanHostAct;
        ButtonRow(
            canSpawn
                ? L.T("刷 1 个", "刷 1 個", "Spawn 1")
                : L.T("刷 1 个 (需选中+主机/联机桥)", "刷 1 個 (需選中+主機/聯機橋)", "Spawn 1 (need item + host/bridge)"),
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
        BeginSection(L.T("射击", "射擊", "Shooting"));
        ToggleRow(L.T("无后座", "無後座", "No Recoil"),
            L.T("视角与枪模都不再抖动", "視角與槍模都不再抖動", "No view or gun-model kick"),
            Features.NoRecoil, true, v => FeatureActions.Apply("NoRecoil", v),
            new[] { "NoRecoil" });
        ToggleRow(L.T("无扩散", "無擴散", "No Spread"),
            L.T("散布归零,子弹沿枪口直线飞出(与镭射一致)", "散佈歸零,子彈沿槍口直線飛出(與鐳射一致)", "Zero spread; bullets fly straight along the muzzle (matches the laser)"),
            Features.NoSpread, true, v => FeatureActions.Apply("NoSpread", v),
            new[] { "NoSpread" });
        ToggleRow(L.T("无限子弹", "無限子彈", "Infinite Ammo"),
            L.T("弹匣永远满,自动跳过换弹动作", "彈匣永遠滿,自動跳過換彈動作", "Magazine always full, reloads are skipped"),
            Features.InfiniteAmmo, true, v => FeatureActions.Apply("InfiniteAmmo", v),
            new[] { "InfiniteAmmo" });
        ToggleRow(L.T("快速射击", "快速射擊", "Fast Fire"),
            L.T("所有枪械按下方倍率提升射速", "所有槍械按下方倍率提升射速", "All guns fire at the multiplier rate below"),
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
            L.T("按住触发键自动跟踪其他玩家(默认右键,右键行可改绑)", "按住觸發鍵自動跟蹤其他玩家(預設右鍵,右鍵行可改綁)", "Auto-track other players while the trigger is held (default RMB, right-click the row to rebind)"),
            Features.Aimbot, true, v => FeatureActions.Apply("Aimbot", v),
            new[] { "Aimbot", Hotkeys.AimbotTrigger });
        SliderRow(L.T("自瞄强度", "自瞄強度", "Aim Strength"), ref Features.AimbotStrength, 0.1f, 1f, "");
        SliderRow(L.T("瞄准范围", "瞄準範圍", "Aim Range"), ref Features.AimbotMaxAngle, 5f, 90f, "°");
        Hint(L.T("按住右键瞄准时锁定离准星最近的玩家并持续跟踪,打死或目标离开范围才换目标。",
                "按住右鍵瞄準時鎖定離準星最近的玩家並持續跟蹤,打死或目標離開範圍才換目標。",
                "Hold RMB to lock the player nearest the crosshair and keep tracking; the target only changes after it dies or leaves range."));
        EndSection();

        BeginSection(L.T("透视", "透視", "ESP"));
        ToggleRow(L.T("玩家透视", "玩家透視", "Player ESP"),
            L.T("隔墙显示其他玩家的框与距离(右键行改框色)", "隔牆顯示其他玩家的框與距離(右鍵行改框色)", "Boxes and distance through walls (right-click the row for colors)"),
            Features.PlayerEsp, true, v => FeatureActions.Apply("PlayerEsp", v),
            new[] { "PlayerEsp" });
        ToggleRow(L.T("玩家骨骼", "玩家骨骼", "Skeleton"),
            L.T("隔墙显示其他玩家的骨架线条(右键行改骨色)", "隔牆顯示其他玩家的骨架線條(右鍵行改骨色)", "Bone lines through walls (right-click the row for colors)"),
            Features.PlayerSkeleton, true, v => FeatureActions.Apply("PlayerSkeleton", v),
            new[] { "PlayerSkeleton" });
        ToggleRow(L.T("玩家血量", "玩家血量", "Health bars"),
            L.T("框顶显示其他玩家当前血量", "框頂顯示其他玩家當前血量", "Current HP above the box"),
            Features.PlayerHealth, true, v => FeatureActions.Apply("PlayerHealth", v),
            new[] { "PlayerHealth" });
        ToggleRow(L.T("屏幕准星", "螢幕準星", "Crosshair"),
            L.T("不开镜时显示枪口指向的准星", "不開鏡時顯示槍口指向的準星", "Muzzle-direction crosshair when not aiming down sights"),
            Features.Crosshair, true, v => FeatureActions.Apply("Crosshair", v),
            new[] { "Crosshair" });
        EndSection();
    }

    // ────────────────────────── tab: vehicle ──────────────────────────

    private static void TabBoat(CheatController c)
    {
        BeginSection(L.T("船", "船", "Boat"));
        SliderRow(L.T("船速倍率", "船速倍率", "Boat Speed"), ref Features.BoatMult, 1f, 8f, "x");
        ToggleRow(L.T("载具飞行", "載具飛行", "Vehicle Flight"),
            L.T("W前 S后 A左 D右 / Space升 Ctrl降,船头随视线", "W前 S後 A左 D右 / Space升 Ctrl降,船頭隨視線", "W fwd, S back, A left, D right / Space up, Ctrl down; bow follows your view"),
            Features.BoatFlight, true, v => FeatureActions.Apply("BoatFlight", v),
            new[] { "BoatFlight", Hotkeys.BoatFlightUp, Hotkeys.BoatFlightDown });
        EndSection();

        BeginSection(L.T("导航", "導航", "Navigation"));
        ToggleRow(L.T("岛屿标签", "島嶼標籤", "Island Labels"),
            L.T("屏幕直接显示所有岛屿方向与距离", "螢幕直接顯示所有島嶼方向與距離", "All islands: direction and distance on screen"),
            Features.IslandLabels, true, v => FeatureActions.Apply("IslandLabels", v),
            new[] { "IslandLabels" });
        Hint(L.T("在视野内显示标签,在屏幕外显示边缘方向点,无需看雷达。",
                "在視野內顯示標籤,在螢幕外顯示邊緣方向點,無需看雷達。",
                "Labels for islands in view, edge direction dots for the rest — no radar needed."));
        EndSection();

        BeginSection(null);
        Hint(L.T("发动油门时按倍率推进,关闭滑条或调回 1x 自动还原。",
                "發動油門時按倍率推進,關閉滑條或調回 1x 自動還原。",
                "Throttle scales by the multiplier; sliding back to 1x restores the original values."));
        EndSection();
    }

    // ────────────────────────── tab: fun ──────────────────────────

    private static void TabFun(CheatController c)
    {
        BeginSection(L.T("旅行", "旅行", "Travel"));
        ButtonRow(L.T("传送下一座岛", "傳送下一座島", "Next Island"), L.T("上一座岛", "上一座島", "Prev Island"), Features.CanHostAct, () =>
        {
            if (Features.HostAct(CheatAction.NextIsland, flag: false))
                c.ShowToast(L.T("跳转下一座岛…", "跳轉下一座島…", "Jumping to the next island…"));
        }, () =>
        {
            if (Features.HostAct(CheatAction.NextIsland, flag: true))
                c.ShowToast(L.T("返回上一座岛…", "返回上一座島…", "Returning to the previous island…"));
        });
        EndSection();

        BeginSection(L.T("轮盘赌", "輪盤賭", "Roulette"));
        ToggleRow(L.T("轮盘必中", "輪盤必中", "Roulette Auto-Win"),
            L.T("球出手后强制滚进你下注的颜色(物理干预)", "球出手後強制滾進你下注的顏色(物理干預)", "Physically forces the ball into your bet color after launch"),
            Features.RouletteAutoWin, Features.CanHostAct, v =>
        {
            Features.RouletteAutoWin = v;
            Features.HostAct(CheatAction.RouletteAutoWin, flag: v);
        });
        Hint(L.T("赌局旋转时顶部自动显示落点预测与稳定倒计时(球停稳 2 秒判胜)。",
                "賭局旋轉時頂部自動顯示落點預測與穩定倒數(球停穩 2 秒判勝)。",
                "During spins the overlay shows the landing prediction and settle countdown (2s dwell wins)."));
        EndSection();

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

        BeginSection(L.T("收藏", "收藏", "Collection"));
        ButtonRow(L.T("解锁全部皮肤", "解鎖全部皮膚", "Unlock All Skins"), null, true, () =>
        {
            int n = Features.UnlockAllSkins();
            c.ShowToast(n >= 0
                ? string.Format(L.T("已解锁 {0} 个皮肤", "已解鎖 {0} 個皮膚", "Unlocked {0} skins"), n)
                : L.T("解锁失败,看日志", "解鎖失敗,看日誌", "Unlock failed — check the log"));
        }, null);
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
            case "GodMode": return L.T("上帝模式", "上帝模式", "God Mode");
            case "FullnessLock": return L.T("锁满饱食度", "鎖滿飽食度", "Fullness Lock");
            case "KeepInventory": return L.T("保留物品栏", "保留物品欄", "Keep Inventory");
            case "PlayerFlight": return L.T("玩家飞行", "玩家飛行", "Player Flight");
            case "ThirdPerson": return L.T("第三人称", "第三人稱", "Third Person");
            case "SelfOneShot": return L.T("秒杀", "秒殺", "One Shot");
            case "BuiltInCheats": return L.T("游戏内置作弊", "遊戲內置作弊", "Built-in Cheats");
            case "NoRecoil": return L.T("无后座", "無後座", "No Recoil");
            case "NoSpread": return L.T("无扩散", "無擴散", "No Spread");
            case "InfiniteAmmo": return L.T("无限子弹", "無限子彈", "Infinite Ammo");
            case "Aimbot": return L.T("自瞄玩家", "自瞄玩家", "Aim Players");
            case "PlayerEsp": return L.T("玩家透视", "玩家透視", "Player ESP");
            case "PlayerSkeleton": return L.T("玩家骨骼", "玩家骨骼", "Skeleton");
            case "PlayerHealth": return L.T("玩家血量", "玩家血量", "Health bars");
            case "Crosshair": return L.T("屏幕准星", "螢幕準星", "Crosshair");
            case "BoatFlight": return L.T("载具飞行", "載具飛行", "Vehicle Flight");
            case "IslandLabels": return L.T("岛屿标签", "島嶼標籤", "Island Labels");
            case "DripSpawn": return L.T("闪亮鱼", "閃亮魚", "Drip Fish");
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
                "You can also right-click a feature row to open the bind popup. Click a button, then press the new key (mouse works too). Esc cancels."));
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

        var r = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
        GUI.Label(new Rect(r.x, r.y + 2, r.width - 20, 18),
            (folded ? "▸  " : "▾  ") + title.ToUpperInvariant(), _labelDim);
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
        var r = GUILayoutUtility.GetRect(0, 36, GUILayout.ExpandWidth(true));
        GUI.Label(new Rect(r.x, r.y + 2, r.width - 60, 18), label, _label);
        GUI.Label(new Rect(r.x, r.y + 19, r.width - 60, 16), sub, _labelDim);

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
        string txt = L.T("INSERT 打开菜单 · HTFMod v1.1", "INSERT 開啟選單 · HTFMod v1.1", "INSERT opens menu · HTFMod v1.1");
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
        _labelSmall = Style(null, CText, 12);
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
