using System;
using System.Reflection;
using UnityEngine;

namespace HTFMod;

/// <summary>
/// Roulette landing prediction + auto-win.
/// Prediction: the result is decided by server-side physics (ball + spinning wheel);
/// both host and clients can read the current slot from the ball/wheel state in real
/// time. The game declares the winner after the ball rests in one slot for 2 seconds,
/// so the overlay shows the current slot plus a settle countdown — the prediction
/// converges to certainty seconds before the official result.
/// Host reads _isPlaying for exact state; clients use a ball-movement heuristic.
/// Auto-win (host only, Features.RouletteAutoWin): shortly after launch the ball is
/// frozen and the wheel's own spin is stopped; then the WHEEL is rotated one slot per
/// frame until the game's own color-judgment function reads the bet color
/// (CasinoManager._curBetColor). The game's 2-second dwell check then declares the
/// bet color the winner through the native payout flow. A 5s timeout falls back to
/// calling the game's result API directly.
/// </summary>
public static class Roulette
{
    private static bool _initFailed;
    private static MethodInfo _miColor;
    private static FieldInfo _fiPlaying;
    private static FieldInfo _fiBall;
    private static FieldInfo _fiWheel;
    private static FieldInfo _fiWheelSpeed;
    private static FieldInfo _fiSlotSize;
    private static FieldInfo _fiCurBet; // CasinoManager static: current bet color

    private static LocalCasino _tracked;
    private static Rigidbody _ball;

    private static BetColor _prevColor = (BetColor)(-1);
    private static float _dwell;
    private static Vector3 _lastBallPos;
    private static float _lastMoveTime;
    private static bool _active;

    // auto-win intervention state
    private static float _playSince;
    private static float _steerElapsed;
    private static bool _wasPlaying;
    private static bool _steerEngaged;

    public static bool Active => _active && LocalCasino.Instance != null;

    public static BetColor Color => _prevColor;

    public static float Dwell => _dwell;

    private static void Init()
    {
        if (_miColor != null || _initFailed)
            return;
        try
        {
            var t = typeof(LocalCasino);
            _miColor = t.GetMethod("GetRouletteColorFromBall", BindingFlags.NonPublic | BindingFlags.Instance);
            _fiPlaying = t.GetField("_isPlaying", BindingFlags.NonPublic | BindingFlags.Instance);
            _fiBall = t.GetField("_ball", BindingFlags.NonPublic | BindingFlags.Instance);
            _fiWheel = t.GetField("_wheel", BindingFlags.NonPublic | BindingFlags.Instance);
            _fiWheelSpeed = t.GetField("_curWheelSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
            _fiSlotSize = t.GetField("_slotSize", BindingFlags.NonPublic | BindingFlags.Instance);
            _fiCurBet = typeof(CasinoManager).GetField("_curBetColor", BindingFlags.NonPublic | BindingFlags.Static);
            if (_miColor == null || _fiPlaying == null || _fiBall == null ||
                _fiWheel == null || _fiWheelSpeed == null || _fiSlotSize == null || _fiCurBet == null)
                _initFailed = true;
        }
        catch
        {
            _initFailed = true;
        }
    }

    /// <summary>Called every frame from CheatController.Update.</summary>
    public static void Tick()
    {
        var c = LocalCasino.Instance;
        if (c == null)
        {
            _tracked = null;
            _ball = null;
            _active = false;
            _wasPlaying = false;
            return;
        }

        Init();
        if (_initFailed)
            return;

        try
        {
            if (_tracked != c)
            {
                _tracked = c;
                _ball = _fiBall.GetValue(c) as Rigidbody;
                _lastBallPos = _ball != null ? _ball.transform.position : Vector3.zero;
                _lastMoveTime = -99f;
                _prevColor = (BetColor)(-1);
                _dwell = 0f;
            }

            // 1) Current slot via the game's own judgment code (includes the LookAt
            //    side effect, so the readout matches the server exactly).
            BetColor col = (BetColor)_miColor.Invoke(c, null);
            if (col != _prevColor)
            {
                _prevColor = col;
                _dwell = 0f;
            }
            else
            {
                _dwell += Time.deltaTime;
            }

            // 2) Activity detection: host reads _isPlaying directly; clients watch
            //    whether the ball is moving.
            bool playing = false;
            try { playing = (bool)_fiPlaying.GetValue(c); } catch { }

            if (playing && !_wasPlaying)
                _playSince = Time.time;
            _wasPlaying = playing;

            if (_ball != null)
            {
                Vector3 p = _ball.transform.position;
                if ((p - _lastBallPos).sqrMagnitude > 1e-5f)
                    _lastMoveTime = Time.time;
                _lastBallPos = p;
            }

            _active = playing || Time.time - _lastMoveTime < 2.5f;

            // 3) Auto-win (host): 0.4s after launch, take over — rotate the WHEEL
            //    until the game's own judgment reads the bet color.
            bool steerNow = playing && Features.RouletteAutoWin && Features.IsHost
                            && Time.time - _playSince > 0.4f;
            if (steerNow)
            {
                SteerToBetColor(c);
            }
            else
            {
                if (_steerEngaged && playing && Features.IsHost && _ball != null)
                {
                    _ball.isKinematic = false; // auto-win turned off mid-round: give the ball back to physics
                    _steerEngaged = false;
                }
                _steerElapsed = 0f;
            }
        }
        catch
        {
            _active = false;
        }
    }

    /// <summary>
    /// Auto-win: freeze the ball, then rotate the wheel one slot per frame until the
    /// game's own judgment function reads the bet color; the game's 2-second dwell
    /// check then declares the win natively. If not aligned within 5 seconds, call
    /// the game's result API directly as a fallback.
    /// </summary>
    private static void SteerToBetColor(LocalCasino c)
    {
        var wheel = _fiWheel.GetValue(c) as Transform;
        var ball = _fiBall.GetValue(c) as Rigidbody;
        var cm = CasinoManager.Instance;
        if (wheel == null || ball == null || cm == null)
            return;

        object betRaw = _fiCurBet.GetValue(null);
        if (betRaw == null)
            return;
        var target = (BetColor)betRaw;

        // Freeze the ball + stop the wheel's own spin speed
        // (the game sets _isSpinning = false by itself next frame).
        ball.isKinematic = true;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        _fiWheelSpeed.SetValue(c, 0f);
        _steerEngaged = true;

        BetColor cur = (BetColor)_miColor.Invoke(c, null);
        if (cur == target)
        {
            _steerElapsed = 0f; // aligned: hands off — the game's 2s dwell check now wins the bet
            return;
        }

        _steerElapsed += Time.deltaTime;
        if (_steerElapsed > 5f)
        {
            // Fallback: declare the bet color the winner directly (public game API, full payout flow).
            _fiPlaying.SetValue(c, false);
            cm.ServerRouletteResult(target);
            _steerElapsed = 0f;
            Loader.Log("Roulette auto-win: physics steering timed out, declared " + target + " directly");
            return;
        }

        // Rotate the wheel by one slot per frame (max ~37 frames for a full revolution).
        float step = Convert.ToSingle(_fiSlotSize.GetValue(c));
        Vector3 e = wheel.localEulerAngles;
        wheel.localEulerAngles = new Vector3(e.x, e.y + step, e.z);
    }
}
