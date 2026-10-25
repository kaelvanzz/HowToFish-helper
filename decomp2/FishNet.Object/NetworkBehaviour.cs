using System;
using System.Collections.Generic;
using System.Text;
using FishNet.CodeAnalysis.Annotations;
using FishNet.CodeGenerating;
using FishNet.Component.ColliderRollback;
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Logging;
using FishNet.Managing.Observing;
using FishNet.Managing.Predicting;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
using FishNet.Managing.Statistic;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object.Delegating;
using FishNet.Object.Prediction;
using FishNet.Object.Prediction.Delegating;
using FishNet.Object.Synchronizing;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Observing;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
using GameKit.Dependencies.Utilities.Types;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Object;

[ExcludeSerialization]
public abstract class NetworkBehaviour : MonoBehaviour
{
	private struct BufferedRpc
	{
		public PooledWriter Writer;

		public DataOrderType OrderType;

		public BufferedRpc(PooledWriter writer, DataOrderType orderType)
		{
			Writer = writer;
			OrderType = orderType;
		}
	}

	private struct SyncTypeWriter
	{
		public List<PooledWriter> Writers;

		public void Reset()
		{
			if (Writers != null)
			{
				for (int i = 0; i < Writers.Count; i++)
				{
					Writers[i].Clear();
				}
			}
		}

		public void Initialize()
		{
			Writers = CollectionCaches<PooledWriter>.RetrieveList();
			for (int i = 0; i < 2; i++)
			{
				Writers.Add(WriterPool.Retrieve());
			}
		}
	}

	private bool _onStartNetworkCalled;

	private bool _onStopNetworkCalled;

	private Dictionary<uint, ReplicateRpcDelegate> _replicateRpcDelegates;

	private Dictionary<uint, ReconcileRpcDelegate> _reconcileRpcDelegates;

	private int _remainingReplicateResends;

	private int _remainingReconcileResends;

	private uint _lastReplicateReadRemoteTick;

	private uint _replicateStartTick;

	private uint _lastOrderedReplicatedTick;

	private uint _lastReadReplicateTick;

	private uint _lastReadReconcileRemoteTick;

	private uint _lastReconcileTick;

	private Vector3 _lastTransformPosition;

	private Quaternion _lastTransformRotation;

	private Vector3 _lastTransformScale;

	[APIExclude]
	private bool _usesPrediction;

	private Dictionary<uint, RpcLinkType> _rpcLinks = new Dictionary<uint, RpcLinkType>();

	internal const int RPCLINK_RESERVED_BYTES = 2;

	private readonly Dictionary<uint, ServerRpcDelegate> _serverRpcDelegates = new Dictionary<uint, ServerRpcDelegate>();

	private readonly Dictionary<uint, ClientRpcDelegate> _observersRpcDelegates = new Dictionary<uint, ClientRpcDelegate>();

	private readonly Dictionary<uint, ClientRpcDelegate> _targetRpcDelegates = new Dictionary<uint, ClientRpcDelegate>();

	private uint _rpcMethodCount;

	private byte _rpcHashSize = 1;

	private readonly Dictionary<uint, BufferedRpc> _bufferedRpcs = new Dictionary<uint, BufferedRpc>();

	private readonly HashSet<NetworkConnection> _networkConnectionCache = new HashSet<NetworkConnection>();

	private static StringBuilder _stringBuilder = new StringBuilder();

	private const int MAXIMUM_RPC_HEADER_SIZE = 10;

	private static Dictionary<ReadPermission, SyncTypeWriter> _syncTypeWriters = new Dictionary<ReadPermission, SyncTypeWriter>();

	private Dictionary<uint, SyncBase> _syncTypes = new Dictionary<uint, SyncBase>();

	internal bool SyncTypeDirty;

	private static List<ReadPermission> _readPermissions;

	internal const byte SYNCTYPE_RESERVE_BYTES = 4;

	internal const byte PAYLOAD_RESERVE_BYTES = 4;

	[SerializeField]
	[HideInInspector]
	private byte _componentIndexCache = byte.MaxValue;

	private TransportManager _transportManagerCache;

	[SerializeField]
	[HideInInspector]
	private NetworkObject _networkObjectCache;

	private bool _initializedOnceServer;

	private bool _initializedOnceClient;

	private NetworkTrafficStatistics _networkTrafficStatistics;

	private string _typeName = string.Empty;

	public const byte MAXIMUM_NETWORKBEHAVIOURS = 254;

	public const byte UNSET_NETWORKBEHAVIOUR_ID = byte.MaxValue;

	[APIExclude]
	public bool OnStartServerCalled { get; private set; }

	[APIExclude]
	public bool OnStartClientCalled { get; private set; }

	public bool IsBehaviourReconciling { get; internal set; }

	[Obsolete("Use IsClientOnlyInitialized. Note the difference between IsClientOnlyInitialized and IsClientOnlyStarted.")]
	public bool IsClientOnly => IsClientOnlyInitialized;

	[Obsolete("Use IsServerOnlyInitialized. Note the difference between IsServerOnlyInitialized and IsServerOnlyStarted.")]
	public bool IsServerOnly => IsServerOnlyInitialized;

	[Obsolete("Use IsHostInitialized. Note the difference between IsHostInitialized and IsHostStarted.")]
	public bool IsHost => IsHostInitialized;

	[Obsolete("Use IsClientInitialized. Note the difference between IsClientInitialized and IsClientStarted.")]
	public bool IsClient => IsClientInitialized;

	[Obsolete("Use IsServerInitialized. Note the difference between IsServerInitialized and IsServerStarted.")]
	public bool IsServer => IsServerInitialized;

	public bool IsDeinitializing => _networkObjectCache.IsDeinitializing;

	public NetworkManager NetworkManager => _networkObjectCache.NetworkManager;

	public ServerManager ServerManager => _networkObjectCache.ServerManager;

	public ClientManager ClientManager => _networkObjectCache.ClientManager;

	public ObserverManager ObserverManager => _networkObjectCache.ObserverManager;

	public TransportManager TransportManager => _networkObjectCache.TransportManager;

	public TimeManager TimeManager => _networkObjectCache.TimeManager;

	public FishNet.Managing.Scened.SceneManager SceneManager => _networkObjectCache.SceneManager;

	public PredictionManager PredictionManager => _networkObjectCache.PredictionManager;

	public RollbackManager RollbackManager => _networkObjectCache.RollbackManager;

	public NetworkObserver NetworkObserver => _networkObjectCache.NetworkObserver;

	public bool IsClientInitialized => _networkObjectCache.IsClientInitialized;

	public bool IsClientStarted => _networkObjectCache.IsClientStarted;

	public bool IsClientOnlyInitialized => _networkObjectCache.IsClientOnlyInitialized;

	public bool IsClientOnlyStarted => _networkObjectCache.IsClientOnlyStarted;

	public bool IsServerInitialized => _networkObjectCache.IsServerInitialized;

	public bool IsServerStarted => _networkObjectCache.IsServerStarted;

	public bool IsServerOnlyInitialized => _networkObjectCache.IsServerOnlyInitialized;

	public bool IsServerOnlyStarted => _networkObjectCache.IsServerOnlyStarted;

	public bool IsHostInitialized => _networkObjectCache.IsHostInitialized;

	public bool IsHostStarted => _networkObjectCache.IsHostStarted;

	public bool IsOffline => _networkObjectCache.IsOffline;

	[Obsolete("Use GetIsNetworked.")]
	public bool IsNetworked => GetIsNetworked();

	public bool IsManagerReconciling => _networkObjectCache.IsManagerReconciling;

	public HashSet<NetworkConnection> Observers => _networkObjectCache.Observers;

	[PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "OnStartServer", "")]
	[PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "OnStartNetwork", " Use base.Owner.IsLocalClient instead.")]
	[PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "Awake", "")]
	[PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "Start", "")]
	public bool IsOwner => _networkObjectCache.IsOwner;

	[PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "OnStartServer", "")]
	[PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "OnStartNetwork", " Use (base.Owner.IsLocalClient || (base.IsServerInitialized && !Owner.Isvalid) instead.")]
	[PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "Awake", "")]
	[PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "Start", "")]
	public bool IsController
	{
		get
		{
			if (!_networkObjectCache.IsOwner)
			{
				if (_networkObjectCache.IsServerInitialized)
				{
					return !_networkObjectCache.Owner.IsValid;
				}
				return false;
			}
			return true;
		}
	}

	[Obsolete("Use IsController.")]
	public bool HasAuthority => IsController;

	public NetworkConnection Owner
	{
		get
		{
			if (_networkObjectCache == null)
			{
				return NetworkManager.EmptyConnection;
			}
			return _networkObjectCache.Owner;
		}
	}

	public int OwnerId => _networkObjectCache.OwnerId;

	public int ObjectId => _networkObjectCache.ObjectId;

	public NetworkConnection LocalConnection => _networkObjectCache.LocalConnection;

	public bool IsSpawned => _networkObjectCache.IsSpawned;

	public byte ComponentIndex
	{
		get
		{
			return _componentIndexCache;
		}
		private set
		{
			_componentIndexCache = value;
		}
	}

	public NetworkObject NetworkObject => _networkObjectCache;

	public virtual void WritePayload(NetworkConnection connection, Writer writer)
	{
	}

	public virtual void ReadPayload(NetworkConnection connection, Reader reader)
	{
	}

	internal void InvokeSyncTypeOnStartCallbacks(bool asServer)
	{
		foreach (SyncBase value in _syncTypes.Values)
		{
			value.OnStartCallback(asServer);
		}
	}

	internal void InvokeSyncTypeOnStopCallbacks(bool asServer)
	{
		foreach (SyncBase value in _syncTypes.Values)
		{
			value.OnStopCallback(asServer);
		}
	}

	internal void InvokeOnNetwork_Internal(bool start)
	{
		if (start)
		{
			if (!_onStartNetworkCalled)
			{
				if (!base.gameObject.activeInHierarchy)
				{
					NetworkInitialize___Early();
					NetworkInitialize___Late();
				}
				OnStartNetwork_Internal();
			}
		}
		else if (!_onStopNetworkCalled)
		{
			OnStopNetwork_Internal();
		}
	}

	internal virtual void OnStartNetwork_Internal()
	{
		_onStartNetworkCalled = true;
		_onStopNetworkCalled = false;
		OnStartNetwork();
	}

	public virtual void OnStartNetwork()
	{
	}

	internal virtual void OnStopNetwork_Internal()
	{
		_onStopNetworkCalled = true;
		_onStartNetworkCalled = false;
		OnStopNetwork();
	}

	public virtual void OnStopNetwork()
	{
	}

	internal void OnStartServer_Internal()
	{
		OnStartServerCalled = true;
		OnStartServer();
	}

	public virtual void OnStartServer()
	{
	}

	internal void OnStopServer_Internal()
	{
		OnStartServerCalled = false;
		ReturnRpcLinks();
		OnStopServer();
	}

	public virtual void OnStopServer()
	{
	}

	internal void OnOwnershipServer_Internal(NetworkConnection prevOwner)
	{
		ResetState_Prediction(asServer: true);
		OnOwnershipServer(prevOwner);
	}

	public virtual void OnOwnershipServer(NetworkConnection prevOwner)
	{
	}

	public virtual void OnSpawnServer(NetworkConnection connection)
	{
	}

	public virtual void OnDespawnServer(NetworkConnection connection)
	{
	}

	internal void OnStartClient_Internal()
	{
		OnStartClientCalled = true;
		OnStartClient();
	}

	public virtual void OnStartClient()
	{
	}

	internal void OnStopClient_Internal()
	{
		OnStartClientCalled = false;
		OnStopClient();
	}

	public virtual void OnStopClient()
	{
	}

	internal void OnOwnershipClient_Internal(NetworkConnection prevOwner)
	{
		if (IsOwner || prevOwner == LocalConnection)
		{
			ResetState_Prediction(asServer: false);
		}
		OnOwnershipClient(prevOwner);
	}

	public virtual void OnOwnershipClient(NetworkConnection prevOwner)
	{
	}

	public bool CanLog(LoggingType loggingType)
	{
		return NetworkManager.CanLog(loggingType);
	}

	internal void Preinitialize_Prediction(bool asServer)
	{
	}

	internal void Deinitialize_Prediction(bool asServer)
	{
	}

	internal void OnDestroy_Prediction()
	{
		CollectionCaches<uint, ReplicateRpcDelegate>.StoreAndDefault(ref _replicateRpcDelegates);
		CollectionCaches<uint, ReconcileRpcDelegate>.StoreAndDefault(ref _reconcileRpcDelegates);
	}

	[MakePublic]
	public void RegisterReplicateRpc(uint hash, ReplicateRpcDelegate del)
	{
		_usesPrediction = true;
		if (_replicateRpcDelegates == null)
		{
			_replicateRpcDelegates = CollectionCaches<uint, ReplicateRpcDelegate>.RetrieveDictionary();
		}
		_replicateRpcDelegates[hash] = del;
	}

	[MakePublic]
	public void RegisterReconcileRpc(uint hash, ReconcileRpcDelegate del)
	{
		if (_reconcileRpcDelegates == null)
		{
			_reconcileRpcDelegates = CollectionCaches<uint, ReconcileRpcDelegate>.RetrieveDictionary();
		}
		_reconcileRpcDelegates[hash] = del;
	}

	internal void OnReplicateRpc(int readerPositionAfterDebug, uint? hash, PooledReader reader, NetworkConnection sendingClient, Channel channel)
	{
		if (!hash.HasValue)
		{
			hash = ReadRpcHash(reader);
		}
		reader.NetworkManager = _networkObjectCache.NetworkManager;
		if (_replicateRpcDelegates.TryGetValueIL2CPP(hash.Value, out var value))
		{
			value(reader, sendingClient, channel);
		}
		else
		{
			_networkObjectCache.NetworkManager.LogWarning($"Replicate not found for hash {hash.Value} on {base.gameObject.name}, behaviour {GetType().Name}. Remainder of packet may become corrupt.");
		}
		if (_networkTrafficStatistics != null)
		{
			_networkTrafficStatistics.AddInboundPacketIdData(PacketId.Replicate, GetRpcName(PacketId.Replicate, hash.Value), reader.Position - readerPositionAfterDebug + 2, base.gameObject, sendingClient.IsValid);
		}
	}

	internal void OnReconcileRpc(int readerPositionAfterDebug, uint? hash, PooledReader reader, Channel channel)
	{
		if (!hash.HasValue)
		{
			hash = ReadRpcHash(reader);
		}
		reader.NetworkManager = _networkObjectCache.NetworkManager;
		if (_reconcileRpcDelegates.TryGetValueIL2CPP(hash.Value, out var value))
		{
			value(reader, channel);
		}
		else
		{
			_networkObjectCache.NetworkManager.LogWarning($"Reconcile not found for hash {hash.Value}. Remainder of packet may become corrupt.");
		}
		if (_networkTrafficStatistics != null)
		{
			_networkTrafficStatistics.AddInboundPacketIdData(PacketId.Reconcile, GetRpcName(PacketId.Reconcile, hash.Value), reader.Position - readerPositionAfterDebug + 2, base.gameObject, asServer: false);
		}
	}

	private void ResetState_Prediction(bool asServer)
	{
		if (!asServer)
		{
			_lastReadReconcileRemoteTick = 0u;
			_lastReconcileTick = 0u;
		}
		_lastOrderedReplicatedTick = 0u;
		_lastReplicateReadRemoteTick = 0u;
		_lastReadReplicateTick = 0u;
		ClearReplicateCache();
	}

	public virtual void ClearReplicateCache()
	{
	}

	[MakePublic]
	public void ClearReplicateCache_Internal<T, T2>(BasicQueue<ReplicateDataContainer<T>> replicatesQueue, RingBuffer<ReplicateDataContainer<T>> replicatesHistory, RingBuffer<LocalReconcile<T2>> reconcilesHistory, ref T lastReadReplicate, ref T2 lastReadReconcile) where T : IReplicateData, new() where T2 : IReconcileData, new()
	{
		while (replicatesQueue.Count > 0)
		{
			replicatesQueue.Dequeue().Dispose();
		}
		if (lastReadReplicate != null)
		{
			lastReadReplicate.Dispose();
		}
		lastReadReplicate = default(T);
		if (lastReadReconcile != null)
		{
			lastReadReconcile.Dispose();
		}
		lastReadReconcile = default(T2);
		for (int i = 0; i < replicatesHistory.Count; i++)
		{
			replicatesHistory[i].Dispose();
		}
		replicatesHistory.Clear();
		ClearReconcileHistory(reconcilesHistory);
	}

	[MakePublic]
	public void Server_SendReconcileRpc<T>(uint hash, ref T lastReconcileData, T reconcileData, Channel channel) where T : IReconcileData
	{
		if (!IsSpawned)
		{
			return;
		}
		if (channel == Channel.Reliable)
		{
			_remainingReconcileResends = 1;
		}
		if (_remainingReconcileResends == 0)
		{
			return;
		}
		_remainingReconcileResends--;
		bool enableStateForwarding = _networkObjectCache.EnableStateForwarding;
		if (!Owner.IsValid && !enableStateForwarding)
		{
			return;
		}
		PooledWriter pooledWriter = WriterPool.Retrieve();
		pooledWriter.WriteReconcile(reconcileData);
		lastReconcileData = reconcileData;
		RpcLinkType value;
		PooledWriter pooledWriter2 = ((!_rpcLinks.TryGetValueIL2CPP(hash, out value)) ? CreateRpc(hash, pooledWriter, PacketId.Reconcile, Channel.Reliable) : CreateLinkedRpc(value, pooledWriter, Channel.Reliable));
		if (!enableStateForwarding)
		{
			Owner.WriteState(pooledWriter2);
		}
		else
		{
			foreach (NetworkConnection observer in Observers)
			{
				observer.WriteState(pooledWriter2);
			}
		}
		if (_networkTrafficStatistics != null)
		{
			int num = (enableStateForwarding ? (pooledWriter2.Length * Observers.Count) : pooledWriter2.Length);
			_networkTrafficStatistics.AddInboundPacketIdData(PacketId.Reconcile, GetRpcName(PacketId.Reconcile, hash), num + 2, base.gameObject, asServer: true);
		}
		pooledWriter.Store();
		pooledWriter2.Store();
	}

	private bool TransformChanged()
	{
		if (TimeManager.PhysicsMode == PhysicsMode.Disabled)
		{
			return false;
		}
		float num = 4E-06f;
		bool flag = false;
		flag |= (base.transform.position - _lastTransformPosition).sqrMagnitude > num;
		if (!flag)
		{
			flag |= (base.transform.rotation.eulerAngles - _lastTransformRotation.eulerAngles).sqrMagnitude > num;
		}
		if (!flag)
		{
			flag |= (base.transform.localScale - _lastTransformScale).sqrMagnitude > num;
		}
		if (flag)
		{
			_lastTransformPosition = base.transform.position;
			_lastTransformRotation = base.transform.rotation;
			_lastTransformScale = base.transform.localScale;
		}
		return flag;
	}

	[MakePublic]
	public void Replicate_Current<T>(ReplicateUserLogicDelegate<T> del, uint methodHash, BasicQueue<ReplicateDataContainer<T>> replicatesQueue, RingBuffer<ReplicateDataContainer<T>> replicatesHistory, ReplicateDataContainer<T> dataContainer) where T : IReplicateData, new()
	{
		if (!_networkObjectCache.PredictionManager.IsReconciling)
		{
			if (_networkObjectCache.IsController)
			{
				Replicate_Authoritative(del, methodHash, replicatesHistory, dataContainer);
			}
			else
			{
				Replicate_NonAuthoritative(del, replicatesQueue, replicatesHistory);
			}
		}
	}

	private void Replicate_Authoritative<T>(ReplicateUserLogicDelegate<T> del, uint methodHash, RingBuffer<ReplicateDataContainer<T>> replicatesHistory, ReplicateDataContainer<T> dataContainer) where T : IReplicateData, new()
	{
		bool flag = !Owner.IsValid && IsServerStarted;
		if (!IsOwner && !flag)
		{
			return;
		}
		Func<T, bool> isDefault = PublicPropertyComparer<T>.IsDefault;
		if (isDefault == null)
		{
			NetworkManager.LogError("PublicPropertyComparer not found for type " + typeof(T).FullName);
			return;
		}
		PredictionManager predictionManager = NetworkManager.PredictionManager;
		uint localTick = TimeManager.LocalTick;
		if (IsHostStarted)
		{
			int count = replicatesHistory.Count;
			int redundancyCount = predictionManager.RedundancyCount;
			int num = count - redundancyCount;
			if (num > 0)
			{
				for (int i = 0; i < num; i++)
				{
					replicatesHistory[i].Dispose();
				}
				replicatesHistory.RemoveRange(fromStart: true, num);
			}
		}
		dataContainer.SetDataTick(localTick);
		AddReplicatesHistory(replicatesHistory, dataContainer);
		bool num2 = !isDefault(dataContainer.Data) || TransformChanged();
		byte redundancyCount2 = PredictionManager.RedundancyCount;
		if (num2)
		{
			_remainingReplicateResends = redundancyCount2;
			_remainingReconcileResends = redundancyCount2;
		}
		if (_remainingReplicateResends > 0)
		{
			bool toServer = !IsServerStarted;
			Replicate_SendAuthoritative(toServer, methodHash, redundancyCount2, replicatesHistory, localTick, dataContainer.Channel, GetDeltaSerializeOption());
			_remainingReplicateResends--;
		}
		SetReplicateTick(localTick, createdReplicate: true);
		del(dataContainer.Data, ReplicateState.Ticked | ReplicateState.Created, dataContainer.Channel);
	}

	private void Replicate_NonAuthoritative<T>(ReplicateUserLogicDelegate<T> del, BasicQueue<ReplicateDataContainer<T>> replicatesQueue, RingBuffer<ReplicateDataContainer<T>> replicatesHistory) where T : IReplicateData, new()
	{
		bool isServerStarted = _networkObjectCache.IsServerStarted;
		bool flag = !Owner.IsValid && isServerStarted;
		if (IsOwner || flag || (!_networkObjectCache.EnableStateForwarding && !isServerStarted))
		{
			return;
		}
		TimeManager tm = _networkObjectCache.TimeManager;
		PredictionManager pm = _networkObjectCache.PredictionManager;
		uint localTick = tm.LocalTick;
		bool isServer = _networkObjectCache.IsServerStarted;
		bool isAppendedStateOrder = pm.IsAppendedStateOrder;
		if (isServer || isAppendedStateOrder)
		{
			if (replicatesQueue.Count == 0)
			{
				ReplicateDefaultData();
			}
			else if (localTick >= _replicateStartTick)
			{
				_replicateStartTick = 0u;
				bool flag2 = false;
				ReplicateDataContainer<T> result;
				while (replicatesQueue.TryDequeue(out result))
				{
					if (result.Data.GetTick() > _lastReconcileTick)
					{
						flag2 = true;
						break;
					}
				}
				if (!flag2)
				{
					return;
				}
				_remainingReconcileResends = pm.RedundancyCount;
				ReplicateData(result, ReplicateState.Ticked | ReplicateState.Created);
				int count = replicatesQueue.Count;
				bool num = !pm.DropExcessiveReplicates || IsClientOnlyStarted;
				int stateInterpolation = _networkObjectCache.PredictionManager.StateInterpolation;
				if (num && count > stateInterpolation)
				{
					int b = count - stateInterpolation;
					int num2 = Mathf.Min(1, b);
					for (int i = 0; i < num2; i++)
					{
						ReplicateData(replicatesQueue.Dequeue(), ReplicateState.Ticked | ReplicateState.Created);
					}
				}
			}
			else
			{
				ReplicateDefaultData();
			}
		}
		else
		{
			ReplicateDefaultData();
		}
		uint GetDefaultedLastReplicateTick()
		{
			if (_lastOrderedReplicatedTick == 0)
			{
				_lastOrderedReplicatedTick = tm.LastPacketTick.Value() + pm.StateInterpolation;
			}
			return _lastOrderedReplicatedTick;
		}
		void ReplicateData(ReplicateDataContainer<T> data, ReplicateState state)
		{
			uint tick = data.Data.GetTick();
			SetReplicateTick(tick, state.ContainsCreated());
			if (isServer)
			{
				AddReplicatesHistory(replicatesHistory, data);
			}
			else
			{
				InsertIntoReplicateHistory(data, replicatesHistory);
			}
			del(data.Data, state, data.Channel);
		}
		void ReplicateDefaultData()
		{
			ReplicateDataContainer<T> @default = ReplicateDataContainer<T>.GetDefault(GetDefaultedLastReplicateTick() + 1);
			ReplicateData(@default, ReplicateState.Ticked);
		}
	}

	internal virtual void Replicate_Replay_Start(uint replayTick)
	{
	}

	[MakePublic]
	public void Replicate_Replay<T>(uint replayTick, ReplicateUserLogicDelegate<T> del, RingBuffer<ReplicateDataContainer<T>> replicatesHistory) where T : IReplicateData, new()
	{
		if (IsBehaviourReconciling)
		{
			if (_networkObjectCache.IsController)
			{
				Replicate_Replay_Authoritative(replayTick, del, replicatesHistory);
			}
			else
			{
				Replicate_Replay_NonAuthoritative(replayTick, del, replicatesHistory);
			}
		}
	}

	private void Replicate_Replay_Authoritative<T>(uint replayTick, ReplicateUserLogicDelegate<T> del, RingBuffer<ReplicateDataContainer<T>> replicatesHistory) where T : IReplicateData, new()
	{
		ReplicateTickFinder.DataPlacementResult findResult;
		int replicateHistoryIndex = ReplicateTickFinder.GetReplicateHistoryIndex(replayTick, replicatesHistory, out findResult);
		if (findResult == ReplicateTickFinder.DataPlacementResult.Exact)
		{
			ReplicateDataContainer<T> replicateDataContainer = replicatesHistory[replicateHistoryIndex];
			ReplicateState state = ReplicateState.Ticked | ReplicateState.Replayed | ReplicateState.Created;
			del(replicateDataContainer.Data, state, replicateDataContainer.Channel);
		}
	}

	[MakePublic]
	public void Replicate_Replay_NonAuthoritative<T>(uint replayTick, ReplicateUserLogicDelegate<T> del, RingBuffer<ReplicateDataContainer<T>> replicatesHistory) where T : IReplicateData, new()
	{
		ReplicateDataContainer<T> dataContainer = default(ReplicateDataContainer<T>);
		ReplicateState state = default(ReplicateState);
		if (_networkObjectCache.PredictionManager.IsAppendedStateOrder || replayTick == _networkObjectCache.PredictionManager.ServerStateTick + 1)
		{
			ReplicateTickFinder.DataPlacementResult findResult;
			int replicateHistoryIndex = ReplicateTickFinder.GetReplicateHistoryIndex(replayTick, replicatesHistory, out findResult);
			if (findResult == ReplicateTickFinder.DataPlacementResult.Exact)
			{
				dataContainer = replicatesHistory[replicateHistoryIndex];
				state = ReplicateState.Replayed;
				bool isCreated = dataContainer.IsCreated;
				if (isCreated)
				{
					state |= ReplicateState.Created;
				}
				if (replayTick <= _lastOrderedReplicatedTick || isCreated)
				{
					state |= ReplicateState.Ticked;
				}
			}
			else
			{
				SetDataToDefault();
			}
		}
		else
		{
			SetDataToDefault();
		}
		del(dataContainer.Data, state, dataContainer.Channel);
		void SetDataToDefault()
		{
			dataContainer = ReplicateDataContainer<T>.GetDefault(replayTick);
			state = ReplicateState.Replayed;
		}
	}

	[MakePublic]
	public virtual void EmptyReplicatesQueueIntoHistory_Start()
	{
	}

	[MakePublic]
	public void EmptyReplicatesQueueIntoHistory<T>(BasicQueue<ReplicateDataContainer<T>> replicatesQueue, RingBuffer<ReplicateDataContainer<T>> replicatesHistory) where T : IReplicateData, new()
	{
		ReplicateDataContainer<T> result;
		while (replicatesQueue.TryDequeue(out result))
		{
			InsertIntoReplicateHistory(result, replicatesHistory);
		}
	}

	private DeltaSerializerOption GetDeltaSerializeOption()
	{
		uint localTick = _networkObjectCache.TimeManager.LocalTick;
		ushort tickRate = _networkObjectCache.TimeManager.TickRate;
		if (_networkObjectCache.ObserverAddedTick == localTick)
		{
			return DeltaSerializerOption.FullSerialize;
		}
		if (localTick % tickRate == 0)
		{
			return DeltaSerializerOption.FullSerialize;
		}
		return DeltaSerializerOption.RootSerialize;
	}

	private void Replicate_SendAuthoritative<T>(bool toServer, uint hash, int pastInputs, RingBuffer<ReplicateDataContainer<T>> replicatesHistory, uint queuedTick, Channel channel, DeltaSerializerOption deltaOption) where T : IReplicateData, new()
	{
		if (!IsSpawned)
		{
			return;
		}
		int count = replicatesHistory.Count;
		if (count <= 0)
		{
			return;
		}
		if (count < pastInputs)
		{
			pastInputs = count;
		}
		int offset = count - pastInputs;
		PooledWriter pooledWriter = WriterPool.Retrieve(1000);
		if (!toServer)
		{
			pooledWriter.WriteTickUnpacked(queuedTick);
		}
		pooledWriter.WriteReplicate(replicatesHistory, offset);
		_transportManagerCache.CheckSetReliableChannel(pooledWriter.Length + 10, ref channel);
		PooledWriter pooledWriter2 = CreateRpc(hash, pooledWriter, PacketId.Replicate, channel);
		int num = 0;
		if (toServer)
		{
			num = pooledWriter2.Length;
			NetworkManager.TransportManager.SendToServer((byte)channel, pooledWriter2.GetArraySegment());
		}
		else if (_networkObjectCache.EnableStateForwarding)
		{
			_networkConnectionCache.Clear();
			_networkConnectionCache.Add(Owner);
			if (IsClientStarted)
			{
				_networkConnectionCache.Add(ClientManager.Connection);
			}
			num = pooledWriter2.Length * (Observers.Count - _networkConnectionCache.Count);
			NetworkManager.TransportManager.SendToClients((byte)channel, pooledWriter2.GetArraySegment(), Observers, _networkConnectionCache);
		}
		if (num != 0 && _networkTrafficStatistics != null)
		{
			_networkTrafficStatistics.AddOutboundPacketIdData(PacketId.Replicate, GetRpcName(PacketId.Replicate, hash), num, base.gameObject, asServer: true);
		}
		if (channel == Channel.Reliable)
		{
			_remainingReplicateResends = 0;
		}
		pooledWriter.StoreLength();
		pooledWriter2.StoreLength();
	}

	[MakePublic]
	public void Replicate_Reader<T>(uint hash, PooledReader reader, NetworkConnection sender, ref ReplicateDataContainer<T> lastReadReplicate, BasicQueue<ReplicateDataContainer<T>> replicatesQueue, RingBuffer<ReplicateDataContainer<T>> replicatesHistory, Channel channel) where T : IReplicateData, new()
	{
		PredictionManager predictionManager = _networkObjectCache.PredictionManager;
		TimeManager timeManager = _networkObjectCache.TimeManager;
		bool flag = reader.Source == Reader.DataSource.Server;
		uint tick = ((!flag) ? timeManager.LastPacketTick.LastRemoteTick : reader.ReadTickUnpacked());
		List<ReplicateDataContainer<T>> list = reader.ReadReplicate<T>(tick);
		if (list.Count > 0)
		{
			lastReadReplicate.Dispose();
			lastReadReplicate = list[list.Count - 1];
		}
		if ((!flag || !IsHostStarted) && (flag || OwnerMatches(sender)) && TimeManager.LastPacketTick.LastRemoteTick >= _lastReplicateReadRemoteTick)
		{
			_lastReplicateReadRemoteTick = TimeManager.LastPacketTick.LastRemoteTick;
			if (!flag && !Owner.IsLocalClient && list.Count > predictionManager.RedundancyCount)
			{
				sender.Kick(reader, KickReason.ExploitAttempt, LoggingType.Common, "Connection " + sender.ToString() + " sent too many past replicates. Connection will be kicked immediately.");
				return;
			}
			Replicate_EnqueueReceivedReplicate(list, replicatesQueue, replicatesHistory);
			Replicate_SendNonAuthoritative(hash, replicatesQueue, channel);
			CollectionCaches<ReplicateDataContainer<T>>.Store(list);
		}
	}

	[MakePublic]
	public void Replicate_SendNonAuthoritative<T>(uint hash, BasicQueue<ReplicateDataContainer<T>> replicatesQueue, Channel channel) where T : IReplicateData, new()
	{
		if (!IsServerStarted || !_networkObjectCache.EnableStateForwarding)
		{
			return;
		}
		int count = replicatesQueue.Count;
		if (count == 0)
		{
			return;
		}
		int count2 = Observers.Count;
		if (count2 != 0 && (!Owner.IsValid || count2 != 1))
		{
			PooledWriter pooledWriter = WriterPool.Retrieve(1000);
			uint num = _networkObjectCache.TimeManager.LocalTick + (uint)(count - 1);
			if (_replicateStartTick != 0)
			{
				num += _replicateStartTick - TimeManager.LocalTick;
			}
			pooledWriter.WriteTickUnpacked(num);
			int redundancyCount = Mathf.Min(_networkObjectCache.PredictionManager.RedundancyCount, count);
			pooledWriter.WriteReplicate(replicatesQueue, redundancyCount);
			PooledWriter pooledWriter2 = CreateRpc(hash, pooledWriter, PacketId.Replicate, channel);
			_networkConnectionCache.Clear();
			if (Owner.IsValid)
			{
				_networkConnectionCache.Add(Owner);
			}
			if (IsClientStarted && !Owner.IsLocalClient)
			{
				_networkConnectionCache.Add(ClientManager.Connection);
			}
			if (_networkTrafficStatistics != null)
			{
				int bytes = pooledWriter2.Length * (Observers.Count - _networkConnectionCache.Count);
				_networkTrafficStatistics.AddOutboundPacketIdData(PacketId.Replicate, GetRpcName(PacketId.Replicate, hash), bytes, base.gameObject, asServer: true);
			}
			NetworkManager.TransportManager.SendToClients((byte)channel, pooledWriter2.GetArraySegment(), Observers, _networkConnectionCache, splitLargeMessages: false);
			pooledWriter.StoreLength();
			pooledWriter2.StoreLength();
		}
	}

	private void Replicate_EnqueueReceivedReplicate<T>(List<ReplicateDataContainer<T>> readDatas, BasicQueue<ReplicateDataContainer<T>> replicatesQueue, RingBuffer<ReplicateDataContainer<T>> replicatesHistory) where T : IReplicateData, new()
	{
		int count = replicatesQueue.Count;
		PredictionManager predictionManager = PredictionManager;
		bool isServerStarted = _networkObjectCache.IsServerStarted;
		bool isAppendedStateOrder = predictionManager.IsAppendedStateOrder;
		int num = (IsServerStarted ? predictionManager.GetMaximumServerReplicates() : predictionManager.MaximumPastReplicates);
		for (int i = 0; i < readDatas.Count; i++)
		{
			ReplicateDataContainer<T> replicateDataContainer = readDatas[i];
			replicateDataContainer.IsCreated = true;
			uint tick = replicateDataContainer.Data.GetTick();
			if (tick <= _lastReadReplicateTick)
			{
				replicateDataContainer.Dispose();
				continue;
			}
			_lastReadReplicateTick = tick;
			if (replicatesQueue.Count > num)
			{
				replicatesQueue.Dequeue().Dispose();
			}
			if (isServerStarted || isAppendedStateOrder)
			{
				replicatesQueue.Enqueue(replicateDataContainer);
			}
			else
			{
				InsertIntoReplicateHistory(replicateDataContainer, replicatesHistory);
			}
		}
		if ((isServerStarted || isAppendedStateOrder) && count == 0 && replicatesQueue.Count > 0)
		{
			_replicateStartTick = _networkObjectCache.TimeManager.LocalTick + predictionManager.StateInterpolation;
		}
	}

	private void InsertIntoReplicateHistory<T>(ReplicateDataContainer<T> dataContainer, RingBuffer<ReplicateDataContainer<T>> replicatesHistory) where T : IReplicateData, new()
	{
		ReplicateTickFinder.DataPlacementResult findResult;
		int replicateHistoryIndex = ReplicateTickFinder.GetReplicateHistoryIndex(dataContainer.Data.GetTick(), replicatesHistory, out findResult);
		switch (findResult)
		{
		case ReplicateTickFinder.DataPlacementResult.Exact:
			replicatesHistory[replicateHistoryIndex].Dispose();
			replicatesHistory[replicateHistoryIndex] = dataContainer;
			break;
		case ReplicateTickFinder.DataPlacementResult.InsertMiddle:
			InsertReplicatesHistory(replicatesHistory, dataContainer, replicateHistoryIndex);
			break;
		case ReplicateTickFinder.DataPlacementResult.InsertEnd:
			AddReplicatesHistory(replicatesHistory, dataContainer);
			break;
		}
		if (findResult == ReplicateTickFinder.DataPlacementResult.InsertBeginning)
		{
			InsertReplicatesHistory(replicatesHistory, dataContainer, 0);
		}
	}

	private void AddReplicatesHistory<T>(RingBuffer<ReplicateDataContainer<T>> replicatesHistory, ReplicateDataContainer<T> value) where T : IReplicateData, new()
	{
		ReplicateDataContainer<T> replicateDataContainer = replicatesHistory.Add(value);
		if (replicateDataContainer.Data != null)
		{
			replicateDataContainer.Dispose();
		}
	}

	private void InsertReplicatesHistory<T>(RingBuffer<ReplicateDataContainer<T>> replicatesHistory, ReplicateDataContainer<T> value, int index) where T : IReplicateData, new()
	{
		ReplicateDataContainer<T> replicateDataContainer = replicatesHistory.Insert(index, value);
		if (replicateDataContainer.Data != null)
		{
			replicateDataContainer.Dispose();
		}
	}

	public virtual void CreateReconcile()
	{
	}

	[MakePublic]
	public void Reconcile_Server<T>(uint methodHash, ref T lastReconcileData, T data, Channel channel) where T : IReconcileData
	{
		if (IsServerInitialized)
		{
			Server_SendReconcileRpc(methodHash, ref lastReconcileData, data, channel);
		}
	}

	[MakePublic]
	public virtual void Reconcile_Client_Start()
	{
	}

	[MakePublic]
	public void Reconcile_Client_AddToLocalHistory<T>(RingBuffer<LocalReconcile<T>> reconcilesHistory, T data) where T : IReconcileData
	{
		if (!_networkObjectCache.IsServerStarted && _networkObjectCache.PredictionManager.CreateLocalStates)
		{
			uint createReconcileTick = _networkObjectCache.PredictionManager.GetCreateReconcileTick(_networkObjectCache.IsOwner);
			if (createReconcileTick != 0)
			{
				data.SetTick(createReconcileTick);
				LocalReconcile<T> data2 = default(LocalReconcile<T>);
				data2.Initialize(createReconcileTick, data);
				reconcilesHistory.Add(data2);
			}
		}
	}

	[MakePublic]
	public void Reconcile_Current<T>(uint hash, ref T lastReconcileData, RingBuffer<LocalReconcile<T>> reconcilesHistory, T data, Channel channel) where T : IReconcileData, new()
	{
		if (!_networkObjectCache.PredictionManager.IsReconciling)
		{
			if (_networkObjectCache.IsServerInitialized)
			{
				Reconcile_Server(hash, ref lastReconcileData, data, channel);
			}
			else
			{
				Reconcile_Client_AddToLocalHistory(reconcilesHistory, data);
			}
		}
	}

	[MakePublic]
	public void Reconcile_Client<T, T2>(ReconcileUserLogicDelegate<T> reconcileDel, RingBuffer<ReplicateDataContainer<T2>> replicatesHistory, RingBuffer<LocalReconcile<T>> reconcilesHistory, T data) where T : IReconcileData where T2 : IReplicateData, new()
	{
		bool isBehaviourReconciling = IsBehaviourReconciling;
		long num = -1L;
		if (reconcilesHistory.Count > 0)
		{
			uint num2 = (isBehaviourReconciling ? data.GetTick() : _networkObjectCache.PredictionManager.GetReconcileStateTick(_networkObjectCache.IsOwner));
			uint tick = reconcilesHistory[0].Tick;
			num = (long)num2 - (long)tick;
			if (!IsHistoryIndexValid((int)num))
			{
				num = -1L;
				ClearReconcileHistory(reconcilesHistory);
			}
			else
			{
				uint tick2 = reconcilesHistory[(int)num].Tick;
				if (tick2 != num2)
				{
					long num3 = (long)num2 - (long)tick2;
					num += num3;
					if (!IsHistoryIndexValid((int)num))
					{
						ClearReconcileHistory(reconcilesHistory);
						num = -1L;
					}
				}
				if (!isBehaviourReconciling && num != -1)
				{
					LocalReconcile<T> localReconcile = reconcilesHistory[(int)num];
					PooledReader reader = ReaderPool.Retrieve(localReconcile.Writer.GetArraySegment(), _networkObjectCache.NetworkManager, Reader.DataSource.Server);
					data = Reconcile_Reader_Local<T>(localReconcile.Tick, reader);
					ReaderPool.Store(reader);
				}
			}
		}
		if (num != -1)
		{
			int num4 = (int)num;
			for (int i = 0; i < num4; i++)
			{
				reconcilesHistory[i].Dispose();
			}
			reconcilesHistory.RemoveRange(fromStart: true, (int)num);
		}
		if (!IsBehaviourReconciling)
		{
			return;
		}
		_networkObjectCache.IsObjectReconciling = true;
		uint num5 = (_lastReconcileTick = data.GetTick());
		if (replicatesHistory.Count > 0)
		{
			int num6 = 0;
			if (replicatesHistory.Count > 0)
			{
				if (replicatesHistory[replicatesHistory.Count - 1].Data.GetTick() <= num5)
				{
					num6 = replicatesHistory.Count;
				}
				else
				{
					for (int j = 0; j < replicatesHistory.Count; j++)
					{
						if (replicatesHistory[j].Data.GetTick() > num5)
						{
							num6 = j;
							break;
						}
					}
				}
			}
			for (int k = 0; k < num6; k++)
			{
				replicatesHistory[k].Dispose();
			}
			replicatesHistory.RemoveRange(fromStart: true, num6);
		}
		reconcileDel?.Invoke(data, Channel.Reliable);
		bool IsHistoryIndexValid(int index)
		{
			if (index >= 0)
			{
				return index < reconcilesHistory.Count;
			}
			return false;
		}
	}

	internal void Reconcile_Client_End()
	{
		IsBehaviourReconciling = false;
	}

	private void ClearReconcileHistory<T>(RingBuffer<LocalReconcile<T>> reconcilesHistory) where T : IReconcileData
	{
		foreach (LocalReconcile<T> item in reconcilesHistory)
		{
			item.Dispose();
		}
		reconcilesHistory.Clear();
	}

	public void Reconcile_Reader<T>(PooledReader reader, ref T lastReconcileData) where T : IReconcileData
	{
		uint num = (IsOwner ? PredictionManager.ClientStateTick : PredictionManager.ServerStateTick);
		T val = reader.ReadReconcile<T>();
		if (num >= _lastReadReconcileRemoteTick)
		{
			lastReconcileData = val;
			lastReconcileData.SetTick(num);
			IsBehaviourReconciling = true;
			_networkObjectCache.IsObjectReconciling = true;
			_lastReadReconcileRemoteTick = num;
		}
	}

	public T Reconcile_Reader_Local<T>(uint tick, PooledReader reader) where T : IReconcileData
	{
		reader.NetworkManager = _networkObjectCache.NetworkManager;
		T result = reader.ReadReconcile<T>();
		result.SetTick(tick);
		IsBehaviourReconciling = true;
		return result;
	}

	private void SetReplicateTick(uint value, bool createdReplicate)
	{
		_lastOrderedReplicatedTick = value;
		_networkObjectCache.SetReplicateTick(value, createdReplicate);
	}

	public bool GetIsNetworked()
	{
		return _networkObjectCache.GetIsNetworked();
	}

	public void SetIsNetworked(bool value)
	{
		_networkObjectCache.SetIsNetworked(value);
	}

	public bool OwnerMatches(NetworkConnection connection)
	{
		return _networkObjectCache.Owner == connection;
	}

	public void Despawn(GameObject go, DespawnType? despawnType = null)
	{
		if (!IsNetworkObjectNull(warn: true))
		{
			_networkObjectCache.Despawn(go, despawnType);
		}
	}

	public void Despawn(NetworkObject nob, DespawnType? despawnType = null)
	{
		if (!IsNetworkObjectNull(warn: true))
		{
			_networkObjectCache.Despawn(nob, despawnType);
		}
	}

	public void Despawn(DespawnType? despawnType = null)
	{
		if (!IsNetworkObjectNull(warn: true))
		{
			_networkObjectCache.Despawn(despawnType);
		}
	}

	public void Spawn(GameObject go, NetworkConnection ownerConnection = null, Scene scene = default(Scene))
	{
		if (!IsNetworkObjectNull(warn: true))
		{
			_networkObjectCache.Spawn(go, ownerConnection, scene);
		}
	}

	public void Spawn(NetworkObject nob, NetworkConnection ownerConnection = null, Scene scene = default(Scene))
	{
		if (!IsNetworkObjectNull(warn: true))
		{
			_networkObjectCache.Spawn(nob, ownerConnection, scene);
		}
	}

	private bool IsNetworkObjectNull(bool warn)
	{
		bool num = _networkObjectCache == null;
		if (num && warn)
		{
			NetworkManager.LogWarning("NetworkObject is null. This can occur if this object is not spawned, or initialized yet.");
		}
		return num;
	}

	public void RemoveOwnership()
	{
		_networkObjectCache.RemoveOwnership();
	}

	public void GiveOwnership(NetworkConnection newOwner)
	{
		_networkObjectCache.GiveOwnership(newOwner, asServer: true, recursive: false);
	}

	public void GiveOwnership(NetworkConnection newOwner, bool includeNested)
	{
		_networkObjectCache.GiveOwnership(newOwner, asServer: true, includeNested);
	}

	public void RegisterInvokeOnInstance<T>(Action<UnityEngine.Component> handler) where T : UnityEngine.Component
	{
		_networkObjectCache.RegisterInvokeOnInstance<T>(handler);
	}

	public void UnregisterInvokeOnInstance<T>(Action<UnityEngine.Component> handler) where T : UnityEngine.Component
	{
		_networkObjectCache.UnregisterInvokeOnInstance<T>(handler);
	}

	public T GetInstance<T>() where T : UnityEngine.Component
	{
		return _networkObjectCache.GetInstance<T>();
	}

	public void RegisterInstance<T>(T component, bool replace = true) where T : UnityEngine.Component
	{
		_networkObjectCache.RegisterInstance(component, replace);
	}

	public bool TryRegisterInstance<T>(T component) where T : UnityEngine.Component
	{
		return _networkObjectCache.TryRegisterInstance(component);
	}

	public void UnregisterInstance<T>() where T : UnityEngine.Component
	{
		_networkObjectCache.UnregisterInstance<T>();
	}

	private void InitializeRpcLinks()
	{
		ServerManager serverManager = NetworkManager.ServerManager;
		if (_observersRpcDelegates != null)
		{
			foreach (uint key in _observersRpcDelegates.Keys)
			{
				if (!MakeLink(key, PacketId.ObserversRpc))
				{
					return;
				}
			}
		}
		if (_targetRpcDelegates != null)
		{
			foreach (uint key2 in _targetRpcDelegates.Keys)
			{
				if (!MakeLink(key2, PacketId.TargetRpc))
				{
					return;
				}
			}
		}
		if (_reconcileRpcDelegates == null)
		{
			return;
		}
		foreach (uint key3 in _reconcileRpcDelegates.Keys)
		{
			if (!MakeLink(key3, PacketId.Reconcile))
			{
				break;
			}
		}
		bool MakeLink(uint rpcHash, PacketId packetId)
		{
			if (serverManager.GetRpcLink(out var value))
			{
				_rpcLinks[rpcHash] = new RpcLinkType(rpcHash, packetId, value);
				return true;
			}
			return false;
		}
	}

	private int GetEstimatedRpcHeaderLength()
	{
		return 20;
	}

	private PooledWriter CreateLinkedRpc(RpcLinkType link, PooledWriter methodWriter, Channel channel)
	{
		int estimatedRpcHeaderLength = GetEstimatedRpcHeaderLength();
		int length = methodWriter.Length;
		PooledWriter pooledWriter = WriterPool.Retrieve(estimatedRpcHeaderLength + length);
		pooledWriter.WriteUInt16(link.LinkPacketId);
		if (channel == Channel.Reliable)
		{
			pooledWriter.WriteInt32(methodWriter.Length);
		}
		pooledWriter.WriteArraySegment(methodWriter.GetArraySegment());
		return pooledWriter;
	}

	private void ReturnRpcLinks()
	{
		if (_rpcLinks.Count != 0)
		{
			ServerManager?.StoreRpcLinks(_rpcLinks);
			_rpcLinks.Clear();
		}
	}

	internal void WriteRpcLinks(Writer writer)
	{
		int count = _rpcLinks.Count;
		if (count == 0)
		{
			return;
		}
		writer.WriteNetworkBehaviourId(this);
		writer.WriteUInt16((ushort)count);
		foreach (KeyValuePair<uint, RpcLinkType> rpcLink in _rpcLinks)
		{
			writer.WriteUInt16Unpacked(rpcLink.Value.LinkPacketId);
			writer.WriteUInt16Unpacked((ushort)rpcLink.Key);
			writer.WriteUInt16Unpacked((ushort)rpcLink.Value.RpcPacketId);
		}
	}

	internal void SendBufferedRpcs(NetworkConnection conn)
	{
		TransportManager transportManager = _networkObjectCache.NetworkManager.TransportManager;
		foreach (BufferedRpc value in _bufferedRpcs.Values)
		{
			transportManager.SendToClient(0, value.Writer.GetArraySegment(), conn, splitLargeMessages: true, value.OrderType);
		}
	}

	[APIExclude]
	[MakePublic]
	public void RegisterServerRpc(uint hash, ServerRpcDelegate del)
	{
		AddRpcName(PacketId.ServerRpc, hash, del.Method.Name);
		if (_serverRpcDelegates.TryAdd(hash, del))
		{
			IncreaseRpcMethodCount();
		}
		else
		{
			NetworkManager.LogError($"ServerRpc key {hash} has already been added for {GetType().FullName} on {base.gameObject.name}");
		}
	}

	[APIExclude]
	[MakePublic]
	public void RegisterObserversRpc(uint hash, ClientRpcDelegate del)
	{
		AddRpcName(PacketId.ObserversRpc, hash, del.Method.Name);
		if (_observersRpcDelegates.TryAdd(hash, del))
		{
			IncreaseRpcMethodCount();
		}
		else
		{
			NetworkManager.LogError($"ObserversRpc key {hash} has already been added for {GetType().FullName} on {base.gameObject.name}");
		}
	}

	[APIExclude]
	[MakePublic]
	public void RegisterTargetRpc(uint hash, ClientRpcDelegate del)
	{
		AddRpcName(PacketId.TargetRpc, hash, del.Method.Name);
		if (_targetRpcDelegates.TryAdd(hash, del))
		{
			IncreaseRpcMethodCount();
		}
		else
		{
			NetworkManager.LogError($"TargetRpc key {hash} has already been added for {GetType().FullName} on {base.gameObject.name}");
		}
	}

	private void AddRpcName(PacketId packetId, uint hash, string methodName)
	{
	}

	private string GetRpcName(PacketId packetId, uint hash)
	{
		return string.Empty;
	}

	private void IncreaseRpcMethodCount()
	{
		_rpcMethodCount++;
		if (_rpcMethodCount <= 255)
		{
			_rpcHashSize = 1;
		}
		else
		{
			_rpcHashSize = 2;
		}
	}

	public void ClearBuffedRpcs()
	{
		foreach (BufferedRpc value in _bufferedRpcs.Values)
		{
			value.Writer.Store();
		}
		_bufferedRpcs.Clear();
	}

	private uint ReadRpcHash(PooledReader reader)
	{
		if (_rpcHashSize == 1)
		{
			return reader.ReadUInt8Unpacked();
		}
		return reader.ReadUInt16();
	}

	internal void ReadServerRpc(int readerPositionAfterDebug, bool fromRpcLink, uint hash, PooledReader reader, NetworkConnection sendingClient, Channel channel)
	{
		if (!fromRpcLink)
		{
			hash = ReadRpcHash(reader);
		}
		if (sendingClient == null)
		{
			_networkObjectCache.NetworkManager.LogError($"NetworkConnection is null. ServerRpc {hash} on object {base.gameObject.name} [id {ObjectId}] will not complete. Remainder of packet may become corrupt.");
			return;
		}
		if (_serverRpcDelegates.TryGetValueIL2CPP(hash, out var value))
		{
			try
			{
				value(reader, channel, sendingClient);
			}
			catch (Exception arg)
			{
				NetworkManager.LogError("ServerRpc failed. Behaviour=" + GetType().Name + ", " + $"Method={value.Method.Name}, Hash={hash}, " + $"Object={base.gameObject.name}, Connection={sendingClient.ClientId}\n{arg}");
				throw;
			}
		}
		else
		{
			_networkObjectCache.NetworkManager.LogError($"ServerRpc not found for hash {hash} on object {base.gameObject.name} [id {ObjectId}]. Remainder of packet may become corrupt.");
		}
		if (_networkTrafficStatistics != null)
		{
			_networkTrafficStatistics.AddInboundPacketIdData(PacketId.ServerRpc, GetRpcName(PacketId.ServerRpc, hash), reader.Position - readerPositionAfterDebug + 2, base.gameObject, asServer: true);
		}
	}

	internal void ReadObserversRpc(int readerPositionAfterDebug, bool fromRpcLink, uint hash, PooledReader reader, Channel channel)
	{
		if (!fromRpcLink)
		{
			hash = ReadRpcHash(reader);
		}
		if (_observersRpcDelegates.TryGetValueIL2CPP(hash, out var value))
		{
			value(reader, channel);
		}
		else
		{
			_networkObjectCache.NetworkManager.LogError($"ObserversRpc not found for hash {hash} on object {base.gameObject.name} [id {ObjectId}] . Remainder of packet may become corrupt.");
		}
		if (_networkTrafficStatistics != null)
		{
			_networkTrafficStatistics.AddInboundPacketIdData(PacketId.ObserversRpc, GetRpcName(PacketId.ObserversRpc, hash), reader.Position - readerPositionAfterDebug + 2, base.gameObject, asServer: false);
		}
	}

	internal void ReadTargetRpc(int readerPositionAfterDebug, bool fromRpcLink, uint hash, PooledReader reader, Channel channel)
	{
		if (!fromRpcLink)
		{
			hash = ReadRpcHash(reader);
		}
		if (_targetRpcDelegates.TryGetValueIL2CPP(hash, out var value))
		{
			value(reader, channel);
		}
		else
		{
			_networkObjectCache.NetworkManager.LogError($"TargetRpc not found for hash {hash} on object {base.gameObject.name} [id {ObjectId}] . Remainder of packet may become corrupt.");
		}
		if (_networkTrafficStatistics != null)
		{
			_networkTrafficStatistics.AddInboundPacketIdData(PacketId.TargetRpc, GetRpcName(PacketId.TargetRpc, hash), reader.Position - readerPositionAfterDebug + 2, base.gameObject, asServer: false);
		}
	}

	[MakePublic]
	public void SendServerRpc(uint hash, PooledWriter methodWriter, Channel channel, DataOrderType orderType)
	{
		if (IsSpawnedWithWarning())
		{
			_transportManagerCache.CheckSetReliableChannel(methodWriter.Length + 10, ref channel);
			PooledWriter pooledWriter = CreateRpc(hash, methodWriter, PacketId.ServerRpc, channel);
			_networkObjectCache.NetworkManager.TransportManager.SendToServer((byte)channel, pooledWriter.GetArraySegment(), splitLargeMessages: true, orderType);
			pooledWriter.StoreLength();
		}
	}

	[APIExclude]
	[MakePublic]
	public void SendObserversRpc(uint hash, PooledWriter methodWriter, Channel channel, DataOrderType orderType, bool bufferLast, bool excludeServer, bool excludeOwner)
	{
		if (!IsSpawnedWithWarning())
		{
			return;
		}
		_transportManagerCache.CheckSetReliableChannel(methodWriter.Length + 10, ref channel);
		PooledWriter writer = lCreateRpc(channel);
		SetNetworkConnectionCache(excludeServer, excludeOwner);
		_networkObjectCache.NetworkManager.TransportManager.SendToClients((byte)channel, writer.GetArraySegment(), _networkObjectCache.Observers, _networkConnectionCache, splitLargeMessages: true, orderType);
		if (bufferLast)
		{
			if (_bufferedRpcs.TryGetValueIL2CPP(hash, out var value))
			{
				value.Writer.StoreLength();
			}
			if (channel == Channel.Unreliable)
			{
				writer.StoreLength();
				writer = lCreateRpc(Channel.Reliable);
			}
			_bufferedRpcs[hash] = new BufferedRpc(writer, orderType);
		}
		else
		{
			writer.StoreLength();
		}
		PooledWriter lCreateRpc(Channel c)
		{
			if (_rpcLinks.TryGetValueIL2CPP(hash, out var value2))
			{
				writer = CreateLinkedRpc(value2, methodWriter, c);
			}
			else
			{
				writer = CreateRpc(hash, methodWriter, PacketId.ObserversRpc, c);
			}
			return writer;
		}
	}

	[MakePublic]
	public void SendTargetRpc(uint hash, PooledWriter methodWriter, Channel channel, DataOrderType orderType, NetworkConnection target, bool excludeServer, bool validateTarget = true)
	{
		if (!IsSpawnedWithWarning())
		{
			return;
		}
		_transportManagerCache.CheckSetReliableChannel(methodWriter.Length + 10, ref channel);
		if (validateTarget)
		{
			if (target == null)
			{
				_networkObjectCache.NetworkManager.LogWarning("Action cannot be completed as no Target is specified.");
				return;
			}
			if (!_networkObjectCache.Observers.Contains(target))
			{
				_networkObjectCache.NetworkManager.LogWarning($"Action cannot be completed as Target is not an observer for object {base.gameObject.name} [id {ObjectId}].");
				return;
			}
		}
		if (!excludeServer || !target.IsLocalClient)
		{
			RpcLinkType value;
			PooledWriter pooledWriter = ((!_rpcLinks.TryGetValueIL2CPP(hash, out value)) ? CreateRpc(hash, methodWriter, PacketId.TargetRpc, channel) : CreateLinkedRpc(value, methodWriter, channel));
			_networkObjectCache.NetworkManager.TransportManager.SendToClient((byte)channel, pooledWriter.GetArraySegment(), target, splitLargeMessages: true, orderType);
			pooledWriter.Store();
		}
	}

	private void SetNetworkConnectionCache(bool addClientHost, bool addOwner)
	{
		_networkConnectionCache.Clear();
		if (addClientHost && IsClientStarted)
		{
			_networkConnectionCache.Add(LocalConnection);
		}
		if (addOwner && Owner.IsValid)
		{
			_networkConnectionCache.Add(Owner);
		}
	}

	private bool IsSpawnedWithWarning()
	{
		bool isSpawned = IsSpawned;
		if (!isSpawned)
		{
			_networkObjectCache.NetworkManager.LogWarning($"Action cannot be completed as object {base.gameObject.name} [Id {ObjectId}] is not spawned.");
		}
		return isSpawned;
	}

	private PooledWriter CreateRpc(uint hash, PooledWriter methodWriter, PacketId packetId, Channel channel)
	{
		int estimatedRpcHeaderLength = GetEstimatedRpcHeaderLength();
		int length = methodWriter.Length;
		PooledWriter pooledWriter = WriterPool.Retrieve(estimatedRpcHeaderLength + length);
		pooledWriter.WritePacketIdUnpacked(packetId);
		pooledWriter.WriteNetworkBehaviour(this);
		if (channel == Channel.Reliable)
		{
			pooledWriter.WriteInt32(length + _rpcHashSize);
		}
		WriteRpcHash(hash, pooledWriter);
		pooledWriter.WriteArraySegment(methodWriter.GetArraySegment());
		return pooledWriter;
	}

	private void WriteRpcHash(uint hash, PooledWriter writer)
	{
		if (_rpcHashSize == 1)
		{
			writer.WriteUInt8Unpacked((byte)hash);
		}
		else
		{
			writer.WriteUInt16((byte)hash);
		}
	}

	internal void RegisterSyncType(SyncBase sb, uint index)
	{
		if (_syncTypes == null)
		{
			_syncTypes = CollectionCaches<uint, SyncBase>.RetrieveDictionary();
		}
		if (!_syncTypes.TryAdd(index, sb))
		{
			NetworkManager.LogError($"SyncType key {index} has already been added for {GetType().FullName} on {base.gameObject.name}");
		}
	}

	internal bool DirtySyncType()
	{
		if (!IsServerStarted)
		{
			return false;
		}
		if (_networkObjectCache.Observers.Count == 0 && !_networkObjectCache.PredictedSpawner.IsValid)
		{
			return false;
		}
		if (!SyncTypeDirty)
		{
			_networkObjectCache.NetworkManager.ServerManager.Objects.SetDirtySyncType(this);
		}
		SyncTypeDirty = true;
		return true;
	}

	private void SyncTypes_Preinitialize(bool asServer)
	{
		if (_networkObjectCache.DoubleLogic(asServer))
		{
			return;
		}
		if (_syncTypeWriters.Count == 0)
		{
			List<ReadPermission> list = new List<ReadPermission>();
			foreach (ReadPermission value2 in Enum.GetValues(typeof(ReadPermission)))
			{
				list.Add(value2);
			}
			foreach (ReadPermission item in list)
			{
				SyncTypeWriter value = default(SyncTypeWriter);
				value.Initialize();
				_syncTypeWriters[item] = value;
			}
		}
		foreach (SyncBase value3 in _syncTypes.Values)
		{
			value3.PreInitialize(_networkObjectCache.NetworkManager, asServer);
		}
	}

	internal void ReadSyncType(int readerPositionAfterDebug, PooledReader reader, int writtenLength, bool asServer = false)
	{
		int num = reader.Position + writtenLength;
		while (reader.Position < num)
		{
			byte b = reader.ReadUInt8Unpacked();
			if (_syncTypes.TryGetValueIL2CPP(b, out var value))
			{
				value.Read(reader, asServer);
			}
			else
			{
				NetworkManager.LogError($"SyncType not found for index {b} on {base.transform.name}, component {GetType().FullName}. The remainder of the packet will become corrupt likely resulting in unforeseen issues for this tick, such as data missing or objects not spawning.");
			}
		}
		if (reader.Position > num)
		{
			NetworkManager.LogError("Remaining bytes in SyncType reader are less than expected. Something did not serialize or deserialize properly which will likely result in a SyncType being incorrect.");
			reader.Position = num;
		}
	}

	internal bool WriteDirtySyncTypes(SyncTypeWriteFlag flags)
	{
		if (!IsSpawned)
		{
			ResetState_SyncTypes(asServer: true);
			return true;
		}
		if (!SyncTypeDirty || _syncTypes.Count == 0)
		{
			return true;
		}
		int num = 0;
		int num2 = 0;
		bool flag = flags.FastContains(SyncTypeWriteFlag.IgnoreInterval);
		bool flag2 = flags.FastContains(SyncTypeWriteFlag.ForceReliable);
		uint tick = _networkObjectCache.NetworkManager.TimeManager.Tick;
		bool isActive = _networkObjectCache.Owner.IsActive;
		foreach (SyncTypeWriter value3 in _syncTypeWriters.Values)
		{
			value3.Reset();
		}
		HashSet<ReadPermission> hashSet = CollectionCaches<ReadPermission>.RetrieveHashSet();
		foreach (SyncBase value4 in _syncTypes.Values)
		{
			if (!value4.IsDirty)
			{
				continue;
			}
			num++;
			if (!flag && !value4.IsNextSyncTimeMet(tick))
			{
				continue;
			}
			value4.ResetDirty();
			ReadPermission readPermission = value4.Settings.ReadPermission;
			if (!isActive && readPermission == ReadPermission.OwnerOnly)
			{
				continue;
			}
			num2++;
			if (flag2)
			{
				value4.SetCurrentChannel(Channel.Reliable);
			}
			byte b = (byte)value4.Channel;
			if (_syncTypeWriters.TryGetValueIL2CPP(readPermission, out var value))
			{
				if (b >= 2)
				{
					b = 0;
				}
				hashSet.Add(readPermission);
				value4.WriteDelta(value.Writers[b]);
			}
		}
		if (num == 0)
		{
			SyncTypeDirty = false;
			CollectionCaches<ReadPermission>.Store(hashSet);
			return true;
		}
		if (hashSet.Count == 0)
		{
			CollectionCaches<ReadPermission>.Store(hashSet);
			return false;
		}
		PooledWriter pooledWriter = WriterPool.Retrieve();
		TransportManager transportManager = _networkObjectCache.NetworkManager.TransportManager;
		foreach (ReadPermission item in hashSet)
		{
			if (!_syncTypeWriters.TryGetValueIL2CPP(item, out var value2))
			{
				continue;
			}
			for (int i = 0; i < value2.Writers.Count; i++)
			{
				PooledWriter pooledWriter2 = value2.Writers[i];
				if (pooledWriter2.Length == 0)
				{
					continue;
				}
				CompleteSyncTypePacket(pooledWriter, pooledWriter2);
				pooledWriter2.Clear();
				if (pooledWriter.Length == 0)
				{
					continue;
				}
				byte channelId = (byte)i;
				switch (item)
				{
				case ReadPermission.Observers:
					transportManager.SendToClients(channelId, pooledWriter.GetArraySegment(), _networkObjectCache.Observers);
					break;
				case ReadPermission.ExcludeOwner:
					_networkConnectionCache.Clear();
					if (isActive)
					{
						_networkConnectionCache.Add(_networkObjectCache.Owner);
					}
					transportManager.SendToClients(channelId, pooledWriter.GetArraySegment(), _networkObjectCache.Observers, _networkConnectionCache);
					break;
				case ReadPermission.OwnerOnly:
					transportManager.SendToClient(channelId, pooledWriter.GetArraySegment(), _networkObjectCache.Owner);
					break;
				}
				pooledWriter.Clear();
			}
		}
		pooledWriter.Store();
		CollectionCaches<ReadPermission>.Store(hashSet);
		bool num3 = num == num2;
		if (num3)
		{
			SyncTypeDirty = false;
		}
		return num3;
	}

	internal void WriteSyncTypesForConnection(NetworkConnection conn, ReadPermission readPermissions)
	{
		if (_syncTypes.Count == 0 || !_syncTypeWriters.TryGetValueIL2CPP(readPermissions, out var value))
		{
			return;
		}
		value.Reset();
		PooledWriter pooledWriter = WriterPool.Retrieve();
		foreach (SyncBase value2 in _syncTypes.Values)
		{
			if (value2.Settings.ReadPermission == readPermissions)
			{
				PooledWriter writer = value.Writers[(int)value2.Settings.Channel];
				value2.WriteFull(writer);
			}
		}
		for (int i = 0; i < value.Writers.Count; i++)
		{
			PooledWriter pooledWriter2 = value.Writers[i];
			CompleteSyncTypePacket(pooledWriter, pooledWriter2);
			pooledWriter2.Clear();
			byte channelId = 0;
			_networkObjectCache.NetworkManager.TransportManager.SendToClient(channelId, pooledWriter.GetArraySegment(), conn);
		}
		pooledWriter.Store();
	}

	private void CompleteSyncTypePacket(PooledWriter fullWriter, PooledWriter syncTypeWriter)
	{
		if (syncTypeWriter.Length != 0)
		{
			fullWriter.Clear();
			fullWriter.WritePacketIdUnpacked(PacketId.SyncType);
			fullWriter.WriteNetworkBehaviour(this);
			ReservedLengthWriter reservedLengthWriter = ReservedWritersExtensions.Retrieve();
			reservedLengthWriter.Initialize(fullWriter, 4);
			fullWriter.WriteArraySegment(syncTypeWriter.GetArraySegment());
			reservedLengthWriter.WriteLength();
			reservedLengthWriter.Store();
		}
	}

	internal void WriteSyncTypesForSpawn(PooledWriter writer, NetworkConnection conn)
	{
		if (_syncTypes.Count == 0)
		{
			return;
		}
		bool flag = conn == _networkObjectCache.Owner;
		writer.Skip(2);
		int position = writer.Position;
		byte b = 0;
		foreach (SyncBase value in _syncTypes.Values)
		{
			ReadPermission readPermission = value.Settings.ReadPermission;
			if (readPermission == ReadPermission.Observers || (readPermission == ReadPermission.ExcludeOwner && !flag) || (readPermission == ReadPermission.OwnerOnly && flag))
			{
				int position2 = writer.Position;
				value.WriteFull(writer);
				if (writer.Position != position2)
				{
					b++;
				}
			}
		}
		if (position != writer.Position)
		{
			int index = position - 2;
			writer.InsertUInt8Unpacked(ComponentIndex, index++);
			writer.InsertUInt8Unpacked(b, index);
		}
		else
		{
			writer.Remove(2);
		}
	}

	internal void ReadSyncTypesForSpawn(PooledReader reader)
	{
		byte b = reader.ReadUInt8Unpacked();
		for (int i = 0; i < b; i++)
		{
			byte b2 = reader.ReadUInt8Unpacked();
			if (_syncTypes.TryGetValueIL2CPP(b2, out var value))
			{
				value.Read(reader, asServer: false);
			}
			else
			{
				NetworkManager.LogWarning($"SyncType not found for index {b2} on {base.transform.name}, component {GetType().FullName}. Remainder of packet may become corrupt.");
			}
		}
	}

	internal void ResetState_SyncTypes(bool asServer)
	{
		if (_syncTypes != null)
		{
			foreach (SyncBase value in _syncTypes.Values)
			{
				value.ResetState(asServer);
			}
		}
		if (_syncTypeWriters != null)
		{
			foreach (SyncTypeWriter value2 in _syncTypeWriters.Values)
			{
				value2.Reset();
			}
		}
		if (asServer)
		{
			SyncTypeDirty = false;
		}
	}

	private void SyncTypes_OnDestroy()
	{
		CollectionCaches<uint, SyncBase>.StoreAndDefault(ref _syncTypes);
	}

	public override string ToString()
	{
		return $"Name [{base.gameObject.name}] ComponentId [{ComponentIndex}] NetworkObject Name [{_networkObjectCache.name}] NetworkObject Id [{_networkObjectCache.ObjectId}]";
	}

	[MakePublic]
	public virtual void NetworkInitialize___Early()
	{
	}

	[MakePublic]
	public virtual void NetworkInitialize___Late()
	{
	}

	internal void InitializeEarly(NetworkObject nob, bool asServer)
	{
		_transportManagerCache = nob.TransportManager;
		SyncTypes_Preinitialize(asServer);
		if (asServer)
		{
			InitializeRpcLinks();
			_initializedOnceServer = true;
			return;
		}
		if (!_initializedOnceClient && nob.EnablePrediction && _usesPrediction)
		{
			nob.RegisterPredictionBehaviourOnce(this);
		}
		_initializedOnceClient = true;
	}

	internal void Deinitialize(bool asServer)
	{
		ResetState_SyncTypes(asServer);
	}

	internal void NetworkBehaviour_OnDestroy()
	{
		SyncTypes_OnDestroy();
	}

	internal void SerializeComponents(NetworkObject nob, byte componentIndex)
	{
		_networkObjectCache = nob;
		ComponentIndex = componentIndex;
	}

	internal void InitializeIfDisabled()
	{
		if (!base.gameObject.activeInHierarchy)
		{
			NetworkInitializeIfDisabled();
		}
	}

	[MakePublic]
	[APIExclude]
	public virtual void NetworkInitializeIfDisabled()
	{
	}

	protected virtual void Reset()
	{
	}

	protected virtual void OnValidate()
	{
	}

	public virtual void ResetState(bool asServer)
	{
		ResetState_SyncTypes(asServer);
		ResetState_Prediction(asServer);
		ClearReplicateCache();
		ClearBuffedRpcs();
	}

	private NetworkObject TryAddNetworkObject()
	{
		return null;
	}
}
