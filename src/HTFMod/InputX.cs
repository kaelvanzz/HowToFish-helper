using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HTFMod;

/// <summary>兼容旧 Input Manager 与新 Input System 两种后端。</summary>
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

    /// <summary>按住鼠标左键(两种输入后端通吃)。</summary>
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

    /// <summary>按住鼠标右键(瞄准)。自瞄激活用。</summary>
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
                KeyCode.M => kb.mKey.wasPressedThisFrame,
                KeyCode.N => kb.nKey.wasPressedThisFrame,
                _ => false,
            };
        }
        catch { return false; }
    }

    /// <summary>按住任意键(自瞄/载具飞行用;新输入系统只映射常用键)。</summary>
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
