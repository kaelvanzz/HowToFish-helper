using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HTFMod;

/// <summary>
/// Input helpers that work under both the legacy Input Manager and the new Input System.
/// The backend is probed once on first use and cached.
/// </summary>
public static class InputX
{
    private static int _mode = -1; // 0 = legacy, 1 = input system

    public static string Mode => _mode == 0 ? "legacy" : _mode == 1 ? "inputsystem" : "unknown";

    public static bool GetKeyDown(KeyCode code)
    {
        EnsureMode();
        if (_mode == 0)
        {
            try { return Input.GetKeyDown(code); }
            catch { _mode = 1; }
        }
        return SysDown(code);
    }

    public static bool GetKey(KeyCode code)
    {
        EnsureMode();
        if (_mode == 0)
        {
            try { return Input.GetKey(code); }
            catch { _mode = 1; }
        }
        return SysHold(code);
    }

    /// <summary>Left mouse held (works with both input backends).</summary>
    public static bool MouseLeftHeld()
    {
        EnsureMode();
        if (_mode == 0)
        {
            try { return Input.GetMouseButton(0); }
            catch { _mode = 1; }
        }
        var m = UnityEngine.InputSystem.Mouse.current;
        return m != null && m.leftButton.isPressed;
    }

    /// <summary>Right mouse held (aiming). Aimbot trigger.</summary>
    public static bool MouseRightHeld()
    {
        EnsureMode();
        if (_mode == 0)
        {
            try { return Input.GetMouseButton(1); }
            catch { _mode = 1; }
        }
        var m = UnityEngine.InputSystem.Mouse.current;
        return m != null && m.rightButton.isPressed;
    }

    /// <summary>Arbitrary key held (used by flight features).</summary>
    public static bool KeyHeld(KeyCode code)
    {
        EnsureMode();
        if (_mode == 0)
        {
            try { return Input.GetKey(code); }
            catch { _mode = 1; }
        }
        return SysHold(code);
    }

    private static void EnsureMode()
    {
        if (_mode >= 0)
            return;
        _mode = 0;
        try { _ = Input.GetKeyDown(KeyCode.Space); }
        catch { _mode = 1; }
    }

    private static bool SysDown(KeyCode code)
    {
        var kb = Keyboard.current;
        if (kb == null)
            return false;
        try
        {
            return code switch
            {
                KeyCode.Insert => kb.insertKey.wasPressedThisFrame,
                KeyCode.F1 => kb.f1Key.wasPressedThisFrame,
                KeyCode.F2 => kb.f2Key.wasPressedThisFrame,
                KeyCode.F3 => kb.f3Key.wasPressedThisFrame,
                KeyCode.Space => kb.spaceKey.wasPressedThisFrame,
                KeyCode.LeftControl => kb.leftCtrlKey.wasPressedThisFrame,
                KeyCode.M => kb.mKey.wasPressedThisFrame,
                KeyCode.N => kb.nKey.wasPressedThisFrame,
                _ => false,
            };
        }
        catch { return false; }
    }

    private static bool SysHold(KeyCode code)
    {
        var kb = Keyboard.current;
        if (kb == null)
            return false;
        try
        {
            return code switch
            {
                KeyCode.Insert => kb.insertKey.isPressed,
                KeyCode.F1 => kb.f1Key.isPressed,
                KeyCode.F2 => kb.f2Key.isPressed,
                KeyCode.F3 => kb.f3Key.isPressed,
                KeyCode.Space => kb.spaceKey.isPressed,
                KeyCode.LeftControl => kb.leftCtrlKey.isPressed,
                KeyCode.M => kb.mKey.isPressed,
                KeyCode.N => kb.nKey.isPressed,
                _ => false,
            };
        }
        catch { return false; }
    }
}
