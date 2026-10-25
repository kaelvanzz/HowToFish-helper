using System;
using System.Reflection;
using UnityEngine;

namespace HTFMod;

/// <summary>
/// 轮盘赌落点预测。结果由服务器物理模拟决定(球 + 转盘),
/// 双方都能实时读取球相对转盘的槽位 —— 游戏在球稳定同一格 2 秒后判胜,
/// 因此叠加层显示"当前槽位 + 稳定倒计时",球停稳后预测即收敛为必中。
/// 主机读 _isPlaying 精确判定;客户端用球位移启发式。
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
    private static FieldInfo _fiCurBet; // CasinoManager 静态:当前下注颜色

    private static LocalCasino _tracked;
    private static Rigidbody _ball;

    private static BetColor _prevColor = (BetColor)(-1);
    private static float _dwell;
    private static Vector3 _lastBallPos;
    private static float _lastMoveTime;
    private static bool _active;

    // 必中干预状态
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

    /// <summary>每帧调用(CheatController.Update)。</summary>
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

            // 1) 当前槽位(游戏自己的判定逻辑,含 LookAt 副作用,与服务器一致)
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

            // 2) 活动检测:主机直接读 _isPlaying;客户端看球是否在动
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

            // 3) 必中干预(主机):球出手 0.4s 后接管,把转盘盘面转到下注颜色下
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
                    _ball.isKinematic = false; // 中途关闭必中:把球还给物理
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
    /// 必中:冻结球,逐帧把轮盘旋转一格,直到游戏自己的判定函数读出下注颜色;
    /// 之后游戏的 2 秒稳定判定自然判赢。5 秒内没转到位则直接调用游戏结果接口兜底。
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

        // 冻结球 + 停掉转盘自动转速(游戏下一帧自行把 _isSpinning 置 false)
        ball.isKinematic = true;
        ball.linearVelocity = Vector3.zero;
        ball.angularVelocity = Vector3.zero;
        _fiWheelSpeed.SetValue(c, 0f);
        _steerEngaged = true;

        BetColor cur = (BetColor)_miColor.Invoke(c, null);
        if (cur == target)
        {
            _steerElapsed = 0f; // 已到位:停手,等游戏 2 秒判定 → 必赢
            return;
        }

        _steerElapsed += Time.deltaTime;
        if (_steerElapsed > 5f)
        {
            // 兜底:直接宣判下注颜色获胜(游戏公开接口,走完整结算)
            _fiPlaying.SetValue(c, false);
            cm.ServerRouletteResult(target);
            _steerElapsed = 0f;
            Loader.Log("轮盘必中:物理转向超时,已直接判定 " + target);
            return;
        }

        // 把轮盘转一格(每帧一格,最多 ~37 格转满一圈)
        float step = Convert.ToSingle(_fiSlotSize.GetValue(c));
        Vector3 e = wheel.localEulerAngles;
        wheel.localEulerAngles = new Vector3(e.x, e.y + step, e.z);
    }
}
