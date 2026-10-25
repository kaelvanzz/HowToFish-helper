using FishNet.Managing.Timing;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Component.Transforming.Beta;

public class TickSmootherController : IResettable
{
	private InitializationSettings _initializationSettings;

	private MovementSettings _ownerMovementSettings;

	private MovementSettings _spectatorMovementSettings;

	private bool _destroyed;

	private TimeManager _timeManager;

	private NetworkBehaviour _initializingNetworkBehaviour;

	private Transform _graphicalTransform;

	private bool _initializedOffline;

	private bool _subscribedToAdaptiveEvents;

	private bool _subscribed;

	private bool _isInitialized;

	public UniversalTickSmoother UniversalSmoother { get; private set; }

	public void Initialize(InitializationSettings initializationSettings, MovementSettings ownerSettings, MovementSettings spectatorSettings)
	{
		_initializingNetworkBehaviour = initializationSettings.InitializingNetworkBehaviour;
		_graphicalTransform = initializationSettings.GraphicalTransform;
		_initializationSettings = initializationSettings;
		_ownerMovementSettings = ownerSettings;
		_spectatorMovementSettings = spectatorSettings;
		_initializedOffline = initializationSettings.InitializingNetworkBehaviour == null;
		_isInitialized = true;
	}

	public void OnDestroy()
	{
		ChangeSubscriptions(subscribe: false);
		StoreSmoother();
		_destroyed = true;
		_isInitialized = false;
	}

	public void StartSmoother()
	{
		if (_isInitialized && (_initializedOffline ? StartOffline() : StartOnline()))
		{
			RetrieveSmoothers();
			UniversalSmoother.Initialize(_initializationSettings, _ownerMovementSettings, _spectatorMovementSettings);
			UniversalSmoother.StartSmoother();
		}
		bool StartOffline()
		{
			if (_timeManager == null)
			{
				return false;
			}
			return true;
		}
		bool StartOnline()
		{
			NetworkBehaviour initializingNetworkBehaviour = _initializingNetworkBehaviour;
			SetTimeManager(initializingNetworkBehaviour.TimeManager);
			return true;
		}
	}

	public void StopSmoother()
	{
		ChangeSubscriptions(subscribe: false);
		if (!_initializedOffline)
		{
			StopOnline();
		}
		if (UniversalSmoother != null)
		{
			UniversalSmoother.StopSmoother();
		}
		void StopOnline()
		{
			SetTimeManager(null);
		}
	}

	public void TimeManager_OnUpdate()
	{
		UniversalSmoother.OnUpdate(Time.deltaTime);
	}

	public void TimeManager_OnPreTick()
	{
		UniversalSmoother.OnPreTick();
	}

	public void TimeManager_OnPostTick()
	{
		if (_timeManager != null)
		{
			UniversalSmoother.OnPostTick(_timeManager.LocalTick);
		}
	}

	private void PredictionManager_OnPostReplicateReplay(uint clientTick, uint serverTick)
	{
		UniversalSmoother.OnPostReplicateReplay(clientTick);
	}

	private void TimeManager_OnRoundTripTimeUpdated(long rttMs)
	{
		UniversalSmoother.UpdateRealtimeInterpolation();
	}

	private void StoreSmoother()
	{
		if (UniversalSmoother != null)
		{
			ResettableObjectCaches<UniversalTickSmoother>.Store(UniversalSmoother);
			UniversalSmoother = null;
		}
	}

	private void RetrieveSmoothers()
	{
		StoreSmoother();
		UniversalSmoother = ResettableObjectCaches<UniversalTickSmoother>.Retrieve();
	}

	public void SetTimeManager(TimeManager tm)
	{
		if (!(tm == _timeManager))
		{
			ChangeSubscriptions(subscribe: false);
			_timeManager = tm;
			ChangeSubscriptions(subscribe: true);
		}
	}

	private void ChangeSubscriptions(bool subscribe)
	{
		if (_destroyed)
		{
			return;
		}
		TimeManager timeManager = _timeManager;
		if (timeManager == null || subscribe == _subscribed)
		{
			return;
		}
		_subscribed = subscribe;
		bool flag = _ownerMovementSettings.AdaptiveInterpolationValue == AdaptiveInterpolationType.Off && _spectatorMovementSettings.AdaptiveInterpolationValue == AdaptiveInterpolationType.Off;
		if (subscribe)
		{
			timeManager.OnUpdate += TimeManager_OnUpdate;
			timeManager.OnPreTick += TimeManager_OnPreTick;
			timeManager.OnPostTick += TimeManager_OnPostTick;
			if (!flag)
			{
				timeManager.OnRoundTripTimeUpdated += TimeManager_OnRoundTripTimeUpdated;
				timeManager.NetworkManager.PredictionManager.OnPostReplicateReplay += PredictionManager_OnPostReplicateReplay;
				_subscribedToAdaptiveEvents = true;
			}
		}
		else
		{
			timeManager.OnUpdate -= TimeManager_OnUpdate;
			timeManager.OnPreTick -= TimeManager_OnPreTick;
			timeManager.OnPostTick -= TimeManager_OnPostTick;
			if (_subscribedToAdaptiveEvents)
			{
				timeManager.OnRoundTripTimeUpdated -= TimeManager_OnRoundTripTimeUpdated;
				timeManager.NetworkManager.PredictionManager.OnPostReplicateReplay -= PredictionManager_OnPostReplicateReplay;
			}
		}
	}

	public void ResetState()
	{
		_initializationSettings = default(InitializationSettings);
		_ownerMovementSettings = default(MovementSettings);
		_spectatorMovementSettings = default(MovementSettings);
		_destroyed = false;
		_timeManager = null;
		_initializingNetworkBehaviour = null;
		_graphicalTransform = null;
		_subscribed = false;
		_subscribedToAdaptiveEvents = false;
		_isInitialized = false;
	}

	public void InitializeState()
	{
	}
}
