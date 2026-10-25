using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace HTFMod;

/// <summary>
/// Input helpers that work under both the legacy Input Manager and the new Input System.
/// The backend is probed once on first use and cached. Any KeyCode can be polled —
/// keyboard keys and mouse buttons both map through to the Input System path.
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

    /// <summary>Arbitrary key or mouse button held (flight features, rebindable hotkeys).</summary>
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

    /// <summary>Map a KeyCode to the matching Input System button (keyboard or mouse).</summary>
    private static ButtonControl ControlOf(KeyCode code)
    {
        try
        {
            var kb = Keyboard.current;
            var m = UnityEngine.InputSystem.Mouse.current;
            switch (code)
            {
                case KeyCode.Mouse0: return m != null ? m.leftButton : null;
                case KeyCode.Mouse1: return m != null ? m.rightButton : null;
                case KeyCode.Mouse2: return m != null ? m.middleButton : null;
                case KeyCode.Mouse3: return m != null ? m.backButton : null;
                case KeyCode.Mouse4: return m != null ? m.forwardButton : null;
                case KeyCode.Backspace: return kb?.backspaceKey;
                case KeyCode.Delete: return kb?.deleteKey;
                case KeyCode.Tab: return kb?.tabKey;
                case KeyCode.Return: return kb?.enterKey;
                case KeyCode.KeypadEnter: return kb?.numpadEnterKey;
                case KeyCode.Escape: return kb?.escapeKey;
                case KeyCode.Space: return kb?.spaceKey;
                case KeyCode.CapsLock: return kb?.capsLockKey;
                case KeyCode.UpArrow: return kb?.upArrowKey;
                case KeyCode.DownArrow: return kb?.downArrowKey;
                case KeyCode.RightArrow: return kb?.rightArrowKey;
                case KeyCode.LeftArrow: return kb?.leftArrowKey;
                case KeyCode.Home: return kb?.homeKey;
                case KeyCode.End: return kb?.endKey;
                case KeyCode.PageUp: return kb?.pageUpKey;
                case KeyCode.PageDown: return kb?.pageDownKey;
                case KeyCode.Insert: return kb?.insertKey;
                case KeyCode.LeftShift: return kb?.leftShiftKey;
                case KeyCode.RightShift: return kb?.rightShiftKey;
                case KeyCode.LeftControl: return kb?.leftCtrlKey;
                case KeyCode.RightControl: return kb?.rightCtrlKey;
                case KeyCode.LeftAlt: return kb?.leftAltKey;
                case KeyCode.RightAlt: return kb?.rightAltKey;
                case KeyCode.BackQuote: return kb?.backquoteKey;
                case KeyCode.Minus: return kb?.minusKey;
                case KeyCode.Equals: return kb?.equalsKey;
                case KeyCode.LeftBracket: return kb?.leftBracketKey;
                case KeyCode.RightBracket: return kb?.rightBracketKey;
                case KeyCode.Backslash: return kb?.backslashKey;
                case KeyCode.Semicolon: return kb?.semicolonKey;
                case KeyCode.Quote: return kb?.quoteKey;
                case KeyCode.Comma: return kb?.commaKey;
                case KeyCode.Period: return kb?.periodKey;
                case KeyCode.Slash: return kb?.slashKey;
                case KeyCode.Keypad0: return kb?.numpad0Key;
                case KeyCode.Keypad1: return kb?.numpad1Key;
                case KeyCode.Keypad2: return kb?.numpad2Key;
                case KeyCode.Keypad3: return kb?.numpad3Key;
                case KeyCode.Keypad4: return kb?.numpad4Key;
                case KeyCode.Keypad5: return kb?.numpad5Key;
                case KeyCode.Keypad6: return kb?.numpad6Key;
                case KeyCode.Keypad7: return kb?.numpad7Key;
                case KeyCode.Keypad8: return kb?.numpad8Key;
                case KeyCode.Keypad9: return kb?.numpad9Key;
                case KeyCode.KeypadDivide: return kb?.numpadDivideKey;
                case KeyCode.KeypadMultiply: return kb?.numpadMultiplyKey;
                case KeyCode.KeypadMinus: return kb?.numpadMinusKey;
                case KeyCode.KeypadPlus: return kb?.numpadPlusKey;
                case KeyCode.KeypadPeriod: return kb?.numpadPeriodKey;
                case KeyCode.F1: return kb?.f1Key;
                case KeyCode.F2: return kb?.f2Key;
                case KeyCode.F3: return kb?.f3Key;
                case KeyCode.F4: return kb?.f4Key;
                case KeyCode.F5: return kb?.f5Key;
                case KeyCode.F6: return kb?.f6Key;
                case KeyCode.F7: return kb?.f7Key;
                case KeyCode.F8: return kb?.f8Key;
                case KeyCode.F9: return kb?.f9Key;
                case KeyCode.F10: return kb?.f10Key;
                case KeyCode.F11: return kb?.f11Key;
                case KeyCode.F12: return kb?.f12Key;
            }

            if (code >= KeyCode.Alpha0 && code <= KeyCode.Alpha9)
                return Digit(code - KeyCode.Alpha0);
            if (code >= KeyCode.A && code <= KeyCode.Z)
                return Letter(code - KeyCode.A);

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static ButtonControl Digit(int i)
    {
        var kb = Keyboard.current;
        return kb switch
        {
            null => null,
            _ when i == 0 => kb.digit0Key,
            _ when i == 1 => kb.digit1Key,
            _ when i == 2 => kb.digit2Key,
            _ when i == 3 => kb.digit3Key,
            _ when i == 4 => kb.digit4Key,
            _ when i == 5 => kb.digit5Key,
            _ when i == 6 => kb.digit6Key,
            _ when i == 7 => kb.digit7Key,
            _ when i == 8 => kb.digit8Key,
            _ => kb.digit9Key,
        };
    }

    private static ButtonControl Letter(int i)
    {
        var kb = Keyboard.current;
        return kb switch
        {
            null => null,
            _ when i == 0 => kb.aKey,
            _ when i == 1 => kb.bKey,
            _ when i == 2 => kb.cKey,
            _ when i == 3 => kb.dKey,
            _ when i == 4 => kb.eKey,
            _ when i == 5 => kb.fKey,
            _ when i == 6 => kb.gKey,
            _ when i == 7 => kb.hKey,
            _ when i == 8 => kb.iKey,
            _ when i == 9 => kb.jKey,
            _ when i == 10 => kb.kKey,
            _ when i == 11 => kb.lKey,
            _ when i == 12 => kb.mKey,
            _ when i == 13 => kb.nKey,
            _ when i == 14 => kb.oKey,
            _ when i == 15 => kb.pKey,
            _ when i == 16 => kb.qKey,
            _ when i == 17 => kb.rKey,
            _ when i == 18 => kb.sKey,
            _ when i == 19 => kb.tKey,
            _ when i == 20 => kb.uKey,
            _ when i == 21 => kb.vKey,
            _ when i == 22 => kb.wKey,
            _ when i == 23 => kb.xKey,
            _ when i == 24 => kb.yKey,
            _ => kb.zKey,
        };
    }

    private static bool SysDown(KeyCode code)
    {
        var c = ControlOf(code);
        try { return c != null && c.wasPressedThisFrame; }
        catch { return false; }
    }

    private static bool SysHold(KeyCode code)
    {
        var c = ControlOf(code);
        try { return c != null && c.isPressed; }
        catch { return false; }
    }
}
