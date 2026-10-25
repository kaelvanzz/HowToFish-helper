using System;
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.Scripting;

namespace FishNet.Component.Transforming;

[Obsolete("This class will be removed in version 5.")]
public sealed class TransformTickSmoother : IResettable
{
	private enum InitializeType
	{
		Unset,
		Networked,
		NonNetworked
	}

	[Preserve]
	private struct TickTransformProperties
	{
		public uint Tick;

		public TransformProperties Properties;

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

	private Transform _graphicalObject;

	private float _teleportThreshold;

	private MoveRates _moveRates = new MoveRates(float.NegativeInfinity);

	private bool _preTicked;

	private TransformProperties _gfxInitializedOffsetValues;

	private TransformProperties _gfxPreSimulateWorldValues;

	private float _tickDelta;

	private byte _ownerInterpolation;

	private byte _interpolation;

	private NetworkObject _networkObject;

	private float _movementMultiplier = 1f;

	private BasicQueue<TickTransformProperties> _transformProperties;

	private TransformPropertiesFlag _ownerSmoothedProperties;

	private TransformPropertiesFlag _spectatorSmoothedProperties;

	private AdaptiveInterpolationType _adaptiveInterpolation = AdaptiveInterpolationType.VeryLow;

	private byte _spectatorInterpolation;

	private Transform _previousParent;

	private bool _detach;

	private bool _useOwnerSmoothing;

	private InitializeType _initializeType;

	private uint _teleportedTick;

	private uint _lastReconcileTick;

	private Transform _rootTransform;

	private int _startFrame;

	private const int RECONCILE_INTERVAL_DEFAULT = int.MaxValue;

	private const int MAXIMUM_QUEUED_OVER_INTERPOLATION = 3;

	public void SetSmoothedProperties(TransformPropertiesFlag value, bool forSpectator)
	{
		if (forSpectator)
		{
			_spectatorSmoothedProperties = value;
		}
		else
		{
			_ownerSmoothedProperties = value;
		}
	}

	public void SetAdaptiveInterpolation(AdaptiveInterpolationType adaptiveInterpolation)
	{
		_adaptiveInterpolation = adaptiveInterpolation;
	}

	public void SetSpectatorInterpolation(byte value, bool disableAdaptiveInterpolation = true)
	{
		_spectatorInterpolation = value;
		if (disableAdaptiveInterpolation)
		{
			_adaptiveInterpolation = AdaptiveInterpolationType.Off;
		}
	}

	[Preserve]
	public TransformTickSmoother()
	{
	}

	~TransformTickSmoother()
	{
		ResetState();
	}

	public void InitializeNetworked(NetworkObject nob, Transform graphicalObject, bool detach, float teleportDistance, float tickDelta, byte ownerInterpolation, TransformPropertiesFlag ownerSmoothedProperties, byte spectatorInterpolation, TransformPropertiesFlag specatorSmoothedProperties, AdaptiveInterpolationType adaptiveInterpolation)
	{
		ResetState();
		_networkObject = nob;
		_spectatorInterpolation = spectatorInterpolation;
		_spectatorSmoothedProperties = specatorSmoothedProperties;
		Initialize_Internal(nob.transform, graphicalObject, detach, teleportDistance, tickDelta, ownerInterpolation, ownerSmoothedProperties, forNetworked: true);
		SetAdaptiveInterpolation(adaptiveInterpolation);
		UpdateInterpolation(0u);
	}

	private void Initialize_Internal(Transform rootTransform, Transform graphicalObject, bool detach, float teleportDistance, float tickDelta, byte ownerInterpolation, TransformPropertiesFlag ownerSmoothedProperties, bool forNetworked)
	{
		_rootTransform = rootTransform;
		_detach = detach;
		_transformProperties = CollectionCaches<TickTransformProperties>.RetrieveBasicQueue();
		_gfxInitializedOffsetValues = rootTransform.GetTransformOffsets(graphicalObject);
		_tickDelta = tickDelta;
		_graphicalObject = graphicalObject;
		_teleportThreshold = teleportDistance;
		_ownerInterpolation = ownerInterpolation;
		_ownerSmoothedProperties = ownerSmoothedProperties;
		_initializeType = (forNetworked ? InitializeType.Networked : InitializeType.NonNetworked);
	}

	public void Initialize(Transform rootTransform, Transform graphicalObject, bool detach, float teleportDistance, float tickDelta, byte ownerInterpolation, TransformPropertiesFlag ownerSmoothedProperties)
	{
		ResetState();
		Initialize_Internal(rootTransform, graphicalObject, detach, teleportDistance, tickDelta, ownerInterpolation, ownerSmoothedProperties, forNetworked: false);
		SetAdaptiveInterpolation(AdaptiveInterpolationType.Off);
	}

	public void Deinitialize()
	{
		ResetState();
	}

	private void UpdateInterpolation(uint clientStateTick)
	{
		if (_initializeType == InitializeType.NonNetworked || _networkObject.IsServerInitialized || _networkObject.Owner.IsLocalClient)
		{
			_interpolation = _ownerInterpolation;
			return;
		}
		if (_adaptiveInterpolation == AdaptiveInterpolationType.Off)
		{
			_interpolation = _spectatorInterpolation;
			return;
		}
		TimeManager timeManager = _networkObject.TimeManager;
		float num = ((clientStateTick != 0) ? ((float)(timeManager.LocalTick - clientStateTick)) : ((float)timeManager.RoundTripTime / 10f));
		num *= GetInterpolationMultiplier();
		num = Mathf.Clamp(num, 2f, 255f);
		_interpolation = (byte)Mathf.CeilToInt(num);
		float GetInterpolationMultiplier()
		{
			switch (_adaptiveInterpolation)
			{
			case AdaptiveInterpolationType.ExtremelyLow:
				return 0.2f;
			case AdaptiveInterpolationType.VeryLow:
				return 0.45f;
			case AdaptiveInterpolationType.Low:
				return 0.8f;
			case AdaptiveInterpolationType.Moderate:
				return 1.05f;
			case AdaptiveInterpolationType.High:
				return 1.25f;
			case AdaptiveInterpolationType.VeryHigh:
				return 1.5f;
			default:
				_networkObject.NetworkManager.LogError($"AdaptiveInterpolationType {_adaptiveInterpolation} is unhandled.");
				return 1f;
			}
		}
	}

	internal void OnStartClient()
	{
		if (_detach)
		{
			_previousParent = _graphicalObject.parent;
			TransformProperties worldProperties = _graphicalObject.GetWorldProperties();
			_graphicalObject.SetParent(null);
			_graphicalObject.SetWorldProperties(worldProperties);
		}
	}

	internal void OnStopClient()
	{
		if (_detach && !(_previousParent == null) && !(_graphicalObject == null))
		{
			_graphicalObject.SetParent(_previousParent);
			_graphicalObject.SetWorldProperties(GetNetworkObjectWorldPropertiesWithOffset());
		}
	}

	internal void OnUpdate()
	{
		if (CanSmooth())
		{
			MoveToTarget(Time.deltaTime);
		}
	}

	public void OnPreTick()
	{
		if (CanSmooth())
		{
			_preTicked = true;
			_useOwnerSmoothing = _networkObject == null || _networkObject.IsOwner;
			DiscardExcessiveTransformPropertiesQueue();
			if (!_detach)
			{
				_gfxPreSimulateWorldValues = _graphicalObject.GetWorldProperties();
			}
		}
	}

	public void OnPreReconcile()
	{
		if (_networkObject.IsObjectReconciling && !_networkObject.IsOwner && _adaptiveInterpolation != 0)
		{
			UpdateInterpolation(_lastReconcileTick = _networkObject.PredictionManager.ClientStateTick);
		}
	}

	public void OnPostReplicateReplay(uint clientTick)
	{
		if (!_networkObject.IsOwner && _adaptiveInterpolation != 0 && _transformProperties.Count != 0 && clientTick > _teleportedTick)
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
			if (!_detach)
			{
				_graphicalObject.SetWorldProperties(_gfxPreSimulateWorldValues);
			}
			AddTransformProperties(clientTick);
		}
		else if (!_detach)
		{
			_graphicalObject.SetWorldProperties(GetNetworkObjectWorldPropertiesWithOffset());
		}
	}

	public void Teleport()
	{
		if (!(_networkObject == null))
		{
			_teleportedTick = _networkObject.TimeManager.LocalTick;
			ClearTransformPropertiesQueue();
			TransformProperties worldProperties = _networkObject.transform.GetWorldProperties();
			worldProperties.Add(_gfxInitializedOffsetValues);
			_graphicalObject.SetWorldProperties(worldProperties);
		}
	}

	private void ClearTransformPropertiesQueue()
	{
		_transformProperties.Clear();
		_moveRates = new MoveRates(float.NegativeInfinity);
	}

	private void DiscardExcessiveTransformPropertiesQueue()
	{
		int num = _transformProperties.Count - (_interpolation + 3);
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
		TickTransformProperties data = new TickTransformProperties(tick, GetNetworkObjectWorldPropertiesWithOffset());
		_transformProperties.Enqueue(data);
		if (_transformProperties.Count == 1)
		{
			TransformProperties prevValues = _graphicalObject.GetWorldProperties();
			SetMoveRates(in prevValues);
			_startFrame = Time.frameCount + 1;
		}
	}

	private void ModifyTransformProperties(uint clientTick, uint firstTick)
	{
		int num = (int)(clientTick - firstTick);
		if (num < _transformProperties.Count && clientTick == _transformProperties[num].Tick)
		{
			_transformProperties[num] = new TickTransformProperties(clientTick, GetNetworkObjectWorldPropertiesWithOffset(), _graphicalObject.localScale);
		}
	}

	private TransformProperties GetNetworkObjectWorldPropertiesWithOffset()
	{
		return _networkObject.transform.GetWorldProperties(_gfxInitializedOffsetValues);
	}

	private bool CanSmooth()
	{
		if (_graphicalObject == null)
		{
			return false;
		}
		if (_networkObject != null && _networkObject.EnablePrediction && !_networkObject.EnableStateForwarding && !_networkObject.IsController)
		{
			return false;
		}
		if (_networkObject.IsServerOnlyStarted)
		{
			return false;
		}
		return true;
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
		float teleportThreshold = _teleportThreshold;
		_moveRates = MoveRates.GetMoveRates(prevValues, properties, tickDelta, teleportThreshold);
		_moveRates.TimeRemaining = tickDelta;
		SetMovementMultiplier();
	}

	private void SetMovementMultiplier()
	{
		int num = _transformProperties.Count - _interpolation;
		if (num != 0)
		{
			_movementMultiplier += 0.015f * (float)num;
		}
		else if (_interpolation == 1)
		{
			_movementMultiplier = 1f;
		}
		_movementMultiplier = Mathf.Clamp(_movementMultiplier, 0.95f, 1.05f);
	}

	private void MoveToTarget(float delta)
	{
		if (Time.frameCount < _startFrame)
		{
			return;
		}
		int count = _transformProperties.Count;
		if (count == 0 || count - _interpolation < -4)
		{
			return;
		}
		TickTransformProperties tickTransformProperties = _transformProperties.Peek();
		TransformPropertiesFlag movedProperties = (_useOwnerSmoothing ? _ownerSmoothedProperties : _spectatorSmoothedProperties);
		_moveRates.Move(_graphicalObject, tickTransformProperties.Properties, movedProperties, delta * _movementMultiplier, useWorldSpace: true);
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

	public void ResetState()
	{
		if (_initializeType == InitializeType.Unset)
		{
			return;
		}
		if (_graphicalObject != null)
		{
			if (_rootTransform != null)
			{
				if (_detach && !ApplicationState.IsQuitting())
				{
					_graphicalObject.SetParent(_rootTransform);
				}
				_graphicalObject.SetWorldProperties(GetNetworkObjectWorldPropertiesWithOffset());
				_graphicalObject = null;
			}
			else if (_detach)
			{
				UnityEngine.Object.Destroy(_graphicalObject.gameObject);
			}
		}
		_networkObject = null;
		_teleportedTick = 0u;
		_lastReconcileTick = 0u;
		_movementMultiplier = 1f;
		CollectionCaches<TickTransformProperties>.StoreAndDefault(ref _transformProperties);
		_teleportThreshold = 0f;
		_moveRates = default(MoveRates);
		_preTicked = false;
		_gfxInitializedOffsetValues = default(TransformProperties);
		_gfxPreSimulateWorldValues = default(TransformProperties);
		_tickDelta = 0f;
		_interpolation = 0;
	}

	public void InitializeState()
	{
	}
}
