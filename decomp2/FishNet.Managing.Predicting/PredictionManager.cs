using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing.Statistic;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Performance;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace FishNet.Managing.Predicting;

[DisallowMultipleComponent]
[AddComponentMenu("FishNet/Manager/PredictionManager")]
public sealed class PredictionManager : MonoBehaviour
{
	internal class StatePacketTick
	{
		public uint Client;

		public uint Server;

		public bool IsUnset => Client == 0;

		public void Update(uint client, uint server)
		{
			Client = client;
			Server = server;
		}

		public void AddTick(uint quantity)
		{
			Client += quantity;
			Server += quantity;
		}
	}

	public delegate void PreReconcileDel(uint clientTick, uint serverTick);

	public delegate void ReconcileDel(uint clientTick, uint serverTick);

	public delegate void PostReconcileDel(uint clientTick, uint serverTick);

	public delegate void PrePhysicsSyncTransformDel(uint clientTick, uint serverTick);

	public delegate void PostPhysicsSyncTransformDel(uint clientTick, uint serverTick);

	public delegate void PreReplicateReplayDel(uint clientTick, uint serverTick);

	public delegate void ReplicateReplayDel(uint clientTick, uint serverTick);

	public delegate void PostReplicateReplayDel(uint clientTick, uint serverTick);

	internal class StatePacket : IResettable
	{
		public struct IncomingData
		{
			public ArraySegment<byte> Data;

			public Channel Channel;

			public IncomingData(ArraySegment<byte> data, Channel channel)
			{
				Data = data;
				Channel = channel;
			}
		}

		public List<IncomingData> Datas;

		public uint ClientTick;

		public uint ServerTick;

		public void Update(ArraySegment<byte> data, uint clientTick, uint serverTick, Channel channel)
		{
			AddData(data, channel);
			ServerTick = serverTick;
			ClientTick = clientTick;
		}

		public void AddData(ArraySegment<byte> data, Channel channel)
		{
			if (data.Array != null)
			{
				Datas.Add(new IncomingData(data, channel));
			}
		}

		public void ResetState()
		{
			for (int i = 0; i < Datas.Count; i++)
			{
				ByteArrayPool.Store(Datas[i].Data.Array);
			}
			CollectionCaches<IncomingData>.StoreAndDefault(ref Datas);
		}

		public void InitializeState()
		{
			Datas = CollectionCaches<IncomingData>.RetrieveList();
		}
	}

	internal bool ReduceClientTiming;

	[Tooltip("True to drop replicates from clients which are being received excessively. This can help with attacks but may cause client to temporarily desynchronize during connectivity issues. When false the server will hold at most up to 3 seconds worth of replicates, consuming multiple per tick to clear out the buffer quicker. This is good to ensure all inputs are executed but potentially could allow speed hacking.")]
	[SerializeField]
	private bool _dropExcessiveReplicates = true;

	[Tooltip("Maximum number of replicates a server can queue per object. Higher values will reduce the chance of dropped input when the client's connection is unstable, but will potentially add latency to the client's object both on the server and client.")]
	[SerializeField]
	private byte _maximumServerReplicates = 15;

	[FormerlySerializedAs("_localStates")]
	[Tooltip("True for the client to create local reconcile states. Enabling this feature allows reconciles to be sent less frequently and provides data to use for reconciles when packets are lost.")]
	[SerializeField]
	private bool _createLocalStates = true;

	[Tooltip("How many states to try and hold in a buffer before running them on clients. Larger values add resilience against network issues at the cost of running states later.")]
	[Range(0f, 5f)]
	[FormerlySerializedAs("_redundancyCount")]
	[FormerlySerializedAs("_interpolation")]
	[SerializeField]
	private byte _stateInterpolation = 2;

	[Tooltip("The order in which clients run states. Future favors performance and does not depend upon reconciles, while Past favors accuracy but clients must reconcile every tick.")]
	[SerializeField]
	private ReplicateStateOrder _stateOrder = ReplicateStateOrder.Appended;

	private byte _droppedReconcilesCount;

	private StatePacketTick _lastStatePacketTick = new StatePacketTick();

	private Queue<StatePacket> _reconcileStates = new Queue<StatePacket>();

	private Dictionary<uint, StatePacket> _stateLookups = new Dictionary<uint, StatePacket>();

	private uint _lastOrderedReadReconcileTick;

	private NetworkTrafficStatistics _networkTrafficStatistics;

	private NetworkManager _networkManager;

	private const byte MINIMUM_PAST_INPUTS = 1;

	internal const byte MAXIMUM_PAST_INPUTS = 5;

	private const byte MINIMUM_REPLICATE_QUEUE_SIZE = 2;

	private const byte MAXIMUM_REPLICATE_QUEUE_SIZE = byte.MaxValue;

	internal const int MINIMUM_APPENDED_INTERPOLATION_RECOMMENDATION = 2;

	internal const int MINIMUM_INSERTED_INTERPOLATION_RECOMMENDATION = 1;

	internal static readonly string ZERO_STATE_INTERPOLATION_MESSAGE = "When interpolation is 0 the chances of de-synchronizations on non-owned objects is increased drastically.";

	internal static readonly string LESS_THAN_MINIMUM_APPENDED_MESSAGE = $"When using Appended StateOrder and an interpolation less than {2} the chances of de-synchronizations on non-owned objects is increased.";

	internal static readonly string LESS_THAN_MINIMUM_INSERTED_MESSAGE = $"When using Inserted StateOrder and an interpolation less than {1} the chances of de-synchronizations on non-owned objects is increased.";

	internal const int STATE_HEADER_RESERVE_LENGTH = 10;

	public bool IsReconciling { get; private set; }

	public uint ClientReplayTick { get; private set; }

	public uint ServerReplayTick { get; private set; }

	public uint ClientStateTick { get; private set; }

	public uint ServerStateTick { get; private set; }

	internal bool DropExcessiveReplicates => _dropExcessiveReplicates;

	internal ushort MaximumPastReplicates => (ushort)(_networkManager.TimeManager.TickRate * 5);

	internal bool CreateLocalStates => _createLocalStates;

	public byte StateInterpolation => _stateInterpolation;

	public ReplicateStateOrder StateOrder => _stateOrder;

	internal bool IsAppendedStateOrder => _stateOrder == ReplicateStateOrder.Appended;

	internal byte RedundancyCount => (byte)(_stateInterpolation + 1);

	public event PreReconcileDel OnPreReconcile;

	public event ReconcileDel OnReconcile;

	public event PostReconcileDel OnPostReconcile;

	public event PrePhysicsSyncTransformDel OnPrePhysicsTransformSync;

	public event PostPhysicsSyncTransformDel OnPostPhysicsTransformSync;

	public event PostPhysicsSyncTransformDel OnPostReconcileSyncTransforms;

	public event PreReplicateReplayDel OnPreReplicateReplay;

	internal event ReplicateReplayDel OnReplicateReplay;

	public event PostReplicateReplayDel OnPostReplicateReplay;

	public void SetMaximumServerReplicates(byte value)
	{
		_maximumServerReplicates = (byte)Mathf.Clamp(value, 2, 255);
	}

	public byte GetMaximumServerReplicates()
	{
		return _maximumServerReplicates;
	}

	public void SetStateOrder(ReplicateStateOrder stateOrder)
	{
		if (_networkManager.IsServerStarted || stateOrder == _stateOrder)
		{
			return;
		}
		_stateOrder = stateOrder;
		if (stateOrder != 0 || !_networkManager.IsClientStarted)
		{
			return;
		}
		foreach (NetworkObject value in _networkManager.ClientManager.Objects.Spawned.Values)
		{
			value.EmptyReplicatesQueueIntoHistory();
		}
	}

	internal void InitializeOnce(NetworkManager manager)
	{
		_networkManager = manager;
		manager.StatisticsManager.TryGetNetworkTrafficStatistics(out _networkTrafficStatistics);
		ValidateClampInterpolation();
		_networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
	}

	private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
	{
		_droppedReconcilesCount = 0;
		_lastOrderedReadReconcileTick = 0u;
	}

	private void ValidateClampInterpolation()
	{
		ushort stateInterpolation = _stateInterpolation;
		if (_dropExcessiveReplicates && _stateInterpolation > _maximumServerReplicates)
		{
			_stateInterpolation = (byte)(_maximumServerReplicates - 1);
		}
		if (_stateInterpolation != stateInterpolation)
		{
			_networkManager.Log($"Interpolation has been set to {_stateInterpolation}.");
		}
		if (_stateInterpolation == 0)
		{
			_networkManager.LogWarning(ZERO_STATE_INTERPOLATION_MESSAGE);
		}
		else if (_stateOrder == ReplicateStateOrder.Appended && _stateInterpolation < 2)
		{
			_networkManager.LogWarning(LESS_THAN_MINIMUM_APPENDED_MESSAGE);
		}
		else if (_stateOrder == ReplicateStateOrder.Inserted && _stateInterpolation < 1)
		{
			_networkManager.LogWarning(LESS_THAN_MINIMUM_INSERTED_MESSAGE);
		}
	}

	public uint GetReconcileStateTick(bool clientTick)
	{
		if (!clientTick)
		{
			return ServerStateTick;
		}
		return ClientStateTick;
	}

	internal void ReconcileToStates()
	{
		if (!_networkManager.IsClientStarted || _reconcileStates.Count == 0)
		{
			return;
		}
		TimeManager timeManager = _networkManager.TimeManager;
		uint localTick = timeManager.LocalTick;
		uint estimatedLastRemoteTick = timeManager.LastPacketTick.Value();
		int num = 0;
		while (_reconcileStates.Count > 0)
		{
			num++;
			byte stateInterpolation = StateInterpolation;
			int num2 = ((_reconcileStates.Count <= stateInterpolation + 1) ? 1 : 2);
			if (num > num2 || !ConditionsMet(_reconcileStates.Peek()))
			{
				break;
			}
			StatePacket statePacket = _reconcileStates.Dequeue();
			bool flag = false;
			uint clientTick = statePacket.ClientTick;
			uint serverTick = statePacket.ServerTick;
			if (_networkManager.TimeManager.LowFrameRate && _networkManager.TimeManager.ClientUptime > 2f)
			{
				int num3 = Mathf.Max(1, _networkManager.TimeManager.TickRate / 3);
				if (_droppedReconcilesCount >= num3)
				{
					_droppedReconcilesCount = 0;
				}
				else
				{
					flag = true;
					_droppedReconcilesCount++;
				}
			}
			else
			{
				_droppedReconcilesCount = 0;
			}
			if (!flag)
			{
				IsReconciling = true;
				_lastStatePacketTick.Update(clientTick, serverTick);
				ClientStateTick = clientTick;
				ServerStateTick = serverTick;
				foreach (StatePacket.IncomingData data in statePacket.Datas)
				{
					PooledReader reader = ReaderPool.Retrieve(data.Data, _networkManager, Reader.DataSource.Server);
					_networkManager.ClientManager.ParseReader(reader, data.Channel);
					ReaderPool.Store(reader);
				}
				bool flag2 = timeManager.PhysicsMode == PhysicsMode.TimeManager;
				float num4 = (float)timeManager.TickDelta * _networkManager.TimeManager.GetPhysicsTimeScale();
				this.OnPreReconcile?.Invoke(ClientStateTick, ServerStateTick);
				this.OnReconcile?.Invoke(ClientStateTick, ServerStateTick);
				if (flag2)
				{
					this.OnPrePhysicsTransformSync?.Invoke(ClientStateTick, ServerStateTick);
					Physics.SyncTransforms();
					Physics2D.SyncTransforms();
					this.OnPostPhysicsTransformSync?.Invoke(ClientStateTick, ServerStateTick);
				}
				this.OnPostReconcileSyncTransforms?.Invoke(ClientStateTick, ServerStateTick);
				ClientReplayTick = ClientStateTick + 1;
				ServerReplayTick = ServerStateTick + 1;
				while (ClientReplayTick < localTick)
				{
					this.OnPreReplicateReplay?.Invoke(ClientReplayTick, ServerReplayTick);
					this.OnReplicateReplay?.Invoke(ClientReplayTick, ServerReplayTick);
					if (flag2 && num4 > 0f)
					{
						Physics.Simulate(num4);
						Physics2D.Simulate(num4);
					}
					this.OnPostReplicateReplay?.Invoke(ClientReplayTick, ServerReplayTick);
					ClientReplayTick++;
					ServerReplayTick++;
				}
				this.OnPostReconcile?.Invoke(ClientStateTick, ServerStateTick);
				ClientReplayTick = 0u;
				ServerReplayTick = 0u;
				IsReconciling = false;
			}
			DisposeOfStatePacket(statePacket);
			bool ConditionsMet(StatePacket spChecked)
			{
				if (spChecked == null)
				{
					return false;
				}
				uint num5 = (uint)((IsAppendedStateOrder ? 2 : 0) + stateInterpolation);
				bool num6 = spChecked.ServerTick < estimatedLastRemoteTick - num5;
				bool flag3 = spChecked.ClientTick < localTick - stateInterpolation;
				return num6 && flag3;
			}
		}
	}

	internal uint GetCreateReconcileTick(bool isOwner)
	{
		uint localTick = _networkManager.TimeManager.LocalTick;
		if (isOwner)
		{
			return localTick;
		}
		if (ClientStateTick == 0)
		{
			return 0u;
		}
		long num = localTick - ClientStateTick;
		if (num < 0)
		{
			num = 0L;
		}
		return ServerStateTick + (uint)(int)num;
	}

	internal void SendStateUpdate()
	{
		byte stateInterpolation = StateInterpolation;
		TransportManager transportManager = _networkManager.TransportManager;
		int num = 0;
		foreach (NetworkConnection value3 in _networkManager.ServerManager.Clients.Values)
		{
			uint value;
			if (!value3.ReplicateTick.IsUnset)
			{
				value = value3.ReplicateTick.Value();
			}
			else
			{
				uint num2 = value3.LocalTick.Value();
				uint num3 = (uint)(stateInterpolation * 2);
				if (num2 < num3)
				{
					num2 = 0u;
				}
				value = num2;
			}
			foreach (PooledWriter predictionStateWriter in value3.PredictionStateWriters)
			{
				num++;
				ArraySegment<byte> arraySegment = predictionStateWriter.GetArraySegment();
				predictionStateWriter.Position = 0;
				predictionStateWriter.WritePacketIdUnpacked(PacketId.StateUpdate);
				predictionStateWriter.WriteTickUnpacked(value);
				int value2 = arraySegment.Count - 10;
				predictionStateWriter.WriteInt32Unpacked(value2);
				Channel channel = Channel.Unreliable;
				_networkManager.TransportManager.CheckSetReliableChannel(arraySegment.Count, ref channel);
				transportManager.SendToClient((byte)channel, arraySegment, value3);
			}
			value3.StorePredictionStateWriters();
		}
	}

	internal void ParseStateUpdate(PooledReader reader, Channel channel)
	{
		uint lastRemoteTick = _networkManager.TimeManager.LastPacketTick.LastRemoteTick;
		if (_networkManager.IsServerStarted || lastRemoteTick < _lastOrderedReadReconcileTick)
		{
			reader.ReadTickUnpacked();
			int value = reader.ReadInt32Unpacked();
			reader.Skip(value);
			return;
		}
		_lastOrderedReadReconcileTick = lastRemoteTick;
		RemoveExcessiveStates();
		uint num = reader.ReadTickUnpacked();
		int num2 = reader.ReadInt32Unpacked();
		byte[] buffer = ByteArrayPool.Retrieve(num2);
		reader.ReadUInt8Array(ref buffer, num2);
		ArraySegment<byte> data = new ArraySegment<byte>(buffer, 0, num2);
		if (_stateLookups.TryGetValue(num, out var value2))
		{
			value2.AddData(data, channel);
			return;
		}
		StatePacket statePacket = ResettableObjectCaches<StatePacket>.Retrieve();
		statePacket.Update(data, num, lastRemoteTick, channel);
		_stateLookups[num] = statePacket;
		_reconcileStates.Enqueue(statePacket);
	}

	private void RemoveExcessiveStates()
	{
		int num = StateInterpolation * 4 + 2;
		if (IsAppendedStateOrder)
		{
			num += StateInterpolation;
		}
		int num2 = Mathf.Max(num, 4);
		while (_reconcileStates.Count > num2)
		{
			StatePacket sp = _reconcileStates.Dequeue();
			DisposeOfStatePacket(sp);
		}
	}

	private void DisposeOfStatePacket(StatePacket sp)
	{
		uint clientTick = sp.ClientTick;
		_stateLookups.Remove(clientTick);
		ResettableObjectCaches<StatePacket>.Store(sp);
	}
}
