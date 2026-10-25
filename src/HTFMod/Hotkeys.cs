using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace HTFMod;

/// <summary>
/// Rebindable hotkeys. Each action is a BepInEx config entry (section "Hotkeys"),
/// so bindings survive restarts and can also be edited by hand in the cfg file.
/// </summary>
public static class Hotkeys
{
    public const string MenuToggle = "MenuToggle";
    public const string AimbotTrigger = "AimbotTrigger";
    public const string PlayerFlightUp = "PlayerFlightUp";
    public const string PlayerFlightDown = "PlayerFlightDown";
    public const string BoatFlightUp = "BoatFlightUp";
    public const string BoatFlightDown = "BoatFlightDown";

    /// <summary>Display order for the menu (labels are resolved by MenuUi).</summary>
    public static readonly string[] All =
    {
        MenuToggle,
        AimbotTrigger,
        PlayerFlightUp,
        PlayerFlightDown,
        BoatFlightUp,
        BoatFlightDown,
    };

    private static readonly Dictionary<string, ConfigEntry<KeyCode>> _entries = new();
    private static readonly List<string> _toggleIds = new();
    private static ConfigFile _cfg;

    public static void Init(ConfigFile cfg)
    {
        _cfg = cfg;
        Bind(cfg, MenuToggle, KeyCode.Insert, "Open/close the cheat menu");
        Bind(cfg, AimbotTrigger, KeyCode.Mouse1, "Aimbot trigger (hold)");
        Bind(cfg, PlayerFlightUp, KeyCode.Space, "Player flight: up");
        Bind(cfg, PlayerFlightDown, KeyCode.LeftControl, "Player flight: down");
        Bind(cfg, BoatFlightUp, KeyCode.Space, "Boat flight: up");
        Bind(cfg, BoatFlightDown, KeyCode.LeftControl, "Boat flight: down");
    }

    /// <summary>
    /// Register a menu toggle as hotkey-capable. Config entry defaults to None
    /// (unbound); the user binds it via the right-click popup or the Keys tab.
    /// </summary>
    public static void RegisterToggle(string id)
    {
        if (_toggleIds.Contains(id))
            return;
        _toggleIds.Add(id);
        if (_cfg != null && !_entries.ContainsKey(id))
            _entries[id] = _cfg.Bind("Hotkeys", id, KeyCode.None, "Toggle hotkey: " + id);
    }

    /// <summary>Ids of all registered toggles (for the Keys tab and the bind popup).</summary>
    public static IReadOnlyList<string> ToggleIds => _toggleIds;

    /// <summary>Per-frame: fire toggles whose bound key was pressed this frame.</summary>
    public static void TickToggles()
    {
        if (_toggleIds.Count == 0)
            return;
        foreach (var id in _toggleIds)
        {
            var k = Key(id);
            if (k == KeyCode.None)
                continue;
            if (InputX.GetKeyDown(k))
            {
                try { FeatureActions.Apply(id, !FeatureActions.Get(id)); }
                catch { }
            }
        }
    }

    private static void Bind(ConfigFile cfg, string key, KeyCode def, string desc)
    {
        _entries[key] = cfg.Bind("Hotkeys", key, def, desc);
    }

    public static KeyCode Key(string action)
    {
        return _entries.TryGetValue(action, out var e) ? e.Value : KeyCode.None;
    }

    public static void Set(string action, KeyCode code)
    {
        if (!_entries.TryGetValue(action, out var e) || e.Value == code)
            return;
        e.Value = code;
        e.ConfigFile?.Save();
    }
}
