using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Utility.Extension;
using UnityEngine;

namespace FishNet.Component.Transforming;

public class DetachableNetworkTickSmoother : NetworkBehaviour
{
	[Tooltip("True to attach the object to it's original parent when OnStopClient is called.")]
	[SerializeField]
	private bool _attachOnStop = true;

	[Tooltip("Object to follow, and smooth towards.")]
	[SerializeField]
	private Transform _followObject;

	[Tooltip("How many ticks to interpolate over.")]
	[Range(1f, 255f)]
	[SerializeField]
	private byte _interpolation = 1;

	[Tooltip("True to enable teleport threshold.")]
	[SerializeField]
	private bool _enableTeleport;

	[Tooltip("How far the object must move between ticks to teleport rather than smooth.")]
	[Range(0f, 65535f)]
	[SerializeField]
	private float _teleportThreshold;

	[Tooltip("True to synchronize the position of the followObject.")]
	[SerializeField]
	private bool _synchronizePosition = true;

	[Tooltip("True to synchronize the rotation of the followObject.")]
	[SerializeField]
	private bool _synchronizeRotation;

	[Tooltip("True to synchronize the scale of the followObject.")]
	[SerializeField]
	private bool _synchronizeScale;

	private TimeManager _timeManager;

	private Transform _parent;

	private TransformProperties _transformInstantiatedLocalProperties;

	private TransformProperties _postTickFollowObjectWorldProperties;

	private MoveRates _moveRates = new MoveRates(float.PositiveInfinity);

	private bool _initialized;

	private float _tickDelta;

	private bool NetworkInitialize___EarlyFishNet_002EComponent_002ETransforming_002EDetachableNetworkTickSmootherFishNet_002ERuntime_002Edll_Excuted;

	private bool NetworkInitialize___LateFishNet_002EComponent_002ETransforming_002EDetachableNetworkTickSmootherFishNet_002ERuntime_002Edll_Excuted;

	public virtual void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_FishNet_002EComponent_002ETransforming_002EDetachableNetworkTickSmoother_FishNet_002ERuntime_002Edll();
		NetworkInitialize___Late();
	}

	private void OnDestroy()
	{
		ChangeSubscription(subscribe: false);
	}

	public override void OnStartClient()
	{
		bool flag = false;
		if (base.transform.parent == null)
		{
			NetworkManagerExtensions.LogError(GetType().Name + " on gameObject " + base.gameObject.name + " requires a parent to detach from.");
			flag = true;
		}
		if (_followObject == null)
		{
			NetworkManagerExtensions.LogError($"{GetType().Name} on gameObject {base.gameObject}, root {base.transform.root} requires followObject to be set.");
			flag = true;
		}
		if (!flag)
		{
			_parent = base.transform.parent;
			base.transform.SetParent(null);
			SetTimeManager(base.TimeManager);
			ChangeSubscription(subscribe: false);
			ChangeSubscription(subscribe: true);
			_postTickFollowObjectWorldProperties = _followObject.GetWorldProperties();
			_tickDelta = (float)base.TimeManager.TickDelta;
			_initialized = true;
		}
	}

	public override void OnStopClient()
	{
		if (_attachOnStop && _parent != null)
		{
			base.transform.SetParent(_parent);
			base.transform.SetLocalProperties(_transformInstantiatedLocalProperties);
		}
		_postTickFollowObjectWorldProperties.ResetState();
		ChangeSubscription(subscribe: false);
		_initialized = false;
	}

	[Client(Logging = LoggingType.Off)]
	private void Update()
	{
		if (!GetIsNetworked() || base.IsClientInitialized)
		{
			MoveTowardsFollowTarget();
		}
	}

	private void _timeManager_OnPostTick()
	{
		if (_initialized)
		{
			_postTickFollowObjectWorldProperties.Update(_followObject);
			if (!_synchronizePosition)
			{
				_postTickFollowObjectWorldProperties.Position = base.transform.position;
			}
			if (!_synchronizeRotation)
			{
				_postTickFollowObjectWorldProperties.Rotation = base.transform.rotation;
			}
			if (!_synchronizeScale)
			{
				_postTickFollowObjectWorldProperties.Scale = base.transform.localScale;
			}
			SetMoveRates();
		}
	}

	private void SetTimeManager(TimeManager tm)
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
		if (!(_timeManager == null))
		{
			if (subscribe)
			{
				_timeManager.OnPostTick += _timeManager_OnPostTick;
			}
			else
			{
				_timeManager.OnPostTick -= _timeManager_OnPostTick;
			}
		}
	}

	private void MoveTowardsFollowTarget()
	{
		if (_initialized)
		{
			_moveRates.Move(base.transform, _postTickFollowObjectWorldProperties, Time.deltaTime, useWorldSpace: true);
		}
	}

	private void SetMoveRates()
	{
		if (_initialized)
		{
			float num = _tickDelta * (float)(int)_interpolation;
			if (_interpolation == 1)
			{
				num += Mathf.Max(Time.deltaTime, 0.02f);
			}
			float teleportThreshold = (_enableTeleport ? _teleportThreshold : float.NegativeInfinity);
			_moveRates = MoveRates.GetWorldMoveRates(base.transform, _followObject, num, teleportThreshold);
		}
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyFishNet_002EComponent_002ETransforming_002EDetachableNetworkTickSmootherFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___EarlyFishNet_002EComponent_002ETransforming_002EDetachableNetworkTickSmootherFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateFishNet_002EComponent_002ETransforming_002EDetachableNetworkTickSmootherFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___LateFishNet_002EComponent_002ETransforming_002EDetachableNetworkTickSmootherFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	private void Awake_UserLogic_FishNet_002EComponent_002ETransforming_002EDetachableNetworkTickSmoother_FishNet_002ERuntime_002Edll()
	{
		_transformInstantiatedLocalProperties = base.transform.GetLocalProperties();
	}
}
