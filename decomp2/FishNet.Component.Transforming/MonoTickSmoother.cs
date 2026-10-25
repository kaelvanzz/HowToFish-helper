using FishNet.Managing.Logging;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Component.Transforming;

public class MonoTickSmoother : MonoBehaviour
{
	[Header("This component will be obsoleted soon.")]
	[Header("Use NetworkTickSmoother or OfflineTickSmoother.")]
	[Header(" ")]
	[Tooltip("True to use InstanceFinder to locate the TimeManager. When false specify which TimeManager to use by calling SetTimeManager.")]
	[SerializeField]
	private bool _useInstanceFinder = true;

	[Tooltip("GraphicalObject you wish to smooth.")]
	[SerializeField]
	private Transform _graphicalObject;

	[Tooltip("True to enable teleport threshold.")]
	[SerializeField]
	private bool _enableTeleport;

	[Tooltip("How far the object must move between ticks to teleport rather than smooth.")]
	[Range(0f, 65535f)]
	[SerializeField]
	private float _teleportThreshold;

	private TimeManager _timeManager;

	private LocalTransformTickSmoother _tickSmoother;

	private void OnEnable()
	{
		Initialize();
	}

	private void OnDisable()
	{
		_tickSmoother.ResetState();
		ChangeSubscription(subscribe: false);
		ObjectCaches<LocalTransformTickSmoother>.StoreAndDefault(ref _tickSmoother);
	}

	[Client(Logging = LoggingType.Off)]
	private void Update()
	{
		if (InstanceFinder.IsClientStarted)
		{
			_tickSmoother?.Update();
		}
	}

	private void Initialize()
	{
		_tickSmoother = ObjectCaches<LocalTransformTickSmoother>.Retrieve();
		if (_useInstanceFinder)
		{
			_timeManager = InstanceFinder.TimeManager;
			ChangeSubscription(subscribe: true);
		}
	}

	public void SetTimeManager(TimeManager tm)
	{
		if (!(tm == _timeManager))
		{
			ChangeSubscription(subscribe: false);
			_timeManager = tm;
			ChangeSubscription(subscribe: true);
		}
	}

	private void ChangeSubscription(bool subscribe)
	{
		if (_timeManager == null)
		{
			return;
		}
		if (subscribe)
		{
			if (_tickSmoother != null)
			{
				float teleportDistance = (_enableTeleport ? _teleportThreshold : float.NegativeInfinity);
				_tickSmoother.InitializeOnce(_graphicalObject, teleportDistance, (float)_timeManager.TickDelta, 1);
			}
			_timeManager.OnPreTick += _timeManager_OnPreTick;
			_timeManager.OnPostTick += _timeManager_OnPostTick;
		}
		else
		{
			_timeManager.OnPreTick -= _timeManager_OnPreTick;
			_timeManager.OnPostTick -= _timeManager_OnPostTick;
		}
	}

	private void _timeManager_OnPreTick()
	{
		_tickSmoother.OnPreTick();
	}

	private void _timeManager_OnPostTick()
	{
		_tickSmoother.OnPostTick();
	}
}
