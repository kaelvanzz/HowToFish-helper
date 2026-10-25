using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Serializing.Generated;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.Scripting;

namespace FishNet.Component.Transforming;

[DisallowMultipleComponent]
[AddComponentMenu("FishNet/Component/NetworkTransform")]
public sealed class NetworkTransform : NetworkBehaviour
{
	[Serializable]
	public enum ComponentConfigurationType
	{
		Disabled,
		CharacterController,
		Rigidbody,
		Rigidbody2D
	}

	private struct ReceivedClientData
	{
		public bool HasData;

		public PooledWriter Writer;

		public Channel Channel;

		public uint LocalTick;

		public void Update(ArraySegment<byte> data, Channel channel, bool updateHasData, uint localTick)
		{
			if (Writer == null)
			{
				Writer = WriterPool.Retrieve();
			}
			Writer.Clear();
			Writer.WriteArraySegment(data);
			Channel = channel;
			LocalTick = localTick;
			if (updateHasData)
			{
				HasData = true;
			}
		}

		public void SendReliably()
		{
			HasData = true;
			Channel = Channel.Reliable;
		}

		public void ResetState()
		{
			HasData = false;
			WriterPool.StoreAndDefault(ref Writer);
		}
	}

	[Serializable]
	public struct SnappedAxes
	{
		public bool X;

		public bool Y;

		public bool Z;
	}

	[Flags]
	private enum ChangedDelta : uint
	{
		Unset = 0u,
		PositionX = 1u,
		PositionY = 2u,
		PositionZ = 4u,
		Rotation = 8u,
		Extended = 0x10u,
		ScaleX = 0x20u,
		ScaleY = 0x40u,
		ScaleZ = 0x80u,
		Nested = 0x100u,
		All = uint.MaxValue
	}

	[Flags]
	private enum ChangedFull
	{
		Unset = 0,
		Position = 1,
		Rotation = 2,
		Scale = 4,
		Childed = 8,
		Teleport = 0x10
	}

	[Flags]
	private enum UpdateFlagA : byte
	{
		Unset = 0,
		X2 = 1,
		X4 = 2,
		Y2 = 4,
		Y4 = 8,
		Z2 = 0x10,
		Z4 = 0x20,
		Rotation = 0x40,
		Extended = 0x80
	}

	[Flags]
	private enum UpdateFlagB : byte
	{
		Unset = 0,
		X2 = 1,
		X4 = 2,
		Y2 = 4,
		Y4 = 8,
		Z2 = 0x10,
		Z4 = 0x20,
		Child = 0x40,
		Teleport = 0x80
	}

	public class GoalData : IResettable
	{
		public uint ReceivedTick;

		public RateData Rates = new RateData();

		public TransformData Transforms = new TransformData();

		[Preserve]
		public GoalData()
		{
		}

		public void ResetState()
		{
			ReceivedTick = 0u;
			Transforms.ResetState();
			Rates.ResetState();
		}

		public void InitializeState()
		{
		}
	}

	public class RateData : IResettable
	{
		public float Position;

		public float Rotation;

		public float Scale;

		public float LastUnalteredPositionRate;

		public uint TickSpan;

		internal float TimeRemaining;

		[Preserve]
		public RateData()
		{
		}

		public void Update(RateData rd)
		{
			Update(rd.Position, rd.Rotation, rd.Scale, rd.LastUnalteredPositionRate, rd.TickSpan, rd.TimeRemaining);
		}

		public void Update(float position, float rotation, float scale, float unalteredPositionRate, uint tickSpan, float timeRemaining)
		{
			Position = position;
			Rotation = rotation;
			Scale = scale;
			LastUnalteredPositionRate = unalteredPositionRate;
			TickSpan = tickSpan;
			TimeRemaining = timeRemaining;
		}

		public void ResetState()
		{
			Position = 0f;
			Rotation = 0f;
			Scale = 0f;
			LastUnalteredPositionRate = 0f;
			TickSpan = 0u;
			TimeRemaining = 0f;
		}

		public void InitializeState()
		{
		}
	}

	public class TransformData : IResettable
	{
		public enum ExtrapolateState : byte
		{
			Disabled,
			Available,
			Active
		}

		public uint Tick;

		public bool SnappingChecked;

		public Vector3 Position;

		public Quaternion Rotation;

		public Vector3 Scale;

		public Vector3 ExtrapolatedPosition;

		public ExtrapolateState ExtrapolationState;

		public NetworkBehaviour ParentBehaviour;

		public bool IsDefault { get; private set; } = true;


		[Preserve]
		public TransformData()
		{
		}

		internal void Update(TransformData copy)
		{
			Update(copy.Tick, copy.Position, copy.Rotation, copy.Scale, copy.ExtrapolatedPosition, copy.ParentBehaviour);
		}

		internal void Update(uint tick, Vector3 position, Quaternion rotation, Vector3 scale, Vector3 extrapolatedPosition, NetworkBehaviour parentBehaviour)
		{
			IsDefault = false;
			Tick = tick;
			Position = position;
			Rotation = rotation;
			Scale = scale;
			ExtrapolatedPosition = extrapolatedPosition;
			ParentBehaviour = parentBehaviour;
		}

		public void ResetState()
		{
			IsDefault = true;
			Tick = 0u;
			SnappingChecked = false;
			Position = Vector3.zero;
			Rotation = Quaternion.identity;
			Scale = Vector3.zero;
			ExtrapolatedPosition = Vector3.zero;
			ExtrapolationState = ExtrapolateState.Disabled;
			ParentBehaviour = null;
		}

		public void InitializeState()
		{
		}
	}

	[APIExclude]
	public delegate void DataReceivedChanged(TransformData prev, TransformData next);

	[Tooltip("Attached movement component to automatically configure.")]
	[SerializeField]
	private ComponentConfigurationType _componentConfiguration;

	[Tooltip("True to synchronize when this transform changes parent.")]
	[SerializeField]
	private bool _synchronizeParent;

	[Tooltip("How much to compress each transform property.")]
	[SerializeField]
	private TransformPackingData _packing = new TransformPackingData
	{
		Position = AutoPackType.Packed,
		Rotation = AutoPackType.Packed,
		Scale = AutoPackType.Unpacked
	};

	[Tooltip("How many ticks to interpolate.")]
	[Range(1f, 250f)]
	[SerializeField]
	private ushort _interpolation = 2;

	[Tooltip("How many ticks to extrapolate.")]
	[Range(0f, 1024f)]
	[SerializeField]
	private ushort _extrapolation = 2;

	[Tooltip("True to enable teleport threshhold.")]
	[SerializeField]
	private bool _enableTeleport;

	[Tooltip("How far the transform must travel in a single update to cause a teleport rather than smoothing. Using 0f will teleport every update.")]
	[Range(0f, float.MaxValue)]
	[SerializeField]
	private float _teleportThreshold = 1f;

	[Tooltip("True if owner controls how the object is synchronized.")]
	[SerializeField]
	private bool _clientAuthoritative = true;

	[Tooltip("True to synchronize movements on server to owner when not using client authoritative movement.")]
	[SerializeField]
	private bool _sendToOwner = true;

	[Tooltip("How often in ticks to synchronize. This is default to 1 but can be set longer to send less often. This value may also be changed at runtime. Enabling Network level of detail for this NetworkTransform disables manual control of this feature as it will be handled internally.")]
	[Range(1f, 255f)]
	[SerializeField]
	private byte _interval = 1;

	[Tooltip("True to synchronize position. Even while checked only changed values are sent.")]
	[SerializeField]
	private bool _synchronizePosition = true;

	[Tooltip("Distance sensitivity on position checks.")]
	[Range(1E-05f, 1.25f)]
	[SerializeField]
	private float _positionSensitivity = 0.001f;

	[Tooltip("Axes to snap on position.")]
	[SerializeField]
	private SnappedAxes _positionSnapping;

	[Tooltip("True to synchronize rotation. Even while checked only changed values are sent.")]
	[SerializeField]
	private bool _synchronizeRotation = true;

	[Tooltip("Axes to snap on rotation.")]
	[SerializeField]
	private SnappedAxes _rotationSnapping;

	[Tooltip("True to synchronize scale. Even while checked only changed values are sent.")]
	[SerializeField]
	private bool _synchronizeScale = true;

	[Tooltip("Distance sensitivity on scale checks.")]
	[Range(1E-05f, 1.25f)]
	[SerializeField]
	private float _scaleSensitivity = 0.001f;

	[Tooltip("Axes to snap on scale.")]
	[SerializeField]
	private SnappedAxes _scaleSnapping;

	private TransformPackingData _unpacked = new TransformPackingData
	{
		Position = AutoPackType.Unpacked,
		Rotation = AutoPackType.Unpacked,
		Scale = AutoPackType.Unpacked
	};

	private bool _lastReceiveReliable = true;

	private Transform _parentTransform;

	private ChangedDelta _serverChangedSinceReliable;

	private ChangedDelta _clientChangedSinceReliable;

	private uint _lastObserversRpcTick;

	private uint _lastServerRpcTick;

	private ReceivedClientData _authoritativeClientData;

	private bool _subscribedToTicks;

	private bool _subscribedToUpdate;

	private RigidbodyInterpolation? _initializedRigidbodyInterpolation;

	private RigidbodyInterpolation2D? _initializedRigidbodyInterpolation2d;

	private TransformData _lastReceivedServerTransformData;

	private TransformData _lastReceivedClientTransformData;

	private readonly RateData _lastCalculatedRateData = new RateData();

	private readonly Queue<GoalData> _goalDataQueue = new Queue<GoalData>();

	private GoalData _currentGoalData;

	private bool _changedSinceStart;

	private short _intervalsRemaining;

	private TransformData _lastSentTransformData;

	private PooledWriter _toClientChangedWriter;

	private uint _forceSendTick;

	private bool _teleport;

	private Transform _cachedTransform;

	private TimeManager _timeManager;

	public const ushort MAX_INTERPOLATION = 250;

	private bool NetworkInitialize___EarlyFishNet_002EComponent_002ETransforming_002ENetworkTransformFishNet_002ERuntime_002Edll_Excuted;

	private bool NetworkInitialize___LateFishNet_002EComponent_002ETransforming_002ENetworkTransformFishNet_002ERuntime_002Edll_Excuted;

	public bool TakenOwnership { get; private set; }

	public NetworkBehaviour ParentBehaviour { get; private set; }

	private ChangedDelta _fullChanged => ChangedDelta.All;

	public event DataReceivedChanged OnDataReceived;

	public event Action<GoalData> OnNextGoal;

	public event Action OnInterpolationComplete;

	public bool GetSendToOwner()
	{
		return _sendToOwner;
	}

	public void SetSendToOwner(bool value)
	{
		_sendToOwner = value;
		if (base.IsServerInitialized)
		{
			ObserversSetSendToOwner(value);
		}
	}

	public void SetSynchronizePosition(bool value)
	{
		_synchronizePosition = value;
	}

	public void SetPositionSnapping(SnappedAxes axes)
	{
		_positionSnapping = axes;
	}

	public void SetSynchronizeRotation(bool value)
	{
		_synchronizeRotation = value;
	}

	public void SetRotationSnapping(SnappedAxes axes)
	{
		_rotationSnapping = axes;
	}

	public void SetSynchronizeScale(bool value)
	{
		_synchronizeScale = value;
	}

	public void SetScaleSnapping(SnappedAxes axes)
	{
		_scaleSnapping = axes;
	}

	public void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_FishNet_002EComponent_002ETransforming_002ENetworkTransform_FishNet_002ERuntime_002Edll();
		NetworkInitialize___Late();
	}

	private void OnDestroy()
	{
		base.ResetState(asServer: true);
		ResetState_OnDestroy();
	}

	public override void OnStartNetwork()
	{
		_cachedTransform = base.transform;
		_timeManager = base.TimeManager;
		ChangeTickSubscription(subscribe: true);
	}

	public override void OnStartServer()
	{
		_lastReceivedClientTransformData = ObjectCaches<TransformData>.Retrieve();
		InitializeFields(asServer: true);
		SetDefaultGoalData();
	}

	public override void OnSpawnServer(NetworkConnection connection)
	{
		base.OnSpawnServer(connection);
		if (base.NetworkObject.gameObject != base.gameObject && _changedSinceStart)
		{
			PooledWriter pooledWriter = WriterPool.Retrieve();
			SerializeChanged(_fullChanged, pooledWriter);
			TargetUpdateTransform(connection, pooledWriter.GetArraySegment(), Channel.Reliable);
			pooledWriter.Store();
		}
	}

	public override void OnStartClient()
	{
		_lastReceivedServerTransformData = ObjectCaches<TransformData>.Retrieve();
		ChangeUpdateSubscription(subscribe: true);
		ConfigureComponents();
		InitializeFields(asServer: false);
		SetDefaultGoalData();
	}

	public override void OnOwnershipServer(NetworkConnection prevOwner)
	{
		ConfigureComponents();
		_intervalsRemaining = 0;
		_lastServerRpcTick = 0u;
		TryClearGoalDatas_OwnershipChange(prevOwner, asServer: true);
	}

	public override void OnOwnershipClient(NetworkConnection prevOwner)
	{
		ConfigureComponents();
		_intervalsRemaining = 0;
		if (!base.IsOwner && _clientAuthoritative)
		{
			SetDefaultGoalData();
		}
		TryClearGoalDatas_OwnershipChange(prevOwner, asServer: false);
	}

	public override void OnStopNetwork()
	{
		ResetState();
		ChangeUpdateSubscription(subscribe: false);
	}

	private void TryClearGoalDatas_OwnershipChange(NetworkConnection prevOwner, bool asServer)
	{
		if (!_clientAuthoritative)
		{
			return;
		}
		if (!asServer)
		{
			if (base.IsOwner)
			{
				_goalDataQueue.Clear();
			}
		}
		else if (base.Owner.IsValid)
		{
			_goalDataQueue.Clear();
		}
	}

	private void TimeManager_OnUpdate()
	{
		MoveToTarget(Time.deltaTime);
	}

	private void InitializeFields(bool asServer)
	{
		if ((!asServer && !base.IsServerStarted) || asServer)
		{
			if (_lastSentTransformData != null)
			{
				_lastSentTransformData.ResetState();
			}
			else
			{
				_lastSentTransformData = ResettableObjectCaches<TransformData>.Retrieve();
			}
		}
		if (asServer)
		{
			if (_toClientChangedWriter != null)
			{
				_toClientChangedWriter.Clear();
			}
			else
			{
				_toClientChangedWriter = WriterPool.Retrieve();
			}
		}
	}

	private void ConfigureComponents()
	{
		if (_componentConfiguration == ComponentConfigurationType.Disabled)
		{
			return;
		}
		CharacterController component3;
		if (_componentConfiguration == ComponentConfigurationType.Rigidbody)
		{
			if (TryGetComponent<Rigidbody>(out var component))
			{
				if (!_initializedRigidbodyInterpolation.HasValue)
				{
					_initializedRigidbodyInterpolation = component.interpolation;
				}
				if (component.isKinematic = CanMakeKinematic())
				{
					component.interpolation = RigidbodyInterpolation.None;
				}
				else
				{
					component.interpolation = _initializedRigidbodyInterpolation.Value;
				}
			}
		}
		else if (_componentConfiguration == ComponentConfigurationType.Rigidbody2D)
		{
			if (TryGetComponent<Rigidbody2D>(out var component2))
			{
				if (!_initializedRigidbodyInterpolation2d.HasValue)
				{
					_initializedRigidbodyInterpolation2d = component2.interpolation;
				}
				bool flag3 = (component2.isKinematic = CanMakeKinematic());
				component2.simulated = !flag3;
				if (flag3)
				{
					component2.interpolation = RigidbodyInterpolation2D.None;
				}
				else
				{
					component2.interpolation = _initializedRigidbodyInterpolation2d.Value;
				}
			}
		}
		else if (_componentConfiguration == ComponentConfigurationType.CharacterController && TryGetComponent<CharacterController>(out component3))
		{
			if (_clientAuthoritative)
			{
				component3.enabled = base.IsController;
			}
			else if (_sendToOwner)
			{
				component3.enabled = base.IsServerInitialized;
			}
			else
			{
				component3.enabled = base.IsServerInitialized || base.IsOwner;
			}
		}
		bool CanMakeKinematic()
		{
			bool isServerStarted = base.IsServerStarted;
			if (!_clientAuthoritative)
			{
				return !isServerStarted;
			}
			if (base.IsOwner)
			{
				return false;
			}
			if (isServerStarted && !base.Owner.IsActive)
			{
				return false;
			}
			return true;
		}
	}

	private void TimeManager_OnPostTick()
	{
		if (_forceSendTick != 0 && _timeManager.LocalTick > _forceSendTick)
		{
			_forceSendTick = 0u;
			ForceSend();
		}
		UpdateParentBehaviour();
		if (_interval > 1)
		{
			if (_intervalsRemaining == -1)
			{
				if (!_cachedTransform.hasChanged)
				{
					return;
				}
				_intervalsRemaining = _interval;
			}
			_intervalsRemaining--;
			if (_intervalsRemaining > 0)
			{
				return;
			}
			_intervalsRemaining = -1;
		}
		bool isServerInitialized = base.IsServerInitialized;
		bool isClientInitialized = base.IsClientInitialized;
		if (isServerInitialized)
		{
			if (!isClientInitialized)
			{
				MoveToTarget((float)_timeManager.TickDelta);
			}
			SendToClients();
		}
		if (isClientInitialized)
		{
			SendToServer(_lastSentTransformData);
		}
	}

	private void ChangeTickSubscription(bool subscribe)
	{
		if (subscribe != _subscribedToTicks && !(base.NetworkManager == null))
		{
			_subscribedToTicks = subscribe;
			if (subscribe)
			{
				base.NetworkManager.TimeManager.OnPostTick += TimeManager_OnPostTick;
			}
			else
			{
				base.NetworkManager.TimeManager.OnPostTick -= TimeManager_OnPostTick;
			}
		}
	}

	private void ChangeUpdateSubscription(bool subscribe)
	{
		if (subscribe != _subscribedToUpdate && !(_timeManager == null))
		{
			_subscribedToUpdate = subscribe;
			if (subscribe)
			{
				_timeManager.OnUpdate += TimeManager_OnUpdate;
			}
			else
			{
				_timeManager.OnUpdate -= TimeManager_OnUpdate;
			}
		}
	}

	public void SetInterpolation(ushort value)
	{
		if (value < 1)
		{
			value = 1;
		}
		_interpolation = value;
	}

	public void SetExtrapolation(ushort value)
	{
		_extrapolation = value;
	}

	private bool CanControl()
	{
		if (_clientAuthoritative)
		{
			return base.IsController;
		}
		if (base.IsServerInitialized)
		{
			return true;
		}
		return false;
	}

	public void Teleport()
	{
		if (CanControl())
		{
			_teleport = true;
		}
	}

	[ObserversRpc(BufferLast = true, ExcludeServer = true)]
	private void ObserversSetSendToOwner(bool value)
	{
		RpcWriter___ObserversSetSendToOwner___1140765316(value);
	}

	public void ForceSend(uint ticks)
	{
		if (_forceSendTick != 0)
		{
			ForceSend();
		}
		_forceSendTick = _timeManager.LocalTick + ticks;
	}

	public void ForceSend()
	{
		_lastSentTransformData.ResetState();
		if (_authoritativeClientData.Writer != null)
		{
			_authoritativeClientData.SendReliably();
		}
	}

	public void SetInterval(byte value)
	{
		if ((base.IsServerInitialized && !_clientAuthoritative) || (base.IsServerInitialized && _clientAuthoritative && !base.Owner.IsValid) || (_clientAuthoritative && base.IsOwner))
		{
			if (base.IsServerInitialized)
			{
				ObserversSetInterval(value);
			}
			else
			{
				ServerSetInterval(value);
			}
		}
	}

	private void SetIntervalInternal(byte value)
	{
		value = (byte)Mathf.Max(value, 1);
		_interval = value;
	}

	[ServerRpc(RunLocally = true)]
	private void ServerSetInterval(byte value)
	{
		RpcWriter___ServerSetInterval___1246646286(value);
		RpcLogic___ServerSetInterval___1246646286(value);
	}

	[ObserversRpc(BufferLast = true, RunLocally = true)]
	private void ObserversSetInterval(byte value)
	{
		RpcWriter___ObserversSetInterval___1246646286(value);
		RpcLogic___ObserversSetInterval___1246646286(value);
	}

	private void SetDefaultGoalData()
	{
		Transform t = _cachedTransform;
		NetworkBehaviour parentBehaviour = null;
		if (_synchronizeParent && base.NetworkObject.CurrentParentNetworkBehaviour != null)
		{
			t.parent.TryGetComponent<NetworkBehaviour>(out parentBehaviour);
			if (parentBehaviour == null)
			{
				LogInvalidParent();
			}
			else
			{
				_parentTransform = t.parent;
				ParentBehaviour = parentBehaviour;
			}
		}
		_teleport = false;
		SetLastReceived(_lastReceivedServerTransformData);
		SetLastReceived(_lastReceivedClientTransformData);
		void SetLastReceived(TransformData td)
		{
			td?.Update(0u, t.localPosition, t.localRotation, t.localScale, t.localPosition, parentBehaviour);
		}
	}

	private void LogInvalidParent()
	{
		base.NetworkManager.LogWarning($"{base.gameObject.name} [Id {base.ObjectId}] is childed but the parent {_cachedTransform.parent.name} does not contain a NetworkBehaviour component. To synchronize parents the parent object must have a NetworkBehaviour component, even if empty.");
	}

	private void SerializeChanged(ChangedDelta changed, PooledWriter writer)
	{
		UpdateFlagA updateFlagA = UpdateFlagA.Unset;
		UpdateFlagB updateFlagB = UpdateFlagB.Unset;
		TransformPackingData transformPackingData = (ChangedContains(changed, ChangedDelta.Nested) ? _unpacked : _packing);
		int position = writer.Position;
		writer.Skip(1);
		float num = 100f;
		float num2 = 32766f;
		Transform cachedTransform = _cachedTransform;
		if (_synchronizePosition)
		{
			AutoPackType position2 = transformPackingData.Position;
			if (ChangedContains(changed, ChangedDelta.PositionX))
			{
				float x = cachedTransform.localPosition.x;
				float num3 = x * num;
				if (position2 != 0 && Math.Abs(num3) <= num2)
				{
					updateFlagA |= UpdateFlagA.X2;
					writer.WriteInt16((short)num3);
				}
				else
				{
					updateFlagA |= UpdateFlagA.X4;
					writer.WriteSingle(x);
				}
			}
			if (ChangedContains(changed, ChangedDelta.PositionY))
			{
				float x = cachedTransform.localPosition.y;
				float num3 = x * num;
				if (position2 != 0 && Math.Abs(num3) <= num2)
				{
					updateFlagA |= UpdateFlagA.Y2;
					writer.WriteInt16((short)num3);
				}
				else
				{
					updateFlagA |= UpdateFlagA.Y4;
					writer.WriteSingle(x);
				}
			}
			if (ChangedContains(changed, ChangedDelta.PositionZ))
			{
				float x = cachedTransform.localPosition.z;
				float num3 = x * num;
				if (position2 != 0 && Math.Abs(num3) <= num2)
				{
					updateFlagA |= UpdateFlagA.Z2;
					writer.WriteInt16((short)num3);
				}
				else
				{
					updateFlagA |= UpdateFlagA.Z4;
					writer.WriteSingle(x);
				}
			}
		}
		if (_synchronizeRotation && ChangedContains(changed, ChangedDelta.Rotation))
		{
			updateFlagA |= UpdateFlagA.Rotation;
			writer.WriteQuaternion(cachedTransform.localRotation, _packing.Rotation);
		}
		bool teleport = _teleport;
		if (teleport)
		{
			changed |= ChangedDelta.Extended;
		}
		if (ChangedContains(changed, ChangedDelta.Extended))
		{
			AutoPackType scale = transformPackingData.Scale;
			updateFlagA |= UpdateFlagA.Extended;
			int position3 = writer.Position;
			writer.Skip(1);
			if (teleport)
			{
				updateFlagB |= UpdateFlagB.Teleport;
				_teleport = false;
			}
			if (_synchronizeScale)
			{
				if (ChangedContains(changed, ChangedDelta.ScaleX))
				{
					float x = cachedTransform.localScale.x;
					float num3 = x * num;
					if (scale != 0 && Math.Abs(num3) <= num2)
					{
						updateFlagB |= UpdateFlagB.X2;
						writer.WriteInt16((short)num3);
					}
					else
					{
						updateFlagB |= UpdateFlagB.X4;
						writer.WriteSingle(x);
					}
				}
				if (ChangedContains(changed, ChangedDelta.ScaleY))
				{
					float x = cachedTransform.localScale.y;
					float num3 = x * num;
					if (scale != 0 && Math.Abs(num3) <= num2)
					{
						updateFlagB |= UpdateFlagB.Y2;
						writer.WriteInt16((short)num3);
					}
					else
					{
						updateFlagB |= UpdateFlagB.Y4;
						writer.WriteSingle(x);
					}
				}
				if (ChangedContains(changed, ChangedDelta.ScaleZ))
				{
					float x = cachedTransform.localScale.z;
					float num3 = x * num;
					if (scale != 0 && Math.Abs(num3) <= num2)
					{
						updateFlagB |= UpdateFlagB.Z2;
						writer.WriteInt16((short)num3);
					}
					else
					{
						updateFlagB |= UpdateFlagB.Z4;
						writer.WriteSingle(x);
					}
				}
			}
			if (ChangedContains(changed, ChangedDelta.Nested) && ParentBehaviour != null)
			{
				updateFlagB |= UpdateFlagB.Child;
				writer.WriteNetworkBehaviour(ParentBehaviour);
			}
			writer.InsertUInt8Unpacked((byte)updateFlagB, position3);
		}
		writer.InsertUInt8Unpacked((byte)updateFlagA, position);
		static bool ChangedContains(ChangedDelta whole, ChangedDelta part)
		{
			return (whole & part) == part;
		}
	}

	private void DeserializePacket(ArraySegment<byte> data, TransformData prevTransformData, TransformData nextTransformData, ref ChangedFull changedFull)
	{
		PooledReader pooledReader = ReaderPool.Retrieve(data, base.NetworkManager);
		UpdateFlagA whole2 = (UpdateFlagA)pooledReader.ReadUInt8Unpacked();
		int remaining = pooledReader.Remaining;
		if (UpdateFlagAContains(whole2, UpdateFlagA.X2))
		{
			nextTransformData.Position.x = (float)pooledReader.ReadInt16() / 100f;
		}
		else if (UpdateFlagAContains(whole2, UpdateFlagA.X4))
		{
			nextTransformData.Position.x = pooledReader.ReadSingle();
		}
		else
		{
			nextTransformData.Position.x = prevTransformData.Position.x;
		}
		if (UpdateFlagAContains(whole2, UpdateFlagA.Y2))
		{
			nextTransformData.Position.y = (float)pooledReader.ReadInt16() / 100f;
		}
		else if (UpdateFlagAContains(whole2, UpdateFlagA.Y4))
		{
			nextTransformData.Position.y = pooledReader.ReadSingle();
		}
		else
		{
			nextTransformData.Position.y = prevTransformData.Position.y;
		}
		if (UpdateFlagAContains(whole2, UpdateFlagA.Z2))
		{
			nextTransformData.Position.z = (float)pooledReader.ReadInt16() / 100f;
		}
		else if (UpdateFlagAContains(whole2, UpdateFlagA.Z4))
		{
			nextTransformData.Position.z = pooledReader.ReadSingle();
		}
		else
		{
			nextTransformData.Position.z = prevTransformData.Position.z;
		}
		if (remaining != pooledReader.Remaining)
		{
			changedFull |= ChangedFull.Position;
		}
		if (UpdateFlagAContains(whole2, UpdateFlagA.Rotation))
		{
			nextTransformData.Rotation = pooledReader.ReadQuaternion(_packing.Rotation);
			changedFull |= ChangedFull.Rotation;
		}
		else
		{
			nextTransformData.Rotation = prevTransformData.Rotation;
		}
		if (UpdateFlagAContains(whole2, UpdateFlagA.Extended))
		{
			UpdateFlagB whole3 = (UpdateFlagB)pooledReader.ReadUInt8Unpacked();
			remaining = pooledReader.Remaining;
			if (UpdateFlagBContains(whole3, UpdateFlagB.X2))
			{
				nextTransformData.Scale.x = (float)pooledReader.ReadInt16() / 100f;
			}
			else if (UpdateFlagBContains(whole3, UpdateFlagB.X4))
			{
				nextTransformData.Scale.x = pooledReader.ReadSingle();
			}
			else
			{
				nextTransformData.Scale.x = prevTransformData.Scale.x;
			}
			if (UpdateFlagBContains(whole3, UpdateFlagB.Y2))
			{
				nextTransformData.Scale.y = (float)pooledReader.ReadInt16() / 100f;
			}
			else if (UpdateFlagBContains(whole3, UpdateFlagB.Y4))
			{
				nextTransformData.Scale.y = pooledReader.ReadSingle();
			}
			else
			{
				nextTransformData.Scale.y = prevTransformData.Scale.y;
			}
			if (UpdateFlagBContains(whole3, UpdateFlagB.Z2))
			{
				nextTransformData.Scale.z = (float)pooledReader.ReadInt16() / 100f;
			}
			else if (UpdateFlagBContains(whole3, UpdateFlagB.Z4))
			{
				nextTransformData.Scale.z = pooledReader.ReadSingle();
			}
			else
			{
				nextTransformData.Scale.z = prevTransformData.Scale.z;
			}
			if (pooledReader.Remaining != remaining)
			{
				changedFull |= ChangedFull.Scale;
			}
			else
			{
				nextTransformData.Scale = prevTransformData.Scale;
			}
			if (UpdateFlagBContains(whole3, UpdateFlagB.Teleport))
			{
				changedFull |= ChangedFull.Teleport;
			}
			if (UpdateFlagBContains(whole3, UpdateFlagB.Child))
			{
				nextTransformData.ParentBehaviour = pooledReader.ReadNetworkBehaviour();
				changedFull |= ChangedFull.Childed;
			}
			else
			{
				Unnest();
			}
		}
		else
		{
			nextTransformData.Scale = prevTransformData.Scale;
			Unnest();
		}
		pooledReader.Store();
		void Unnest()
		{
			nextTransformData.ParentBehaviour = null;
		}
		static bool UpdateFlagAContains(UpdateFlagA whole, UpdateFlagA part)
		{
			return (whole & part) == part;
		}
		static bool UpdateFlagBContains(UpdateFlagB whole, UpdateFlagB part)
		{
			return (whole & part) == part;
		}
	}

	private void UpdateParentBehaviour()
	{
		if (!_synchronizeParent || !CanControl())
		{
			return;
		}
		Transform parent = _cachedTransform.parent;
		if (parent == null)
		{
			if (base.NetworkObject.RuntimeParentNetworkBehaviour != null)
			{
				base.NetworkManager.LogWarning(base.gameObject.name + " parent object was removed without calling UnsetParent. Use networkObject.UnsetParent() to remove a NetworkObject from it's parent. This is being made a requirement in Fish-Networking v4.");
			}
			ParentBehaviour = null;
			_parentTransform = null;
		}
		else
		{
			if (_parentTransform == parent)
			{
				return;
			}
			_parentTransform = parent;
			if (!parent.TryGetComponent<NetworkBehaviour>(out var component))
			{
				ParentBehaviour = null;
				LogInvalidParent();
				return;
			}
			ParentBehaviour = component;
			if (base.NetworkObject.CurrentParentNetworkBehaviour != ParentBehaviour)
			{
				base.NetworkManager.LogWarning(base.gameObject.name + " parent was set without calling SetParent. Use networkObject.SetParent(obj) to assign a NetworkObject a new parent. This is being made a requirement in Fish-Networking v4.");
			}
		}
	}

	private void SetParent(NetworkBehaviour parent, RateData rd)
	{
		Transform transform = ((parent == null) ? null : parent.transform);
		Transform cachedTransform = _cachedTransform;
		if (!(transform == cachedTransform.parent))
		{
			Vector3 localScale = cachedTransform.localScale;
			if (transform != null)
			{
				base.NetworkObject.SetParent(parent);
			}
			else
			{
				base.NetworkObject.UnsetParent();
			}
			cachedTransform.localScale = localScale;
			rd?.Update(-1f, -1f, -1f, rd.LastUnalteredPositionRate, rd.TickSpan, rd.TimeRemaining);
		}
	}

	private void MoveToTarget(float delta)
	{
		if (_currentGoalData == null || (!base.IsServerInitialized && !base.IsClientInitialized))
		{
			return;
		}
		if (_clientAuthoritative)
		{
			if (base.IsOwner || TakenOwnership)
			{
				return;
			}
		}
		else if (base.IsOwner && !_sendToOwner)
		{
			return;
		}
		if ((!_clientAuthoritative || !base.Owner.IsActive) && base.IsServerInitialized)
		{
			return;
		}
		TransformData transforms = _currentGoalData.Transforms;
		RateData rates = _currentGoalData.Rates;
		if (_synchronizeParent)
		{
			SetParent(transforms.ParentBehaviour, rates);
		}
		float num = 1f;
		int count = _goalDataQueue.Count;
		if (count > _interpolation + 1)
		{
			num += 0.05f;
		}
		Transform cachedTransform = _cachedTransform;
		SnapProperties(transforms);
		if (_synchronizePosition)
		{
			float position = rates.Position;
			Vector3 target = ((transforms.ExtrapolationState == TransformData.ExtrapolateState.Active && !_lastReceiveReliable) ? transforms.ExtrapolatedPosition : transforms.Position);
			if (position == -1f)
			{
				cachedTransform.localPosition = transforms.Position;
			}
			else
			{
				cachedTransform.localPosition = Vector3.MoveTowards(cachedTransform.localPosition, target, position * delta * num);
			}
		}
		if (_synchronizeRotation)
		{
			float position = rates.Rotation;
			if (position == -1f)
			{
				cachedTransform.localRotation = transforms.Rotation;
			}
			else
			{
				cachedTransform.localRotation = Quaternion.RotateTowards(cachedTransform.localRotation, transforms.Rotation, position * delta);
			}
		}
		if (_synchronizeScale)
		{
			float position = rates.Scale;
			if (position == -1f)
			{
				cachedTransform.localScale = transforms.Scale;
			}
			else
			{
				cachedTransform.localScale = Vector3.MoveTowards(cachedTransform.localScale, transforms.Scale, position * delta);
			}
		}
		float num2 = rates.TimeRemaining - delta * num;
		if (num2 < 0f - delta)
		{
			num2 = 0f - delta;
		}
		rates.TimeRemaining = num2;
		if (!(rates.TimeRemaining <= 0f))
		{
			return;
		}
		float num3 = Mathf.Abs(rates.TimeRemaining);
		if (count > 0)
		{
			SetCurrentGoalData(_goalDataQueue.Dequeue());
			if (num3 > 0f)
			{
				MoveToTarget(num3);
			}
		}
		else
		{
			if (!HasChanged(transforms))
			{
				_currentGoalData = null;
			}
			this.OnInterpolationComplete?.Invoke();
		}
	}

	private void SendToClients()
	{
		bool num = _clientAuthoritative && base.Owner.IsValid;
		Channel channel = Channel.Unreliable;
		if (num && !base.Owner.IsLocalClient)
		{
			if (!_authoritativeClientData.HasData && _authoritativeClientData.Channel != 0 && _authoritativeClientData.Writer != null)
			{
				uint num2 = (uint)(1 + _interpolation + _extrapolation);
				if (_timeManager.LocalTick - _authoritativeClientData.LocalTick <= num2)
				{
					return;
				}
				_authoritativeClientData.SendReliably();
			}
			if (_authoritativeClientData.HasData)
			{
				_changedSinceStart = true;
				ObserversUpdateClientAuthoritativeTransform(_authoritativeClientData.Writer.GetArraySegment(), _authoritativeClientData.Channel);
				_authoritativeClientData.HasData = false;
			}
			return;
		}
		PooledWriter toClientChangedWriter = _toClientChangedWriter;
		TransformData lastSentTransformData = _lastSentTransformData;
		ChangedDelta changed = GetChanged(lastSentTransformData);
		if (changed == ChangedDelta.Unset)
		{
			if (_serverChangedSinceReliable == ChangedDelta.Unset)
			{
				return;
			}
			_serverChangedSinceReliable = ChangedDelta.Unset;
			toClientChangedWriter = _toClientChangedWriter;
			channel = Channel.Reliable;
		}
		else
		{
			toClientChangedWriter.Clear();
			_serverChangedSinceReliable |= changed;
			_changedSinceStart = true;
			Transform cachedTransform = _cachedTransform;
			lastSentTransformData.Update(0u, cachedTransform.localPosition, cachedTransform.localRotation, cachedTransform.localScale, cachedTransform.localPosition, ParentBehaviour);
			SerializeChanged(changed, toClientChangedWriter);
		}
		ObserversUpdateClientAuthoritativeTransform(toClientChangedWriter.GetArraySegment(), channel);
	}

	private void SendToServer(TransformData lastSentTransformData)
	{
		if (base.IsServerInitialized || !_clientAuthoritative || !base.IsOwner)
		{
			return;
		}
		Channel channel = Channel.Unreliable;
		ChangedDelta changedDelta = GetChanged(lastSentTransformData);
		if (changedDelta == ChangedDelta.Unset)
		{
			if (_clientChangedSinceReliable == ChangedDelta.Unset)
			{
				return;
			}
			changedDelta = _clientChangedSinceReliable;
			_clientChangedSinceReliable = ChangedDelta.Unset;
			channel = Channel.Reliable;
		}
		else
		{
			_clientChangedSinceReliable |= changedDelta;
		}
		Transform cachedTransform = _cachedTransform;
		lastSentTransformData.Update(0u, cachedTransform.localPosition, cachedTransform.localRotation, cachedTransform.localScale, cachedTransform.localPosition, ParentBehaviour);
		PooledWriter pooledWriter = WriterPool.Retrieve();
		SerializeChanged(changedDelta, pooledWriter);
		ServerUpdateTransform(pooledWriter.GetArraySegment(), channel);
		pooledWriter.Store();
	}

	private bool HasChanged(TransformData td)
	{
		Transform cachedTransform = _cachedTransform;
		if (!(td.Position != cachedTransform.localPosition) && !(td.Rotation != cachedTransform.localRotation))
		{
			return td.Scale != cachedTransform.localScale;
		}
		return true;
	}

	private bool HasChanged(TransformData a, TransformData b)
	{
		if (!(a.Position != b.Position) && !(a.Rotation != b.Rotation) && !(a.Scale != b.Scale))
		{
			return a.ParentBehaviour != b.ParentBehaviour;
		}
		return true;
	}

	private ChangedDelta GetChanged(TransformData transformData)
	{
		if (transformData == null || transformData.IsDefault)
		{
			return _fullChanged;
		}
		return GetChanged(transformData.Position, transformData.Rotation, transformData.Scale, transformData.ParentBehaviour);
	}

	private ChangedDelta GetChanged(Vector3 lastPosition, Quaternion lastRotation, Vector3 lastScale, NetworkBehaviour lastParentBehaviour)
	{
		ChangedDelta changedDelta = ChangedDelta.Unset;
		Transform cachedTransform = _cachedTransform;
		Vector3 localPosition = cachedTransform.localPosition;
		if (Mathf.Abs(localPosition.x - lastPosition.x) >= _positionSensitivity)
		{
			changedDelta |= ChangedDelta.PositionX;
		}
		if (Mathf.Abs(localPosition.y - lastPosition.y) >= _positionSensitivity)
		{
			changedDelta |= ChangedDelta.PositionY;
		}
		if (Mathf.Abs(localPosition.z - lastPosition.z) >= _positionSensitivity)
		{
			changedDelta |= ChangedDelta.PositionZ;
		}
		if (!cachedTransform.localRotation.Matches(lastRotation, precise: true))
		{
			changedDelta |= ChangedDelta.Rotation;
		}
		ChangedDelta changedDelta2 = changedDelta;
		Vector3 localScale = cachedTransform.localScale;
		if (Mathf.Abs(localScale.x - lastScale.x) >= _scaleSensitivity)
		{
			changedDelta |= ChangedDelta.ScaleX;
		}
		if (Mathf.Abs(localScale.y - lastScale.y) >= _scaleSensitivity)
		{
			changedDelta |= ChangedDelta.ScaleY;
		}
		if (Mathf.Abs(localScale.z - lastScale.z) >= _scaleSensitivity)
		{
			changedDelta |= ChangedDelta.ScaleZ;
		}
		if (changedDelta != 0 && ParentBehaviour != null)
		{
			changedDelta |= ChangedDelta.Nested;
		}
		if (changedDelta2 != changedDelta)
		{
			changedDelta |= ChangedDelta.Extended;
		}
		return changedDelta;
	}

	private void SnapProperties(TransformData transformData, bool force = false)
	{
		if (!transformData.SnappingChecked)
		{
			transformData.SnappingChecked = true;
			Transform cachedTransform = _cachedTransform;
			if (_synchronizePosition)
			{
				_ = cachedTransform.localPosition;
				Vector3 localPosition = default(Vector3);
				localPosition.x = ((force || _positionSnapping.X) ? transformData.Position.x : cachedTransform.localPosition.x);
				localPosition.y = ((force || _positionSnapping.Y) ? transformData.Position.y : cachedTransform.localPosition.y);
				localPosition.z = ((force || _positionSnapping.Z) ? transformData.Position.z : cachedTransform.localPosition.z);
				cachedTransform.localPosition = localPosition;
			}
			if (_synchronizeRotation)
			{
				Vector3 eulerAngles = transformData.Rotation.eulerAngles;
				Vector3 localEulerAngles = default(Vector3);
				localEulerAngles.x = ((force || _rotationSnapping.X) ? eulerAngles.x : cachedTransform.localEulerAngles.x);
				localEulerAngles.y = ((force || _rotationSnapping.Y) ? eulerAngles.y : cachedTransform.localEulerAngles.y);
				localEulerAngles.z = ((force || _rotationSnapping.Z) ? eulerAngles.z : cachedTransform.localEulerAngles.z);
				cachedTransform.localEulerAngles = localEulerAngles;
			}
			if (_synchronizeScale)
			{
				Vector3 localScale = default(Vector3);
				localScale.x = ((force || _scaleSnapping.X) ? transformData.Scale.x : cachedTransform.localScale.x);
				localScale.y = ((force || _scaleSnapping.Y) ? transformData.Scale.y : cachedTransform.localScale.y);
				localScale.z = ((force || _scaleSnapping.Z) ? transformData.Scale.z : cachedTransform.localScale.z);
				cachedTransform.localScale = localScale;
			}
		}
	}

	private void SetInstantRates(RateData rd, uint tickDifference, float timeRemaining)
	{
		rd.Update(-1f, -1f, -1f, -1f, tickDifference, timeRemaining);
	}

	private void SetCalculatedRates(TransformData prevTd, RateData prevRd, GoalData nextGd, ChangedFull changedFull, bool hasChanged, Channel channel)
	{
		TransformData transforms = nextGd.Transforms;
		if (channel == Channel.Reliable && !hasChanged)
		{
			nextGd.Rates.Update(prevRd);
			return;
		}
		float timePassed;
		uint tickDifference = GetTickDifference(prevTd, nextGd, 1u, out timePassed);
		float num = 0f;
		float num2 = 0f;
		float num3 = 0f;
		RateData rates = nextGd.Rates;
		if (ChangedFullContains(changedFull, ChangedFull.Teleport))
		{
			SetInstantRates(rates, tickDifference, timePassed);
			return;
		}
		float num4 = 1f;
		float num5 = rates.LastUnalteredPositionRate;
		if (ChangedFullContains(changedFull, ChangedFull.Position))
		{
			float num6 = Vector3.Distance(prevTd.Position, transforms.Position);
			if (_enableTeleport && num6 >= _teleportThreshold)
			{
				SetInstantRates(rates, tickDifference, timePassed);
				return;
			}
			if (LowDistance(num6, rotation: false))
			{
				num5 = -1f;
				num = -1f;
			}
			else
			{
				num5 = num6 / timePassed;
				if (num5 > 0f && rates.LastUnalteredPositionRate > 0f && Mathf.Abs(1f - num5 / rates.LastUnalteredPositionRate) > 0.25f)
				{
					float num7 = rates.LastUnalteredPositionRate / num5;
					float num8 = 0.1f;
					if ((num7 < 1f && Mathf.Abs(0.5f - num7) < num8) || (num7 > 1f && Mathf.Abs(2f - num7) < num8))
					{
						num4 = num7;
					}
				}
				num = num5 * num4;
				if (num <= 0f)
				{
					num = -1f;
				}
			}
		}
		if (ChangedFullContains(changedFull, ChangedFull.Rotation))
		{
			float num6 = prevTd.Rotation.Angle(transforms.Rotation, precise: true);
			if (LowDistance(num6, rotation: true))
			{
				num2 = -1f;
			}
			else
			{
				num2 = num6 / timePassed * num4;
				if (num2 <= 0f)
				{
					num2 = -1f;
				}
			}
		}
		if (ChangedFullContains(changedFull, ChangedFull.Scale))
		{
			float num6 = Vector3.Distance(prevTd.Scale, transforms.Scale);
			if (LowDistance(num6, rotation: false))
			{
				num3 = -1f;
			}
			else
			{
				num3 = num6 / timePassed * num4;
				if (num3 <= 0f)
				{
					num3 = -1f;
				}
			}
		}
		rates.Update(num, num2, num3, num5, tickDifference, timePassed);
		static bool ChangedFullContains(ChangedFull whole, ChangedFull part)
		{
			return (whole & part) == part;
		}
		static bool LowDistance(float dist, bool rotation)
		{
			if (rotation)
			{
				return dist < 1f;
			}
			return dist < 0.0001f;
		}
	}

	private uint GetTickDifference(TransformData prevTd, GoalData nextGd, uint minimum, out float timePassed)
	{
		TransformData transforms = nextGd.Transforms;
		uint num = prevTd.Tick;
		if (num == 0)
		{
			num = transforms.Tick - _interval;
		}
		long num2 = transforms.Tick - num;
		if (num2 < minimum)
		{
			num2 = minimum;
		}
		timePassed = (float)base.NetworkManager.TimeManager.TicksToTime((uint)num2);
		return (uint)num2;
	}

	private void SetExtrapolatedData(TransformData prev, TransformData next, Channel channel)
	{
		next.ExtrapolationState = TransformData.ExtrapolateState.Disabled;
	}

	[TargetRpc(ValidateTarget = false)]
	private void TargetUpdateTransform(NetworkConnection conn, ArraySegment<byte> data, Channel channel)
	{
		RpcWriter___TargetUpdateTransform___748863190(conn, data, channel);
	}

	[ObserversRpc]
	private void ObserversUpdateClientAuthoritativeTransform(ArraySegment<byte> data, Channel channel)
	{
		RpcWriter___ObserversUpdateClientAuthoritativeTransform___2713644489(data, channel);
	}

	[ServerRpc]
	private void ServerUpdateTransform(ArraySegment<byte> data, Channel channel)
	{
		RpcWriter___ServerUpdateTransform___2713644489(data, channel);
	}

	private void DataReceived(ArraySegment<byte> data, Channel channel, bool asServer)
	{
		if (base.IsDeinitializing)
		{
			return;
		}
		TransformData transformData = (asServer ? _lastReceivedClientTransformData : _lastReceivedServerTransformData);
		RateData lastCalculatedRateData = _lastCalculatedRateData;
		ChangedFull changedFull = ChangedFull.Unset;
		GoalData goalData = ResettableObjectCaches<GoalData>.Retrieve();
		TransformData transforms = goalData.Transforms;
		UpdateTransformData(data, transformData, transforms, ref changedFull);
		this.OnDataReceived?.Invoke(transformData, transforms);
		SetExtrapolatedData(transformData, transforms, channel);
		bool flag = HasChanged(transformData, transforms);
		if (asServer && !base.IsClientStarted)
		{
			float timePassed;
			uint tickDifference = GetTickDifference(transformData, goalData, 1u, out timePassed);
			SetInstantRates(goalData.Rates, tickDifference, timePassed);
		}
		else
		{
			SetCalculatedRates(transformData, lastCalculatedRateData, goalData, changedFull, flag, channel);
		}
		_lastReceiveReliable = channel == Channel.Reliable;
		if (channel == Channel.Reliable)
		{
			transforms.Tick = 0u;
		}
		transformData.Update(transforms);
		lastCalculatedRateData.Update(goalData.Rates);
		goalData.ReceivedTick = _timeManager.LocalTick;
		bool flag2 = _currentGoalData == null;
		if (!flag2 && _currentGoalData.Transforms.ExtrapolationState == TransformData.ExtrapolateState.Active)
		{
			SetCurrentGoalData(goalData);
		}
		else if ((flag2 && _goalDataQueue.Count >= _interpolation) || channel == Channel.Reliable)
		{
			if (_goalDataQueue.Count > 0)
			{
				SetCurrentGoalData(_goalDataQueue.Dequeue());
				if (flag)
				{
					_goalDataQueue.Enqueue(goalData);
				}
			}
			else
			{
				SetCurrentGoalData(goalData);
			}
		}
		else
		{
			_goalDataQueue.Enqueue(goalData);
		}
		if (_goalDataQueue.Count > _interpolation + 3)
		{
			while (_goalDataQueue.Count > _interpolation)
			{
				ResettableObjectCaches<GoalData>.Store(_goalDataQueue.Dequeue());
			}
			SetCurrentGoalData(_goalDataQueue.Dequeue());
			SetInstantRates(_currentGoalData.Rates, 1u, -1f);
			SnapProperties(_currentGoalData.Transforms, force: true);
		}
	}

	private void SetCurrentGoalData(GoalData data)
	{
		if (_currentGoalData != null)
		{
			ResettableObjectCaches<GoalData>.Store(_currentGoalData);
		}
		_currentGoalData = data;
		this.OnNextGoal?.Invoke(data);
	}

	private void UpdateTransformData(ArraySegment<byte> packetData, TransformData prevTransformData, TransformData nextTransformData, ref ChangedFull changedFull)
	{
		DeserializePacket(packetData, prevTransformData, nextTransformData, ref changedFull);
		nextTransformData.Tick = _timeManager.LastPacketTick.LastRemoteTick;
	}

	internal void ConfigureForPrediction(NetworkObject.PredictionType predictionType)
	{
		_clientAuthoritative = false;
		_sendToOwner = false;
		if (_componentConfiguration != 0)
		{
			switch (predictionType)
			{
			case NetworkObject.PredictionType.Rigidbody:
				_componentConfiguration = ComponentConfigurationType.Rigidbody;
				break;
			case NetworkObject.PredictionType.Rigidbody2D:
				_componentConfiguration = ComponentConfigurationType.Rigidbody2D;
				break;
			case NetworkObject.PredictionType.Other:
				_componentConfiguration = ComponentConfigurationType.CharacterController;
				break;
			}
		}
		ConfigureComponents();
	}

	public void SetSynchronizedProperties(SynchronizedProperty value)
	{
		if (base.IsServerInitialized)
		{
			if (!base.IsController && _clientAuthoritative)
			{
				return;
			}
			ObserversSetSynchronizedProperties(value);
		}
		else
		{
			if (!_clientAuthoritative || !base.IsOwner)
			{
				return;
			}
			ServerSetSynchronizedProperties(value);
		}
		SetSynchronizedPropertiesInternal(value);
	}

	[ServerRpc]
	private void ServerSetSynchronizedProperties(SynchronizedProperty value)
	{
		RpcWriter___ServerSetSynchronizedProperties___535967898(value);
	}

	[ObserversRpc(BufferLast = true, ExcludeServer = true)]
	private void ObserversSetSynchronizedProperties(SynchronizedProperty value)
	{
		RpcWriter___ObserversSetSynchronizedProperties___535967898(value);
	}

	private void SetSynchronizedPropertiesInternal(SynchronizedProperty value)
	{
		_synchronizeParent = SynchronizedPropertyContains(value, SynchronizedProperty.Parent);
		_synchronizePosition = SynchronizedPropertyContains(value, SynchronizedProperty.Position);
		_synchronizeRotation = SynchronizedPropertyContains(value, SynchronizedProperty.Rotation);
		_synchronizeScale = SynchronizedPropertyContains(value, SynchronizedProperty.Scale);
		static bool SynchronizedPropertyContains(SynchronizedProperty whole, SynchronizedProperty part)
		{
			return (whole & part) == part;
		}
	}

	private void ResetState()
	{
		_teleport = false;
		ChangeTickSubscription(subscribe: false);
		_lastObserversRpcTick = 0u;
		_authoritativeClientData.ResetState();
		WriterPool.StoreAndDefault(ref _toClientChangedWriter);
		ObjectCaches<bool>.StoreAndDefault(ref _authoritativeClientData.HasData);
		ObjectCaches<ChangedDelta>.StoreAndDefault(ref _serverChangedSinceReliable);
		ResettableObjectCaches<TransformData>.StoreAndDefault(ref _lastReceivedClientTransformData);
		ResettableObjectCaches<TransformData>.StoreAndDefault(ref _lastReceivedServerTransformData);
		while (_goalDataQueue.Count > 0)
		{
			ResettableObjectCaches<GoalData>.Store(_goalDataQueue.Dequeue());
		}
		if (_lastSentTransformData != null)
		{
			_lastSentTransformData.ResetState();
		}
		ResettableObjectCaches<GoalData>.StoreAndDefault(ref _currentGoalData);
	}

	private void ResetState_OnDestroy()
	{
		ResettableObjectCaches<TransformData>.StoreAndDefault(ref _lastSentTransformData);
		WriterPool.StoreAndDefault(ref _toClientChangedWriter);
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyFishNet_002EComponent_002ETransforming_002ENetworkTransformFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___EarlyFishNet_002EComponent_002ETransforming_002ENetworkTransformFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
			RegisterObserversRpc(0u, RpcReader___ObserversSetSendToOwner___1140765316);
			RegisterServerRpc(1u, RpcReader___ServerSetInterval___1246646286);
			RegisterObserversRpc(2u, RpcReader___ObserversSetInterval___1246646286);
			RegisterTargetRpc(3u, RpcReader___TargetUpdateTransform___748863190);
			RegisterObserversRpc(4u, RpcReader___ObserversUpdateClientAuthoritativeTransform___2713644489);
			RegisterServerRpc(5u, RpcReader___ServerUpdateTransform___2713644489);
			RegisterServerRpc(6u, RpcReader___ServerSetSynchronizedProperties___535967898);
			RegisterObserversRpc(7u, RpcReader___ObserversSetSynchronizedProperties___535967898);
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateFishNet_002EComponent_002ETransforming_002ENetworkTransformFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___LateFishNet_002EComponent_002ETransforming_002ENetworkTransformFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	private void RpcWriter___ObserversSetSendToOwner___1140765316(bool value)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			return;
		}
		Channel channel = Channel.Reliable;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		GeneratedWriters___Internal.InstancedExtension___WriteBoolean(pooledWriter, value);
		SendObserversRpc(0u, pooledWriter, channel, DataOrderType.Default, bufferLast: true, excludeServer: true, excludeOwner: false);
		pooledWriter.Store();
	}

	private void RpcLogic___ObserversSetSendToOwner___1140765316(bool P_0)
	{
		_sendToOwner = P_0;
	}

	private void RpcReader___ObserversSetSendToOwner___1140765316(PooledReader PooledReader0, Channel channel)
	{
		bool flag = GeneratedReaders___Internal.InstancedExtension___ReadBoolean(PooledReader0);
		if (base.IsClientInitialized)
		{
			RpcLogic___ObserversSetSendToOwner___1140765316(flag);
		}
	}

	private void RpcWriter___ServerSetInterval___1246646286(byte value)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			return;
		}
		if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			return;
		}
		Channel channel = Channel.Reliable;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		GeneratedWriters___Internal.InstancedExtension___WriteUInt8Unpacked(pooledWriter, value);
		SendServerRpc(1u, pooledWriter, channel, DataOrderType.Default);
		pooledWriter.Store();
	}

	private void RpcLogic___ServerSetInterval___1246646286(byte P_0)
	{
		if (!_clientAuthoritative)
		{
			base.Owner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection Id {base.Owner.ClientId} has been kicked for trying to update this object without client authority.");
		}
		else
		{
			SetIntervalInternal(P_0);
		}
	}

	private void RpcReader___ServerSetInterval___1246646286(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		byte b = GeneratedReaders___Internal.InstancedExtension___ReadUInt8Unpacked(PooledReader0);
		if (base.IsServerInitialized && OwnerMatches(conn) && !conn.IsLocalClient)
		{
			RpcLogic___ServerSetInterval___1246646286(b);
		}
	}

	private void RpcWriter___ObserversSetInterval___1246646286(byte value)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			return;
		}
		Channel channel = Channel.Reliable;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		GeneratedWriters___Internal.InstancedExtension___WriteUInt8Unpacked(pooledWriter, value);
		SendObserversRpc(2u, pooledWriter, channel, DataOrderType.Default, bufferLast: true, excludeServer: false, excludeOwner: false);
		pooledWriter.Store();
	}

	private void RpcLogic___ObserversSetInterval___1246646286(byte P_0)
	{
		SetIntervalInternal(P_0);
	}

	private void RpcReader___ObserversSetInterval___1246646286(PooledReader PooledReader0, Channel channel)
	{
		byte b = GeneratedReaders___Internal.InstancedExtension___ReadUInt8Unpacked(PooledReader0);
		if (base.IsClientInitialized && !base.IsHostStarted)
		{
			RpcLogic___ObserversSetInterval___1246646286(b);
		}
	}

	private void RpcWriter___TargetUpdateTransform___748863190(NetworkConnection conn, ArraySegment<byte> data, Channel channel)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			return;
		}
		Channel channel2 = channel;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		GeneratedWriters___Internal.InstancedExtension___WriteArraySegmentAndSize(pooledWriter, data);
		SendTargetRpc(3u, pooledWriter, channel2, DataOrderType.Default, conn, excludeServer: false, validateTarget: false);
		pooledWriter.Store();
	}

	private void RpcLogic___TargetUpdateTransform___748863190(NetworkConnection P_0, ArraySegment<byte> P_1, Channel P_2)
	{
		if (P_1.Count != 0)
		{
			DataReceived(P_1, P_2, asServer: false);
		}
	}

	private void RpcReader___TargetUpdateTransform___748863190(PooledReader PooledReader0, Channel channel)
	{
		ArraySegment<byte> arraySegment = GeneratedReaders___Internal.InstancedExtension___ReadArraySegmentAndSize(PooledReader0);
		if (base.IsClientInitialized)
		{
			RpcLogic___TargetUpdateTransform___748863190(base.LocalConnection, arraySegment, channel);
		}
	}

	private void RpcWriter___ObserversUpdateClientAuthoritativeTransform___2713644489(ArraySegment<byte> data, Channel channel)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			return;
		}
		Channel channel2 = channel;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		GeneratedWriters___Internal.InstancedExtension___WriteArraySegmentAndSize(pooledWriter, data);
		SendObserversRpc(4u, pooledWriter, channel2, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
		pooledWriter.Store();
	}

	private void RpcLogic___ObserversUpdateClientAuthoritativeTransform___2713644489(ArraySegment<byte> P_0, Channel P_1)
	{
		if ((_clientAuthoritative || !base.IsOwner || _sendToOwner) && (!_clientAuthoritative || !base.IsOwner) && !base.IsServerInitialized)
		{
			uint lastRemoteTick = _timeManager.LastPacketTick.LastRemoteTick;
			if (lastRemoteTick > _lastObserversRpcTick)
			{
				_lastObserversRpcTick = lastRemoteTick;
				DataReceived(P_0, P_1, asServer: false);
			}
		}
	}

	private void RpcReader___ObserversUpdateClientAuthoritativeTransform___2713644489(PooledReader PooledReader0, Channel channel)
	{
		ArraySegment<byte> arraySegment = GeneratedReaders___Internal.InstancedExtension___ReadArraySegmentAndSize(PooledReader0);
		if (base.IsClientInitialized)
		{
			RpcLogic___ObserversUpdateClientAuthoritativeTransform___2713644489(arraySegment, channel);
		}
	}

	private void RpcWriter___ServerUpdateTransform___2713644489(ArraySegment<byte> data, Channel channel)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			return;
		}
		if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			return;
		}
		Channel channel2 = channel;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		GeneratedWriters___Internal.InstancedExtension___WriteArraySegmentAndSize(pooledWriter, data);
		SendServerRpc(5u, pooledWriter, channel2, DataOrderType.Default);
		pooledWriter.Store();
	}

	private void RpcLogic___ServerUpdateTransform___2713644489(ArraySegment<byte> P_0, Channel P_1)
	{
		if (!_clientAuthoritative)
		{
			base.Owner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection Id {base.Owner.ClientId} has been kicked for trying to update this object without client authority.");
			return;
		}
		TimeManager timeManager = base.TimeManager;
		uint lastRemoteTick = timeManager.LastPacketTick.LastRemoteTick;
		if (lastRemoteTick > _lastServerRpcTick)
		{
			_lastServerRpcTick = lastRemoteTick;
			_authoritativeClientData.Update(P_0, P_1, updateHasData: true, timeManager.LocalTick);
			DataReceived(P_0, P_1, asServer: true);
		}
	}

	private void RpcReader___ServerUpdateTransform___2713644489(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		ArraySegment<byte> arraySegment = GeneratedReaders___Internal.InstancedExtension___ReadArraySegmentAndSize(PooledReader0);
		if (base.IsServerInitialized && OwnerMatches(conn))
		{
			RpcLogic___ServerUpdateTransform___2713644489(arraySegment, channel);
		}
	}

	private void RpcWriter___ServerSetSynchronizedProperties___535967898(SynchronizedProperty value)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			return;
		}
		if (!base.IsOwner)
		{
			NetworkManager networkManager2 = base.NetworkManager;
			networkManager2.LogWarning("Cannot complete action because you are not the owner of this object. .");
			return;
		}
		Channel channel = Channel.Reliable;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		GeneratedWriters___Internal.GWrite___FishNet_002EComponent_002ETransforming_002ESynchronizedPropertyFishNet_002ESerializing_002EGenerated(pooledWriter, value);
		SendServerRpc(6u, pooledWriter, channel, DataOrderType.Default);
		pooledWriter.Store();
	}

	private void RpcLogic___ServerSetSynchronizedProperties___535967898(SynchronizedProperty P_0)
	{
		if (!_clientAuthoritative)
		{
			base.Owner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection Id {base.Owner.ClientId} has been kicked for trying to update this object without client authority.");
			return;
		}
		SetSynchronizedPropertiesInternal(P_0);
		ObserversSetSynchronizedProperties(P_0);
	}

	private void RpcReader___ServerSetSynchronizedProperties___535967898(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		SynchronizedProperty synchronizedProperty = GeneratedReaders___Internal.GRead___FishNet_002EComponent_002ETransforming_002ESynchronizedPropertyFishNet_002ESerializing_002EGenerateds(PooledReader0);
		if (base.IsServerInitialized && OwnerMatches(conn))
		{
			RpcLogic___ServerSetSynchronizedProperties___535967898(synchronizedProperty);
		}
	}

	private void RpcWriter___ObserversSetSynchronizedProperties___535967898(SynchronizedProperty value)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			return;
		}
		Channel channel = Channel.Reliable;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		GeneratedWriters___Internal.GWrite___FishNet_002EComponent_002ETransforming_002ESynchronizedPropertyFishNet_002ESerializing_002EGenerated(pooledWriter, value);
		SendObserversRpc(7u, pooledWriter, channel, DataOrderType.Default, bufferLast: true, excludeServer: true, excludeOwner: false);
		pooledWriter.Store();
	}

	private void RpcLogic___ObserversSetSynchronizedProperties___535967898(SynchronizedProperty P_0)
	{
		SetSynchronizedPropertiesInternal(P_0);
	}

	private void RpcReader___ObserversSetSynchronizedProperties___535967898(PooledReader PooledReader0, Channel channel)
	{
		SynchronizedProperty synchronizedProperty = GeneratedReaders___Internal.GRead___FishNet_002EComponent_002ETransforming_002ESynchronizedPropertyFishNet_002ESerializing_002EGenerateds(PooledReader0);
		if (base.IsClientInitialized)
		{
			RpcLogic___ObserversSetSynchronizedProperties___535967898(synchronizedProperty);
		}
	}

	private void Awake_UserLogic_FishNet_002EComponent_002ETransforming_002ENetworkTransform_FishNet_002ERuntime_002Edll()
	{
		_interval = Math.Max(_interval, (byte)1);
	}
}
