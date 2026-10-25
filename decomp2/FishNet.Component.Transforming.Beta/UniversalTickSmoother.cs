using System;
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.Scripting;

namespace FishNet.Component.Transforming.Beta;

public sealed class UniversalTickSmoother : IResettable
{
	[Preserve]
	private struct TickTransformProperties
	{
		public readonly uint Tick;

		public readonly TransformProperties Properties;

		public TickTransformProperties(uint tick, Transform t)
		{
			Tick = tick;
			Properties = new TransformProperties(t.localPosition, t.localRotation, t.localScale);
		}

		public TickTransformProperties(uint tick, Transform t, Vector3 localScale)
		{
			Tick = tick;
			Properties = new TransformProperties(t.localPosition, t.localRotation, localScale);
		}

		public TickTransformProperties(uint tick, TransformProperties tp)
		{
			Tick = tick;
			Properties = tp;
		}

		public TickTransformProperties(uint tick, TransformProperties tp, Vector3 localScale)
		{
			Tick = tick;
			tp.Scale = localScale;
			Properties = tp;
		}
	}

	private MoveRates _moveRates;

	private bool _preTicked;

	private TransformProperties _trackerPreTickWorldValues;

	private TransformProperties _graphicsPreTickWorldValues;

	private AdaptiveInterpolationType _cachedAdaptiveInterpolationValue;

	private byte _cachedInterpolationValue;

	private TransformPropertiesFlag _cachedSmoothedProperties;

	private bool _cachedSnapNonSmoothedProperties;

	private float _cachedTeleportThreshold;

	private bool _detachOnStart;

	private bool _attachOnStop;

	private bool _moveImmediately;

	private Transform _targetTransform;

	private Transform _graphicalTransform;

	private Transform _trackerTransform;

	private float _tickDelta;

	private NetworkBehaviour _initializingNetworkBehaviour;

	private TimeManager _initializingTimeManager;

	private float _movementMultiplier = 1f;

	private BasicQueue<TickTransformProperties> _transformProperties;

	private bool _useOwnerSettings;

	private uint _teleportedTick;

	private byte _realtimeInterpolation;

	private MovementSettings _controllerMovementSettings;

	private MovementSettings _spectatorMovementSettings;

	private bool _isMoving;

	private const int MAXIMUM_QUEUED_OVER_INTERPOLATION = 3;

	private TransformProperties? _queuedTrackerProperties;

	public bool IsInitialized { get; private set; }

	[Preserve]
	public UniversalTickSmoother()
	{
	}

	~UniversalTickSmoother()
	{
		ResetState();
	}

	[Obsolete("This method is no longer used. Use TrySetGraphicalTrackerLocalProperties(TransformProperties).")]
	public void SetGraphicalInitializedOffsetValues(TransformProperties value)
	{
	}

	[Obsolete("This method is no longer used. Use GetGraphicalTrackerLocalProperties.")]
	public TransformProperties GetGraphicalInitializedOffsetValues()
	{
		return default(TransformProperties);
	}

	public bool TrySetGraphicalTrackerLocalProperties(TransformProperties? localValues)
	{
		if (_trackerTransform == null || !localValues.HasValue)
		{
			_queuedTrackerProperties = localValues;
			return false;
		}
		_trackerTransform.SetLocalProperties(localValues.Value);
		return true;
	}

	[Obsolete("This method is no longer used. Use TrySetGraphicalTrackerLocalProperties(TransformProperties).")]
	public void SetAdditionalGraphicalOffsetValues(TransformProperties localValues)
	{
	}

	[Obsolete("This method is no longer used. Use GetGraphicalTrackerLocalProperties.")]
	public TransformProperties GetAdditionalGraphicalOffsetValues()
	{
		return default(TransformProperties);
	}

	public TransformProperties GetGraphicalTrackerLocalProperties()
	{
		if (_trackerTransform != null)
		{
			return new TransformProperties(_trackerTransform.localPosition, _trackerTransform.localRotation, _trackerTransform.localScale);
		}
		if (_queuedTrackerProperties.HasValue)
		{
			return _queuedTrackerProperties.Value;
		}
		((_initializingNetworkBehaviour == null) ? null : _initializingNetworkBehaviour.NetworkManager).LogWarning("Graphical tracker properties cannot be returned because tracker is not setup yet, and no setup properties have been specified. Use TrySetGraphicalTrackerProperties to set setup properties or call this method after IsInitialized is true.");
		return default(TransformProperties);
	}

	public void SetSmoothedProperties(TransformPropertiesFlag value, bool forOwnerOrOfflineSmoother)
	{
		_controllerMovementSettings.SmoothedProperties = value;
		SetCaches(forOwnerOrOfflineSmoother);
	}

	public void SetInterpolationValue(byte value, bool forOwnerOrOfflineSmoother)
	{
		SetInterpolationValue(value, forOwnerOrOfflineSmoother, unsetAdaptiveInterpolation: true);
	}

	private void SetInterpolationValue(byte value, bool forOwnerOrOfflineSmoother, bool unsetAdaptiveInterpolation)
	{
		if (value < 1)
		{
			value = 1;
		}
		if (forOwnerOrOfflineSmoother)
		{
			_controllerMovementSettings.InterpolationValue = value;
		}
		else
		{
			_spectatorMovementSettings.InterpolationValue = value;
		}
		if (unsetAdaptiveInterpolation)
		{
			SetAdaptiveInterpolation(AdaptiveInterpolationType.Off, forOwnerOrOfflineSmoother);
		}
	}

	public void SetAdaptiveInterpolation(AdaptiveInterpolationType value, bool forOwnerOrOfflineSmoother)
	{
		if (forOwnerOrOfflineSmoother)
		{
			_controllerMovementSettings.AdaptiveInterpolationValue = value;
		}
		else
		{
			_spectatorMovementSettings.AdaptiveInterpolationValue = value;
		}
		UpdateRealtimeInterpolation();
	}

	public void Initialize(InitializationSettings initializationSettings, MovementSettings ownerSettings, MovementSettings spectatorSettings)
	{
		ResetState();
		Transform graphicalTransform = initializationSettings.GraphicalTransform;
		Transform targetTransform = initializationSettings.TargetTransform;
		if (TransformsAreValid(graphicalTransform, targetTransform))
		{
			_transformProperties = CollectionCaches<TickTransformProperties>.RetrieveBasicQueue();
			_controllerMovementSettings = ownerSettings;
			_spectatorMovementSettings = spectatorSettings;
			if (!initializationSettings.DetachOnStart)
			{
				_controllerMovementSettings.SmoothedProperties &= ~TransformPropertiesFlag.Scale;
				_spectatorMovementSettings.SmoothedProperties &= ~TransformPropertiesFlag.Scale;
			}
			_initializingNetworkBehaviour = initializationSettings.InitializingNetworkBehaviour;
			_initializingTimeManager = initializationSettings.InitializingTimeManager;
			_targetTransform = targetTransform;
			_graphicalTransform = graphicalTransform;
			_tickDelta = (float)initializationSettings.InitializingTimeManager.TickDelta;
			_detachOnStart = initializationSettings.DetachOnStart;
			_attachOnStop = initializationSettings.AttachOnStop;
			_moveImmediately = initializationSettings.MoveImmediately;
			SetCaches(GetUseOwnerSettings());
			SetInterpolationValue(_controllerMovementSettings.InterpolationValue, forOwnerOrOfflineSmoother: true, unsetAdaptiveInterpolation: false);
			SetInterpolationValue(_spectatorMovementSettings.InterpolationValue, forOwnerOrOfflineSmoother: false, unsetAdaptiveInterpolation: false);
			SetAdaptiveInterpolation(_controllerMovementSettings.AdaptiveInterpolationValue, forOwnerOrOfflineSmoother: true);
			SetAdaptiveInterpolation(_spectatorMovementSettings.AdaptiveInterpolationValue, forOwnerOrOfflineSmoother: false);
			SetupTrackerTransform();
			if (_queuedTrackerProperties.HasValue)
			{
				TrySetGraphicalTrackerLocalProperties(_queuedTrackerProperties.Value);
			}
			IsInitialized = true;
		}
		void SetupTrackerTransform()
		{
			_trackerTransform = new GameObject(_graphicalTransform.name + "_Tracker").transform;
			if (_detachOnStart)
			{
				_trackerTransform.SetParent(_targetTransform);
			}
			else
			{
				Transform parent = (_graphicalTransform.IsChildOf(targetTransform) ? _graphicalTransform.parent : targetTransform);
				_trackerTransform.SetParent(parent);
			}
			_trackerTransform.SetLocalPositionRotationAndScale(_graphicalTransform.localPosition, graphicalTransform.localRotation, graphicalTransform.localScale);
		}
	}

	private bool TransformsAreValid(Transform graphicalTransform, Transform targetTransform)
	{
		if (graphicalTransform == null)
		{
			NetworkManagerExtensions.LogError("Graphical transform cannot be null.");
			return false;
		}
		if (targetTransform == null)
		{
			NetworkManagerExtensions.LogError($"Target transform on {graphicalTransform} cannot be null.");
			return false;
		}
		if (targetTransform == graphicalTransform)
		{
			NetworkManagerExtensions.LogError($"Target transform cannot be the same as graphical transform on {graphicalTransform}.");
			return false;
		}
		return true;
	}

	private bool GetUseAdaptiveInterpolation()
	{
		if (_cachedAdaptiveInterpolationValue == AdaptiveInterpolationType.Off || _initializingTimeManager.NetworkManager.IsServerOnlyStarted)
		{
			return false;
		}
		return true;
	}

	private bool GetUseOwnerSettings()
	{
		if (!(_initializingNetworkBehaviour == null) && !_initializingNetworkBehaviour.IsOwner)
		{
			return !_initializingNetworkBehaviour.Owner.IsValid;
		}
		return true;
	}

	private void SetUseOwnerSettings(bool value, bool force = false)
	{
		if (value != _useOwnerSettings || force)
		{
			_useOwnerSettings = value;
			SetCaches(value);
		}
	}

	private void SetCaches(bool useOwnerSettings)
	{
		MovementSettings movementSettings = (useOwnerSettings ? _controllerMovementSettings : _spectatorMovementSettings);
		_cachedSmoothedProperties = movementSettings.SmoothedProperties;
		_cachedSnapNonSmoothedProperties = movementSettings.SnapNonSmoothedProperties;
		_cachedAdaptiveInterpolationValue = movementSettings.AdaptiveInterpolationValue;
		_cachedInterpolationValue = movementSettings.InterpolationValue;
		_cachedTeleportThreshold = (movementSettings.EnableTeleport ? (movementSettings.TeleportThreshold * movementSettings.TeleportThreshold) : float.NegativeInfinity);
	}

	public void Deinitialize()
	{
		ResetState();
		IsInitialized = false;
	}

	public void UpdateRealtimeInterpolation()
	{
		if (!GetUseAdaptiveInterpolation())
		{
			_realtimeInterpolation = _cachedInterpolationValue;
			return;
		}
		TimeManager initializingTimeManager = _initializingTimeManager;
		uint localTick = initializingTimeManager.LocalTick;
		if (localTick != 0)
		{
			long roundTripTime = initializingTimeManager.RoundTripTime;
			uint num = initializingTimeManager.TimeToTicks(roundTripTime) + 1;
			uint num2 = localTick - num;
			float num3 = localTick - num2;
			num3 += (float)(int)(byte)_cachedAdaptiveInterpolationValue;
			if (num3 > (float)(int)initializingTimeManager.TickRate)
			{
				num3 = (int)initializingTimeManager.TickRate;
			}
			else if (num3 > 255f)
			{
				num3 = 255f;
			}
			if (_realtimeInterpolation == 0 || Math.Abs((float)(int)_realtimeInterpolation - num3) > 1f)
			{
				_realtimeInterpolation = (byte)Math.Ceiling(num3);
			}
		}
	}

	public void StartSmoother()
	{
		DetachOnStart();
	}

	internal void StopSmoother()
	{
		AttachOnStop();
	}

	public void OnUpdate(float delta)
	{
		if (CanSmooth())
		{
			MoveToTarget(delta);
		}
	}

	public void OnPreTick()
	{
		if (CanSmooth())
		{
			SetUseOwnerSettings(GetUseOwnerSettings());
			_preTicked = true;
			DiscardExcessiveTransformPropertiesQueue();
			_graphicsPreTickWorldValues = _graphicalTransform.GetWorldProperties();
			_trackerPreTickWorldValues = GetTrackerWorldProperties();
		}
	}

	public void OnPostReplicateReplay(uint clientTick)
	{
		if (NetworkObjectIsReconciling() && _transformProperties.Count != 0 && clientTick > _teleportedTick)
		{
			uint tick = _transformProperties.Peek().Tick;
			if (clientTick > tick)
			{
				ModifyTransformProperties(clientTick, tick);
			}
		}
	}

	public void OnPostTick(uint clientTick)
	{
		if (!CanSmooth() || clientTick <= _teleportedTick)
		{
			return;
		}
		if (_preTicked)
		{
			DiscardExcessiveTransformPropertiesQueue();
			if (!_detachOnStart)
			{
				_graphicalTransform.SetWorldProperties(_graphicsPreTickWorldValues);
			}
			AddTransformProperties(clientTick);
		}
		else if (!_detachOnStart)
		{
			_graphicalTransform.SetWorldProperties(GetTrackerWorldProperties());
		}
	}

	private void SnapNonSmoothedProperties()
	{
		if (!_cachedSnapNonSmoothedProperties)
		{
			return;
		}
		TransformPropertiesFlag cachedSmoothedProperties = _cachedSmoothedProperties;
		if (cachedSmoothedProperties != TransformPropertiesFlag.Everything)
		{
			TransformProperties trackerWorldProperties = GetTrackerWorldProperties();
			if (!cachedSmoothedProperties.FastContains(TransformPropertiesFlag.Position))
			{
				_graphicalTransform.position = trackerWorldProperties.Position;
			}
			if (!cachedSmoothedProperties.FastContains(TransformPropertiesFlag.Rotation))
			{
				_graphicalTransform.rotation = trackerWorldProperties.Rotation;
			}
			if (!cachedSmoothedProperties.FastContains(TransformPropertiesFlag.Scale))
			{
				_graphicalTransform.localScale = trackerWorldProperties.Scale;
			}
		}
	}

	private bool NetworkObjectIsReconciling()
	{
		if (!(_initializingNetworkBehaviour == null))
		{
			return _initializingNetworkBehaviour.NetworkObject.IsObjectReconciling;
		}
		return true;
	}

	public void Teleport()
	{
		if (_initializingTimeManager == null)
		{
			return;
		}
		if (_controllerMovementSettings.AdaptiveInterpolationValue != 0)
		{
			TimeManager timeManager = ((_initializingTimeManager == null) ? InstanceFinder.TimeManager : _initializingTimeManager);
			if (timeManager != null)
			{
				_teleportedTick = timeManager.LocalTick;
			}
		}
		ClearTransformPropertiesQueue();
		_graphicalTransform.SetWorldProperties(_trackerTransform.GetWorldProperties());
	}

	private void ClearTransformPropertiesQueue()
	{
		_transformProperties.Clear();
		_moveRates = new MoveRates(float.NegativeInfinity);
	}

	private void DiscardExcessiveTransformPropertiesQueue()
	{
		int num = _transformProperties.Count - (_realtimeInterpolation + 3);
		if (num > 0)
		{
			TickTransformProperties tickTransformProperties = default(TickTransformProperties);
			for (int i = 0; i < num; i++)
			{
				tickTransformProperties = _transformProperties.Dequeue();
			}
			SetMoveRates(in tickTransformProperties.Properties);
		}
	}

	private void AddTransformProperties(uint tick)
	{
		TickTransformProperties data = new TickTransformProperties(tick, GetTrackerWorldProperties());
		_transformProperties.Enqueue(data);
		if (_transformProperties.Count == 1)
		{
			TransformProperties prevValues = _graphicalTransform.GetWorldProperties();
			SetMoveRates(in prevValues);
		}
	}

	private void ModifyTransformProperties(uint clientTick, uint firstTick)
	{
		int count = _transformProperties.Count;
		int num = (int)(clientTick - firstTick);
		if (num >= count || clientTick != _transformProperties[num].Tick)
		{
			return;
		}
		TransformProperties trackerWorldProperties = GetTrackerWorldProperties();
		int num2 = count - 1 - 1;
		if (num2 < 1)
		{
			num2 = 1;
		}
		float num3 = (float)num / (float)num2;
		if (num3 < 1f)
		{
			if (num3 < 1f)
			{
				num3 = (float)Math.Pow(num3, num2 - num);
			}
			TransformProperties properties = _transformProperties[num].Properties;
			trackerWorldProperties.Position = Vector3.Lerp(properties.Position, trackerWorldProperties.Position, num3);
			trackerWorldProperties.Rotation = Quaternion.Lerp(properties.Rotation, trackerWorldProperties.Rotation, num3);
			trackerWorldProperties.Scale = Vector3.Lerp(properties.Scale, trackerWorldProperties.Scale, num3);
		}
		_transformProperties[num] = new TickTransformProperties(clientTick, trackerWorldProperties);
	}

	private TransformProperties GetTrackerWorldProperties()
	{
		Vector3 localScale = (_detachOnStart ? _trackerTransform.lossyScale : _trackerTransform.localScale);
		return new TransformProperties(_trackerTransform.position, _trackerTransform.rotation, localScale);
	}

	private bool CanSmooth()
	{
		if (_graphicalTransform == null)
		{
			return false;
		}
		return _initializingTimeManager.NetworkManager.IsClientStarted;
	}

	private void SetMoveRates(in TransformProperties prevValues)
	{
		if (_transformProperties.Count == 0)
		{
			_moveRates = new MoveRates(float.NegativeInfinity);
			return;
		}
		TransformProperties properties = _transformProperties.Peek().Properties;
		float tickDelta = _tickDelta;
		_moveRates = MoveRates.GetMoveRates(prevValues, properties, tickDelta, _cachedTeleportThreshold);
		_moveRates.TimeRemaining = tickDelta;
		SetMovementMultiplier();
	}

	private void SetMovementMultiplier()
	{
		if (_moveImmediately)
		{
			float movementMultiplier = Mathf.InverseLerp(0f, (int)_realtimeInterpolation, _transformProperties.Count);
			_movementMultiplier = movementMultiplier;
			_movementMultiplier = Mathf.Clamp(_movementMultiplier, 0.5f, 1.05f);
			return;
		}
		int num = _transformProperties.Count - _realtimeInterpolation;
		if (num != 0)
		{
			_movementMultiplier += 0.015f * (float)num;
		}
		else if (_realtimeInterpolation == 1)
		{
			_movementMultiplier = 1f;
		}
		_movementMultiplier = Mathf.Clamp(_movementMultiplier, 0.95f, 1.05f);
	}

	private void MoveToTarget(float delta)
	{
		int count = _transformProperties.Count;
		if (count == 0)
		{
			return;
		}
		if (_moveImmediately)
		{
			_isMoving = true;
		}
		else if (count >= _realtimeInterpolation)
		{
			_isMoving = true;
		}
		else
		{
			if (!_isMoving)
			{
				return;
			}
			if (count - _realtimeInterpolation < -4)
			{
				_isMoving = false;
				return;
			}
		}
		TickTransformProperties tickTransformProperties = _transformProperties.Peek();
		TransformPropertiesFlag cachedSmoothedProperties = _cachedSmoothedProperties;
		_moveRates.Move(_graphicalTransform, tickTransformProperties.Properties, cachedSmoothedProperties, delta * _movementMultiplier, useWorldSpace: true);
		float timeRemaining = _moveRates.TimeRemaining;
		if (!(timeRemaining <= 0f))
		{
			return;
		}
		_transformProperties.Dequeue();
		if (_transformProperties.Count > 0)
		{
			SetMoveRates(in tickTransformProperties.Properties);
			if (timeRemaining < 0f)
			{
				MoveToTarget(Mathf.Abs(timeRemaining));
			}
		}
		else
		{
			ClearTransformPropertiesQueue();
		}
	}

	private void DetachOnStart()
	{
		if (_detachOnStart)
		{
			TransformProperties worldProperties = _graphicalTransform.GetWorldProperties();
			_graphicalTransform.SetParent(null);
			_graphicalTransform.SetWorldProperties(worldProperties);
		}
	}

	private void AttachOnStop()
	{
		if (_detachOnStart && !(_graphicalTransform == null) && !ApplicationState.IsQuitting())
		{
			if (!_attachOnStop || _targetTransform == null)
			{
				UnityEngine.Object.Destroy(_graphicalTransform.gameObject);
				return;
			}
			_graphicalTransform.SetParent(_targetTransform.parent);
			_graphicalTransform.SetLocalProperties(_trackerTransform.GetLocalProperties());
		}
	}

	public void ResetState()
	{
		if (IsInitialized)
		{
			AttachOnStop();
			_initializingNetworkBehaviour = null;
			_initializingTimeManager = null;
			_graphicalTransform = null;
			_targetTransform = null;
			_teleportedTick = 0u;
			_movementMultiplier = 1f;
			CollectionCaches<TickTransformProperties>.StoreAndDefault(ref _transformProperties);
			_moveRates = default(MoveRates);
			_preTicked = false;
			_queuedTrackerProperties = null;
			_trackerPreTickWorldValues = default(TransformProperties);
			_graphicsPreTickWorldValues = default(TransformProperties);
			_realtimeInterpolation = 0;
			_isMoving = false;
			if (_trackerTransform != null)
			{
				UnityEngine.Object.Destroy(_trackerTransform.gameObject);
			}
		}
	}

	public void InitializeState()
	{
	}
}
