using System;
using System.Diagnostics;
using FishNet.Connection;
using FishNet.Managing.Statistic;
using FishNet.Serializing;
using FishNet.Transporting;
using UnityEngine;

namespace FishNet.Managing.Timing;

[DisallowMultipleComponent]
[AddComponentMenu("FishNet/Manager/TimeManager")]
public sealed class TimeManager : MonoBehaviour
{
	private enum TimingType
	{
		Tick,
		Variable
	}

	private enum UpdateOrder : byte
	{
		BeforeTick,
		AfterTick
	}

	[Tooltip("When to invoke OnUpdate and other Unity callbacks relayed by the TimeManager.")]
	[SerializeField]
	private UpdateOrder _updateOrder;

	[Tooltip("Timing for sending and receiving data.")]
	[SerializeField]
	private TimingType _timingType;

	[Tooltip("While true clients may drop local ticks if their devices are unable to maintain the tick rate. This could result in a temporary desynchronization but will prevent the client falling further behind on ticks by repeatedly running the logic cycle multiple times per frame.")]
	[SerializeField]
	private bool _allowTickDropping = true;

	[Tooltip("Maximum number of ticks which may occur in a single frame before remainder are dropped for the frame.")]
	[Range(1f, 25f)]
	[SerializeField]
	private byte _maximumFrameTicks = 3;

	[Tooltip("How many times per second the server will simulate. This does not limit server frame rate.")]
	[Range(1f, 240f)]
	[SerializeField]
	private ushort _tickRate = 30;

	[Tooltip("How often in seconds to a connections ping. This is also responsible for approximating server tick. This value does not affect prediction.")]
	[Range(1f, 15f)]
	[SerializeField]
	private byte _pingInterval = 1;

	[Tooltip("How to perform physics.")]
	[SerializeField]
	private PhysicsMode _physicsMode;

	private uint _localTick;

	private Stopwatch _pingStopwatch = new Stopwatch();

	private uint _pingTicks;

	private MovingAverage _pingAverage = new MovingAverage(5);

	private double _elapsedTickTime;

	private double _adjustedTickDelta;

	private int _lastIncomingIterationFrame = -1;

	private bool _receivedPong = true;

	private float _lastMultipleTicksTime;

	private static uint _manualPhysics;

	private bool _fixedUpdateTimeStep;

	private float _physicsTimeScale = 1f;

	private NetworkTrafficStatistics _networkTrafficStatistics;

	public const uint UNSET_TICK = 0u;

	private const string SAVED_FIXED_TIME_TEXT = "SavedFixedTimeFN";

	public NetworkManager NetworkManager { get; private set; }

	internal uint TimingTickInterval => _tickRate;

	public long RoundTripTime { get; private set; }

	public long HalfRoundTripTime => (long)Math.Round((double)RoundTripTime / 2.0);

	internal bool LowFrameRate => Time.unscaledTime - _lastMultipleTicksTime < 1f;

	public EstimatedTick LastPacketTick { get; internal set; } = new EstimatedTick();


	public uint Tick { get; internal set; }

	[HideInInspector]
	public double TickDelta { get; private set; }

	public bool FrameTicked { get; private set; }

	public float ServerUptime { get; private set; }

	public float ClientUptime { get; private set; }

	public ushort TickRate
	{
		get
		{
			return _tickRate;
		}
		private set
		{
			_tickRate = value;
		}
	}

	public byte PingInterval => _pingInterval;

	public PhysicsMode PhysicsMode => _physicsMode;

	public uint LocalTick
	{
		get
		{
			if (!NetworkManager.IsServerStarted)
			{
				return _localTick;
			}
			return Tick;
		}
		private set
		{
			_localTick = value;
		}
	}

	public event Action<long> OnRoundTripTimeUpdated;

	public event Action OnPreTick;

	public event Action OnTick;

	public event Action<float> OnPrePhysicsSimulation;

	public event Action<float> OnPostPhysicsSimulation;

	public event Action OnPostTick;

	public event Action OnUpdate;

	public event Action OnLateUpdate;

	public event Action OnFixedUpdate;

	public float GetPhysicsTimeScale()
	{
		return _physicsTimeScale;
	}

	public void SetPhysicsTimeScale(float value)
	{
		value = Mathf.Clamp(value, 0f, float.PositiveInfinity);
		_physicsTimeScale = value;
	}

	internal void TickFixedUpdate()
	{
		this.OnFixedUpdate?.Invoke();
		if (PhysicsMode == PhysicsMode.Unity)
		{
			if (_fixedUpdateTimeStep)
			{
				this.OnPostPhysicsSimulation?.Invoke(Time.fixedDeltaTime);
			}
			_fixedUpdateTimeStep = true;
			this.OnPrePhysicsSimulation?.Invoke(Time.fixedDeltaTime);
		}
	}

	internal void TickUpdate()
	{
		if (NetworkManager.IsServerStarted)
		{
			ServerUptime += Time.deltaTime;
		}
		if (NetworkManager.IsClientStarted)
		{
			ClientUptime += Time.deltaTime;
		}
		if (_updateOrder == UpdateOrder.BeforeTick)
		{
			this.OnUpdate?.Invoke();
			MethodLogic();
		}
		else
		{
			MethodLogic();
			this.OnUpdate?.Invoke();
		}
		void MethodLogic()
		{
			IncreaseTick();
			if (PhysicsMode == PhysicsMode.Unity && _fixedUpdateTimeStep)
			{
				_fixedUpdateTimeStep = false;
				this.OnPostPhysicsSimulation?.Invoke(Time.fixedDeltaTime);
			}
		}
	}

	internal void TickLateUpdate()
	{
		this.OnLateUpdate?.Invoke();
	}

	internal void InitializeOnce_Internal(NetworkManager networkManager)
	{
		NetworkManager = networkManager;
		LastPacketTick.Initialize(networkManager.TimeManager);
		SetInitialValues();
		networkManager.StatisticsManager.TryGetNetworkTrafficStatistics(out _networkTrafficStatistics);
		networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
		networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
		AddNetworkLoops();
	}

	private void AddNetworkLoops()
	{
		if (!base.gameObject.TryGetComponent<NetworkWriterLoop>(out var _))
		{
			base.gameObject.AddComponent<NetworkWriterLoop>();
		}
		if (!base.gameObject.TryGetComponent<NetworkReaderLoop>(out var _))
		{
			base.gameObject.AddComponent<NetworkReaderLoop>();
		}
	}

	private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
	{
		if (obj.ConnectionState != LocalConnectionState.Started)
		{
			if (!NetworkManager.IsServerStarted)
			{
				LastPacketTick.ResetTicks();
			}
			_pingStopwatch.Stop();
			ClientUptime = 0f;
			if (!NetworkManager.IsServerStarted)
			{
				LocalTick = 0u;
				Tick = 0u;
				SetTickRate(TickRate);
			}
		}
		else
		{
			_pingStopwatch.Restart();
		}
	}

	private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
	{
		if (!NetworkManager.ServerManager.IsAnyServerStarted())
		{
			LastPacketTick.ResetTicks();
			ServerUptime = 0f;
			Tick = 0u;
		}
	}

	private void SetInitialValues()
	{
		SetTickRate(TickRate);
		InitializePhysicsMode(PhysicsMode);
	}

	private void UnsetSimulationSettings()
	{
		SetAutomaticPhysicsSimulation(automatic: true);
		float @float = PlayerPrefs.GetFloat("SavedFixedTimeFN", float.MinValue);
		if (@float != float.MinValue)
		{
			Time.fixedDeltaTime = @float;
		}
	}

	private void SetAutomaticPhysicsSimulation(bool automatic)
	{
		if (automatic)
		{
			Physics.simulationMode = SimulationMode.FixedUpdate;
			Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
		}
		else
		{
			Physics.simulationMode = SimulationMode.Script;
			Physics2D.simulationMode = SimulationMode2D.Script;
		}
	}

	private void InitializePhysicsMode(PhysicsMode mode)
	{
		switch (mode)
		{
		case PhysicsMode.Disabled:
			SetPhysicsMode(mode);
			break;
		case PhysicsMode.TimeManager:
			Time.fixedDeltaTime = (float)TickDelta;
			if (NetworkManager != null)
			{
				if (_manualPhysics != 0)
				{
					NetworkManager.LogError("There are multiple TimeManagers instantiated which are using manual physics. Manual physics with multiple TimeManagers is not supported.");
				}
				_manualPhysics++;
			}
			SetPhysicsMode(mode);
			break;
		default:
			SetPhysicsMode(mode);
			break;
		}
	}

	public void SetPhysicsMode(PhysicsMode mode)
	{
		_physicsMode = mode;
		if (mode == PhysicsMode.Disabled || mode == PhysicsMode.TimeManager)
		{
			SetAutomaticPhysicsSimulation(automatic: false);
		}
		else
		{
			SetAutomaticPhysicsSimulation(automatic: true);
		}
	}

	internal void ModifyPing(uint clientTick)
	{
		uint num = LocalTick - clientTick;
		_pingAverage.ComputeAverage(num);
		double a = (double)_pingAverage.Average * TickDelta * 1000.0;
		RoundTripTime = (long)Math.Round(a);
		_receivedPong = true;
		this.OnRoundTripTimeUpdated?.Invoke(RoundTripTime);
	}

	private void TrySendPing(uint? tickOverride = null)
	{
		byte pingInterval = PingInterval;
		long num = pingInterval * 1000;
		float num2 = (_receivedPong ? 1f : 1.5f);
		num = (long)((float)num * num2);
		uint num3 = TimeToTicks((float)(int)pingInterval * num2);
		_pingTicks++;
		if (_pingTicks >= num3 && _pingStopwatch.ElapsedMilliseconds >= num)
		{
			_pingTicks = 0u;
			_pingStopwatch.Restart();
			_receivedPong = false;
			uint value = ((!tickOverride.HasValue) ? LocalTick : tickOverride.Value);
			PooledWriter pooledWriter = WriterPool.Retrieve();
			pooledWriter.WritePacketIdUnpacked(PacketId.PingPong);
			pooledWriter.WriteTickUnpacked(value);
			NetworkManager.TransportManager.SendToServer(1, pooledWriter.GetArraySegment());
			pooledWriter.Store();
		}
	}

	internal void SendPong(NetworkConnection conn, uint clientTick)
	{
		if (conn.IsActive && conn.IsAuthenticated)
		{
			PooledWriter pooledWriter = WriterPool.Retrieve();
			pooledWriter.WritePacketIdUnpacked(PacketId.PingPong);
			pooledWriter.WriteTickUnpacked(clientTick);
			conn.SendToClient(1, pooledWriter.GetArraySegment());
			pooledWriter.Store();
		}
	}

	private void IncreaseTick()
	{
		bool isClientStarted = NetworkManager.IsClientStarted;
		double num = (NetworkManager.IsServerStarted ? TickDelta : _adjustedTickDelta);
		if (num == 0.0)
		{
			NetworkManagerExtensions.LogWarning("Simulation delta cannot be 0. Network timing will not continue.");
			return;
		}
		double num2 = Time.unscaledDeltaTime;
		_elapsedTickTime += num2;
		FrameTicked = _elapsedTickTime >= num;
		int num3 = Mathf.FloorToInt((float)(_elapsedTickTime / num));
		if (num3 > 1)
		{
			_lastMultipleTicksTime = Time.unscaledTime;
		}
		if (_allowTickDropping && num3 > _maximumFrameTicks)
		{
			_elapsedTickTime = num * (double)(int)_maximumFrameTicks;
		}
		bool flag = _timingType == TimingType.Variable;
		bool frameTicked = FrameTicked;
		float num4 = (float)TickDelta * GetPhysicsTimeScale();
		do
		{
			if (frameTicked)
			{
				this.OnPreTick?.Invoke();
			}
			if (frameTicked || flag)
			{
				TryIterateData(incoming: true);
			}
			if (frameTicked)
			{
				NetworkManager.PredictionManager.ReconcileToStates();
				this.OnTick?.Invoke();
				if (PhysicsMode == PhysicsMode.TimeManager && num4 > 0f)
				{
					this.OnPrePhysicsSimulation?.Invoke(num4);
					Physics.Simulate(num4);
					Physics2D.Simulate(num4);
					this.OnPostPhysicsSimulation?.Invoke(num4);
				}
				this.OnPostTick?.Invoke();
				NetworkManager.PredictionManager.SendStateUpdate();
				bool flag2 = _elapsedTickTime < num * 2.0;
				if (isClientStarted && flag2)
				{
					TrySendPing(LocalTick + 1);
				}
				if (NetworkManager.IsServerStarted)
				{
					SendTimingAdjustment();
				}
			}
			if (frameTicked || flag)
			{
				TryIterateData(incoming: false);
			}
			if (frameTicked)
			{
				_elapsedTickTime -= num;
				Tick++;
				LocalTick++;
			}
		}
		while (_elapsedTickTime >= num);
	}

	public double GetTickPercentAsDouble()
	{
		if (NetworkManager == null)
		{
			return 0.0;
		}
		return _elapsedTickTime / TickDelta;
	}

	public double GetTickElapsedAsDouble()
	{
		return _elapsedTickTime;
	}

	public byte GetTickPercentAsByte()
	{
		return (byte)(GetTickPercentAsDouble() * 100.0);
	}

	public static double GetTickPercentAsDouble(byte value)
	{
		return (double)(int)value / 100.0;
	}

	public PreciseTick GetPreciseTick(uint tick)
	{
		if (NetworkManager == null)
		{
			return default(PreciseTick);
		}
		double num = (NetworkManager.IsServerStarted ? TickDelta : _adjustedTickDelta);
		double percent = _elapsedTickTime / num;
		return new PreciseTick(tick, percent);
	}

	public PreciseTick GetPreciseTick(TickType tickType)
	{
		if (NetworkManager == null)
		{
			return default(PreciseTick);
		}
		switch (tickType)
		{
		case TickType.Tick:
			return GetPreciseTick(Tick);
		case TickType.LocalTick:
			return GetPreciseTick(LocalTick);
		case TickType.LastPacketTick:
			return GetPreciseTick(LastPacketTick.LastRemoteTick);
		default:
			NetworkManager.LogError("TickType " + tickType.ToString() + " is unhandled.");
			return default(PreciseTick);
		}
	}

	public double TicksToTime(TickType tickType = TickType.LocalTick)
	{
		switch (tickType)
		{
		case TickType.LocalTick:
			return TicksToTime(LocalTick);
		case TickType.Tick:
			return TicksToTime(Tick);
		case TickType.LastPacketTick:
			return TicksToTime(LastPacketTick.LastRemoteTick);
		default:
			NetworkManager.LogError($"TickType {tickType} is unhandled.");
			return 0.0;
		}
	}

	public double TicksToTime(PreciseTick pt)
	{
		double num = TicksToTime(pt.Tick);
		double num2 = pt.PercentAsDouble * TickDelta;
		return num + num2;
	}

	public double TicksToTime(uint ticks)
	{
		return TickDelta * (double)ticks;
	}

	public double TimePassed(uint currentTick, uint previousTick)
	{
		double num;
		double num2;
		if (currentTick >= previousTick)
		{
			num = 1.0;
			num2 = TicksToTime(currentTick - previousTick);
		}
		else
		{
			num = -1.0;
			num2 = TicksToTime(previousTick - currentTick);
		}
		return num2 * num;
	}

	public double TimePassed(PreciseTick preciseTick, bool allowNegative = false)
	{
		PreciseTick preciseTick2 = GetPreciseTick(TickType.Tick);
		long num = (long)preciseTick2.Tick - (long)preciseTick.Tick;
		double num2 = preciseTick2.PercentAsDouble - preciseTick.PercentAsDouble;
		bool flag = num < 0 || (num <= 0 && num2 <= 0.0);
		if (!allowNegative && flag)
		{
			return 0.0;
		}
		double num3 = TimePassed(preciseTick.Tick, allowNegative: true);
		double num4 = num2 * TickDelta;
		return num3 + num4;
	}

	public double TimePassed(uint previousTick, bool allowNegative = false)
	{
		uint tick = Tick;
		if (tick >= previousTick)
		{
			return TicksToTime(tick - previousTick);
		}
		if (!allowNegative)
		{
			return 0.0;
		}
		return TicksToTime(previousTick - tick) * -1.0;
	}

	public uint TimeToTicks(double time, TickRounding rounding = TickRounding.RoundNearest)
	{
		double num = time / TickDelta;
		return rounding switch
		{
			TickRounding.RoundNearest => (uint)Math.Round(num), 
			TickRounding.RoundDown => (uint)Math.Floor(num), 
			_ => (uint)Math.Ceiling(num), 
		};
	}

	public uint TimeToTicks(long time, TickRounding rounding = TickRounding.RoundNearest)
	{
		double time2 = (double)time / 1000.0;
		return TimeToTicks(time2, rounding);
	}

	public PreciseTick TimeToPreciseTick(double time)
	{
		return time.AsPreciseTick(TickDelta);
	}

	public uint TickToLocalTick(uint tick)
	{
		if (NetworkManager.IsServerStarted)
		{
			return tick;
		}
		long num = Tick - tick;
		long num2 = LocalTick - num;
		if (num2 <= 0)
		{
			num2 = 0L;
		}
		return (uint)num2;
	}

	public uint LocalTickToTick(uint localTick)
	{
		if (NetworkManager.IsServerStarted)
		{
			return localTick;
		}
		long num = LocalTick - localTick;
		long num2 = Tick - num;
		if (num2 <= 0)
		{
			num2 = 0L;
		}
		return (uint)num2;
	}

	private void TryIterateData(bool incoming)
	{
		if (incoming)
		{
			int frameCount = Time.frameCount;
			if (frameCount != _lastIncomingIterationFrame)
			{
				_lastIncomingIterationFrame = frameCount;
				NetworkManager.TransportManager.IterateIncoming(asServer: true);
				NetworkManager.TransportManager.IterateIncoming(asServer: false);
			}
		}
		else
		{
			NetworkManager.TransportManager.IterateOutgoing(asServer: true);
			NetworkManager.TransportManager.IterateOutgoing(asServer: false);
		}
	}

	internal void ChangeAdjustedTickDelta(bool speedUp, double additionalMultiplier = 1.0)
	{
		double num = TickDelta * 0.01 * additionalMultiplier;
		if (speedUp)
		{
			_adjustedTickDelta -= num;
		}
		else
		{
			_adjustedTickDelta += num;
		}
	}

	private void SendTimingAdjustment()
	{
		if (LocalTick % TimingTickInterval != 0)
		{
			return;
		}
		PooledWriter pooledWriter = WriterPool.Retrieve();
		foreach (NetworkConnection value in NetworkManager.ServerManager.Clients.Values)
		{
			if (value.IsAuthenticated)
			{
				pooledWriter.WritePacketIdUnpacked(PacketId.TimingUpdate);
				pooledWriter.WriteTickUnpacked(value.PacketTick.Value());
				value.SendToClient(1, pooledWriter.GetArraySegment());
				pooledWriter.Clear();
			}
		}
		pooledWriter.Store();
	}

	internal void ParseTimingUpdate(Reader reader)
	{
		uint num = reader.ReadTickUnpacked();
		if (!NetworkManager.IsServerStarted && LocalTick >= num)
		{
			uint remoteTick = LastPacketTick.RemoteTick;
			uint tick = Tick;
			uint num2 = (LocalTick - num) / 2 + remoteTick + 1;
			long num3 = (long)num2 - (long)tick;
			Tick = num2;
			if (Mathf.Abs(num3) > 4f)
			{
				_adjustedTickDelta = TickDelta;
			}
			else if (num3 != 0L)
			{
				bool speedUp = num3 > 0;
				ChangeAdjustedTickDelta(speedUp);
			}
		}
	}

	public void SetTickRate(ushort value)
	{
		TickRate = value;
		TickDelta = 1.0 / (double)(int)TickRate;
		_adjustedTickDelta = TickDelta;
	}

	private void OnValidate()
	{
		SetInitialValues();
	}
}
