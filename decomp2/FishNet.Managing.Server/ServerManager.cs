using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Authenticating;
using FishNet.Broadcast;
using FishNet.Broadcast.Helping;
using FishNet.Component.Observing;
using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Statistic;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Managing.Utility;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Server;

[DisallowMultipleComponent]
[AddComponentMenu("FishNet/Manager/ServerManager")]
public sealed class ServerManager : MonoBehaviour
{
	private readonly Dictionary<ushort, BroadcastHandlerBase> _broadcastHandlers = new Dictionary<ushort, BroadcastHandlerBase>();

	private HashSet<NetworkConnection> _connectionsWithoutExclusionsCache = new HashSet<NetworkConnection>();

	internal Dictionary<ushort, RpcLink> RpcLinks = new Dictionary<ushort, RpcLink>();

	private Queue<ushort> _availableRpcLinkIndexes = new Queue<ushort>();

	[HideInInspector]
	public Dictionary<int, NetworkConnection> Clients = new Dictionary<int, NetworkConnection>();

	private List<NetworkConnection> _clientsList = new List<NetworkConnection>();

	[Tooltip("Authenticator for this ServerManager. May be null if not using authentication.")]
	[SerializeField]
	private Authenticator _authenticator;

	[Tooltip("What platforms to enable remote client timeout.")]
	[SerializeField]
	private RemoteTimeoutType _remoteClientTimeout = RemoteTimeoutType.Development;

	[Tooltip("How long in seconds a client must go without sending any packets before getting disconnected. This is independent of any transport settings.")]
	[Range(1f, 1500f)]
	[SerializeField]
	private ushort _remoteClientTimeoutDuration = 60;

	[Tooltip("True to allow clients to use predicted spawning. While true, each NetworkObject you wish this feature to apply towards must have a PredictedSpawn component. Predicted spawns can have custom validation on the server.")]
	[SerializeField]
	private bool _allowPredictedSpawning;

	[Tooltip("Maximum number of Ids to reserve on clients for predicted spawning. Higher values will allow clients to send more predicted spawns per second but may reduce availability of ObjectIds with high player counts.")]
	[Range(1f, 100f)]
	[SerializeField]
	private ushort _reservedObjectIds = 15;

	[Tooltip("Default send rate for SyncTypes. A value of 0f will send changed values every tick.")]
	[Range(0f, 60f)]
	[SerializeField]
	private float _syncTypeRate = 0.1f;

	[Tooltip("How to pack object spawns.")]
	[SerializeField]
	internal TransformPackingData SpawnPacking = new TransformPackingData
	{
		Position = AutoPackType.Unpacked,
		Rotation = AutoPackType.PackedLess,
		Scale = AutoPackType.PackedLess
	};

	[Tooltip("True to automatically set the frame rate when the client connects.")]
	[SerializeField]
	private bool _changeFrameRate = true;

	[Tooltip("Maximum frame rate the server may run at. When as host this value runs at whichever is higher between client and server.")]
	[Range(1f, 500f)]
	[SerializeField]
	private ushort _frameRate = 500;

	[Tooltip("True to share the Ids of clients and the objects they own with other clients. No sensitive information is shared.")]
	[SerializeField]
	private bool _shareIds = true;

	[Tooltip("True to automatically start the server connection when running as headless.")]
	[SerializeField]
	private bool _startOnHeadless = true;

	private int _nextClientTimeoutCheckIndex;

	private float _nextTimeoutCheckTime;

	private SplitReader _splitReader = new SplitReader();

	private NetworkTrafficStatistics _networkTrafficStatistics;

	public const ushort MAXIMUM_REMOTE_CLIENT_TIMEOUT_DURATION = 1500;

	private const int MAXIMUM_RESERVED_OBJECT_IDS = 100;

	public bool Started { get; private set; }

	public ServerObjects Objects { get; private set; }

	[HideInInspector]
	public NetworkManager NetworkManager { get; private set; }

	internal ushort FrameRate
	{
		get
		{
			if (!_changeFrameRate)
			{
				return 0;
			}
			return _frameRate;
		}
	}

	public bool ShareIds => _shareIds;

	public event Action<NetworkConnection, int, KickReason> OnClientKick;

	public event Action<ServerConnectionStateArgs> OnServerConnectionState;

	public event Action<NetworkConnection, bool> OnAuthenticationResult;

	public event Action<NetworkConnection, RemoteConnectionStateArgs> OnRemoteConnectionState;

	public void RegisterBroadcast<T>(Action<NetworkConnection, T, Channel> handler, bool requireAuthentication = true) where T : struct, IBroadcast
	{
		if (handler == null)
		{
			NetworkManager.LogError("Broadcast cannot be registered because handler is null. This may occur when trying to register to objects which require initialization, such as events.");
			return;
		}
		ushort key = BroadcastExtensions.GetKey<T>();
		if (!_broadcastHandlers.TryGetValueIL2CPP(key, out var value))
		{
			value = new ClientBroadcastHandler<T>(requireAuthentication);
			_broadcastHandlers.Add(key, value);
		}
		value.RegisterHandler(handler);
	}

	public void UnregisterBroadcast<T>(Action<NetworkConnection, T, Channel> handler) where T : struct, IBroadcast
	{
		ushort key = BroadcastExtensions.GetKey<T>();
		if (_broadcastHandlers.TryGetValueIL2CPP(key, out var value))
		{
			value.UnregisterHandler(handler);
		}
	}

	private void ParseBroadcast(PooledReader reader, NetworkConnection conn, Channel channel)
	{
		_ = reader.Position;
		ushort key = reader.ReadUInt16();
		int packetLength = Packets.GetPacketLength(12, reader, channel);
		if (_broadcastHandlers.TryGetValueIL2CPP(key, out var value))
		{
			if (value.RequireAuthentication && !conn.IsAuthenticated)
			{
				conn.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"ConnectionId {conn.ClientId} sent a broadcast which requires authentication, but client was not authenticated. Client has been disconnected.");
			}
			else
			{
				value.InvokeHandlers(conn, reader, channel);
			}
		}
		else
		{
			reader.Skip(packetLength);
		}
	}

	public void Broadcast<T>(NetworkConnection connection, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
	{
		if (!Started)
		{
			NetworkManager.LogWarning("Cannot send broadcast to client because server is not active.");
			return;
		}
		if (requireAuthenticated && !connection.IsAuthenticated)
		{
			NetworkManager.LogWarning("Cannot send broadcast to client because they are not authenticated.");
			return;
		}
		PooledWriter pooledWriter = WriterPool.Retrieve();
		BroadcastsSerializers.WriteBroadcast(NetworkManager, pooledWriter, message, ref channel);
		ArraySegment<byte> arraySegment = pooledWriter.GetArraySegment();
		AddOutboundNetworkTraffic<T>(arraySegment.Count);
		NetworkManager.TransportManager.SendToClient((byte)channel, arraySegment, connection);
		pooledWriter.Store();
	}

	public void Broadcast<T>(HashSet<NetworkConnection> connections, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
	{
		if (!Started)
		{
			NetworkManager.LogWarning("Cannot send broadcast to client because server is not active.");
			return;
		}
		bool flag = false;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		BroadcastsSerializers.WriteBroadcast(NetworkManager, pooledWriter, message, ref channel);
		ArraySegment<byte> arraySegment = pooledWriter.GetArraySegment();
		int num = 0;
		int count = arraySegment.Count;
		foreach (NetworkConnection connection in connections)
		{
			if (requireAuthenticated && !connection.IsAuthenticated)
			{
				flag = true;
				continue;
			}
			NetworkManager.TransportManager.SendToClient((byte)channel, arraySegment, connection);
			num += count;
		}
		pooledWriter.Store();
		AddOutboundNetworkTraffic<T>(num);
		if (flag)
		{
			NetworkManager.LogWarning("One or more broadcast did not send to a client because they were not authenticated.");
		}
	}

	public void BroadcastExcept<T>(HashSet<NetworkConnection> connections, NetworkConnection excludedConnection, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
	{
		if (!Started)
		{
			NetworkManager.LogWarning("Cannot send broadcast to client because server is not active.");
			return;
		}
		if (excludedConnection == null || !excludedConnection.IsValid)
		{
			Broadcast(connections, message, requireAuthenticated, channel);
			return;
		}
		connections.Remove(excludedConnection);
		Broadcast(connections, message, requireAuthenticated, channel);
	}

	public void BroadcastExcept<T>(HashSet<NetworkConnection> connections, HashSet<NetworkConnection> excludedConnections, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
	{
		if (!Started)
		{
			NetworkManager.LogWarning("Cannot send broadcast to client because server is not active.");
			return;
		}
		if (excludedConnections == null || excludedConnections.Count == 0)
		{
			Broadcast(connections, message, requireAuthenticated, channel);
			return;
		}
		foreach (NetworkConnection excludedConnection in excludedConnections)
		{
			connections.Remove(excludedConnection);
		}
		Broadcast(connections, message, requireAuthenticated, channel);
	}

	public void BroadcastExcept<T>(NetworkConnection excludedConnection, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
	{
		if (!Started)
		{
			NetworkManager.LogWarning("Cannot send broadcast to client because server is not active.");
			return;
		}
		if (excludedConnection == null || !excludedConnection.IsValid)
		{
			Broadcast(message, requireAuthenticated, channel);
			return;
		}
		_connectionsWithoutExclusionsCache.Clear();
		foreach (NetworkConnection value in Clients.Values)
		{
			_connectionsWithoutExclusionsCache.Add(value);
		}
		_connectionsWithoutExclusionsCache.Remove(excludedConnection);
		Broadcast(_connectionsWithoutExclusionsCache, message, requireAuthenticated, channel);
	}

	public void BroadcastExcept<T>(HashSet<NetworkConnection> excludedConnections, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
	{
		if (!Started)
		{
			NetworkManager.LogWarning("Cannot send broadcast to client because server is not active.");
			return;
		}
		if (excludedConnections == null || excludedConnections.Count == 0)
		{
			Broadcast(message, requireAuthenticated, channel);
			return;
		}
		_connectionsWithoutExclusionsCache.Clear();
		foreach (NetworkConnection value in Clients.Values)
		{
			_connectionsWithoutExclusionsCache.Add(value);
		}
		foreach (NetworkConnection excludedConnection in excludedConnections)
		{
			_connectionsWithoutExclusionsCache.Remove(excludedConnection);
		}
		Broadcast(_connectionsWithoutExclusionsCache, message, requireAuthenticated, channel);
	}

	public void Broadcast<T>(NetworkObject networkObject, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
	{
		if (networkObject == null)
		{
			NetworkManager.LogWarning("Cannot send broadcast because networkObject is null.");
		}
		else
		{
			Broadcast(networkObject.Observers, message, requireAuthenticated, channel);
		}
	}

	public void Broadcast<T>(T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
	{
		if (!Started)
		{
			NetworkManager.LogWarning("Cannot send broadcast to client because server is not active.");
			return;
		}
		bool flag = false;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		BroadcastsSerializers.WriteBroadcast(NetworkManager, pooledWriter, message, ref channel);
		ArraySegment<byte> arraySegment = pooledWriter.GetArraySegment();
		int num = 0;
		int count = arraySegment.Count;
		foreach (NetworkConnection value in Clients.Values)
		{
			if (requireAuthenticated && !value.IsAuthenticated)
			{
				flag = true;
				continue;
			}
			NetworkManager.TransportManager.SendToClient((byte)channel, arraySegment, value);
			num += count;
		}
		AddOutboundNetworkTraffic<T>(num);
		pooledWriter.Store();
		if (flag)
		{
			NetworkManager.LogWarning("One or more broadcast did not send to a client because they were not authenticated.");
		}
	}

	private void AddOutboundNetworkTraffic<T>(int bytes) where T : struct, IBroadcast
	{
	}

	private bool StoreTransportCacheAndReturn(List<Transport> cache, bool returnedValue)
	{
		CollectionCaches<Transport>.Store(cache);
		return returnedValue;
	}

	public bool AreAllServersStopped()
	{
		List<Transport> allTransports = NetworkManager.TransportManager.GetAllTransports(includeMultipass: false);
		foreach (Transport item in allTransports)
		{
			if (item.GetConnectionState(server: true) != LocalConnectionState.Stopped)
			{
				return StoreTransportCacheAndReturn(allTransports, returnedValue: false);
			}
		}
		return StoreTransportCacheAndReturn(allTransports, returnedValue: true);
	}

	public bool IsOnlyOneServerStarted()
	{
		List<Transport> allTransports = NetworkManager.TransportManager.GetAllTransports(includeMultipass: false);
		int num = 0;
		foreach (Transport item in allTransports)
		{
			if (item.GetConnectionState(server: true) == LocalConnectionState.Started)
			{
				num++;
			}
		}
		return StoreTransportCacheAndReturn(allTransports, num == 1);
	}

	[Obsolete("Use IsOnlyOneServerStarted().")]
	public bool OneServerStarted()
	{
		return IsOnlyOneServerStarted();
	}

	public bool IsAnyServerStarted(Transport excludedTransport)
	{
		List<Transport> allTransports = NetworkManager.TransportManager.GetAllTransports(includeMultipass: false);
		foreach (Transport item in allTransports)
		{
			if (!(item == excludedTransport) && item.GetConnectionState(server: true) == LocalConnectionState.Started)
			{
				return StoreTransportCacheAndReturn(allTransports, returnedValue: true);
			}
		}
		return StoreTransportCacheAndReturn(allTransports, returnedValue: false);
	}

	public bool IsAnyServerStarted(int excludedIndex = -1)
	{
		Transport excludedTransport = null;
		if (excludedIndex != -1 && NetworkManager.TransportManager.Transport is Multipass multipass)
		{
			excludedTransport = multipass.GetTransport(excludedIndex);
		}
		return IsAnyServerStarted(excludedTransport);
	}

	[Obsolete("Use IsAnyServerStarted.")]
	public bool AnyServerStarted(int excludedIndex = -1)
	{
		return IsAnyServerStarted(excludedIndex);
	}

	public void Spawn(GameObject go, NetworkConnection ownerConnection = null, Scene scene = default(Scene))
	{
		if (go == null)
		{
			NetworkManager.LogWarning("GameObject cannot be spawned because it is null.");
			return;
		}
		NetworkObject component = go.GetComponent<NetworkObject>();
		Spawn(component, ownerConnection, scene);
	}

	public void Spawn(NetworkObject nob, NetworkConnection ownerConnection = null, Scene scene = default(Scene))
	{
		if (!nob.GetIsSpawnable())
		{
			NetworkManager.LogWarning($"NetworkObject {nob} cannot be spawned because it is not marked as spawnable.");
		}
		else
		{
			Objects.Spawn(nob, ownerConnection, scene);
		}
	}

	public void Despawn(GameObject go, DespawnType? despawnType = null)
	{
		if (go == null)
		{
			NetworkManager.LogWarning("GameObject cannot be despawned because it is null.");
			return;
		}
		NetworkObject component = go.GetComponent<NetworkObject>();
		Despawn(component, despawnType);
	}

	public void Despawn(NetworkObject networkObject, DespawnType? despawnType = null)
	{
		DespawnType despawnType2 = ((!despawnType.HasValue) ? networkObject.GetDefaultDespawnType() : despawnType.Value);
		Objects.Despawn(networkObject, despawnType2, asServer: true);
	}

	public void Kick(NetworkConnection conn, KickReason kickReason, LoggingType loggingType = LoggingType.Common, string log = "")
	{
		if (conn.IsValid)
		{
			this.OnClientKick?.Invoke(conn, conn.ClientId, kickReason);
			if (conn.IsActive)
			{
				conn.Disconnect(immediately: true);
			}
			if (!string.IsNullOrEmpty(log))
			{
				NetworkManager.Log(loggingType, log);
			}
		}
	}

	public void Kick(int clientId, KickReason kickReason, LoggingType loggingType = LoggingType.Common, string log = "")
	{
		this.OnClientKick?.Invoke(null, clientId, kickReason);
		NetworkManager.TransportManager.Transport.StopConnection(clientId, immediately: true);
		if (!string.IsNullOrEmpty(log))
		{
			NetworkManager.Log(loggingType, log);
		}
	}

	public void Kick(NetworkConnection conn, Reader reader, KickReason kickReason, LoggingType loggingType = LoggingType.Common, string log = "")
	{
		reader.Clear();
		Kick(conn, kickReason, loggingType, log);
	}

	private void InitializeRpcLinks()
	{
		ushort startingRpcLinkIndex = NetworkManager.StartingRpcLinkIndex;
		for (ushort num = ushort.MaxValue; num >= startingRpcLinkIndex; num--)
		{
			_availableRpcLinkIndexes.Enqueue(num);
		}
	}

	internal bool GetRpcLink(out ushort value)
	{
		if (_availableRpcLinkIndexes.Count > 0)
		{
			value = _availableRpcLinkIndexes.Dequeue();
			return true;
		}
		value = 0;
		return false;
	}

	internal void SetRpcLink(ushort linkIndex, RpcLink data)
	{
		RpcLinks[linkIndex] = data;
	}

	internal void StoreRpcLinks(Dictionary<uint, RpcLinkType> links)
	{
		foreach (RpcLinkType value in links.Values)
		{
			_availableRpcLinkIndexes.Enqueue(value.LinkPacketId);
		}
	}

	public Authenticator GetAuthenticator()
	{
		return _authenticator;
	}

	public void SetAuthenticator(Authenticator value)
	{
		_authenticator = value;
		InitializeAuthenticator();
	}

	public void SetRemoteClientTimeout(RemoteTimeoutType timeoutType, ushort duration)
	{
		_remoteClientTimeout = timeoutType;
		duration = (ushort)Mathf.Clamp(duration, 1, 1500);
		_remoteClientTimeoutDuration = duration;
	}

	internal bool GetAllowPredictedSpawning()
	{
		return _allowPredictedSpawning;
	}

	internal ushort GetReservedObjectIds()
	{
		return _reservedObjectIds;
	}

	internal float GetSyncTypeRate()
	{
		return _syncTypeRate;
	}

	public void SetFrameRate(ushort value)
	{
		_frameRate = (ushort)Mathf.Clamp(value, 0, 500);
		_changeFrameRate = true;
		if (NetworkManager != null)
		{
			NetworkManager.UpdateFramerate();
		}
	}

	public bool GetStartOnHeadless()
	{
		return _startOnHeadless;
	}

	public void SetStartOnHeadless(bool value)
	{
		_startOnHeadless = value;
	}

	private void OnDestroy()
	{
		Objects?.SubscribeToSceneLoaded(subscribe: false);
	}

	internal void InitializeOnce_Internal(NetworkManager manager)
	{
		NetworkManager = manager;
		Objects = new ServerObjects(manager);
		Objects.SubscribeToSceneLoaded(subscribe: true);
		InitializeRpcLinks();
		SubscribeToTransport(subscribe: false);
		SubscribeToTransport(subscribe: true);
		NetworkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
		NetworkManager.SceneManager.OnClientLoadedStartScenes += SceneManager_OnClientLoadedStartScenes;
		NetworkManager.TimeManager.OnPostTick += TimeManager_OnPostTick;
		NetworkManager.StatisticsManager.TryGetNetworkTrafficStatistics(out _networkTrafficStatistics);
		if (_authenticator == null)
		{
			_authenticator = GetComponent<Authenticator>();
		}
		if (_authenticator != null)
		{
			InitializeAuthenticator();
		}
	}

	private void InitializeAuthenticator()
	{
		Authenticator authenticator = GetAuthenticator();
		if (!(authenticator == null) && !authenticator.Initialized && !(NetworkManager == null))
		{
			authenticator.InitializeOnce(NetworkManager);
			authenticator.OnAuthenticationResult += _authenticator_OnAuthenticationResult;
		}
	}

	internal void StartForHeadless()
	{
		GetStartOnHeadless();
	}

	public bool StopConnection(bool sendDisconnectMessage)
	{
		if (sendDisconnectMessage)
		{
			SendDisconnectMessages(Clients.Values.ToList(), iterate: true);
		}
		return NetworkManager.TransportManager.Transport.StopConnection(server: true);
	}

	public void SendDisconnectMessages(int[] connectionIds)
	{
		List<NetworkConnection> list = new List<NetworkConnection>();
		foreach (int key in connectionIds)
		{
			if (Clients.TryGetValueIL2CPP(key, out var value))
			{
				list.Add(value);
			}
		}
		if (list.Count > 0)
		{
			SendDisconnectMessages(list, iterate: false);
		}
	}

	public void SendDisconnectMessages(List<NetworkConnection> conns, bool iterate)
	{
		PooledWriter pooledWriter = WriterPool.Retrieve();
		pooledWriter.WritePacketIdUnpacked(PacketId.Disconnect);
		ArraySegment<byte> arraySegment = pooledWriter.GetArraySegment();
		foreach (NetworkConnection conn in conns)
		{
			conn.SendToClient(0, arraySegment);
		}
		pooledWriter.Store();
		if (iterate)
		{
			NetworkManager.TransportManager.IterateOutgoing(asServer: true);
		}
	}

	public bool StartConnection()
	{
		return NetworkManager.TransportManager.Transport.StartConnection(server: true);
	}

	public bool StartConnection(ushort port)
	{
		Transport transport = NetworkManager.TransportManager.Transport;
		transport.SetPort(port);
		return transport.StartConnection(server: true);
	}

	private void CheckClientTimeout()
	{
		if (_remoteClientTimeout == RemoteTimeoutType.Disabled || NetworkManager.SceneManager.IsIteratingQueue(2f))
		{
			return;
		}
		float unscaledTime = Time.unscaledTime;
		if (unscaledTime < _nextTimeoutCheckTime)
		{
			return;
		}
		_nextTimeoutCheckTime = unscaledTime + 0.2f;
		int count = Clients.Count;
		if (count == 0)
		{
			return;
		}
		if (_nextClientTimeoutCheckIndex >= count)
		{
			_nextClientTimeoutCheckIndex = 0;
		}
		uint num = NetworkManager.TimeManager.TimeToTicks((double)(int)_remoteClientTimeoutDuration, TickRounding.RoundUp);
		int num2 = Mathf.CeilToInt(10f);
		int num3 = Mathf.Max(count / num2, 1);
		uint localTick = NetworkManager.TimeManager.LocalTick;
		for (int i = 0; i < num3; i++)
		{
			if (_nextClientTimeoutCheckIndex >= _clientsList.Count)
			{
				_nextClientTimeoutCheckIndex = 0;
			}
			NetworkConnection networkConnection = _clientsList[_nextClientTimeoutCheckIndex];
			uint num4 = networkConnection.PacketTick.LocalTick;
			if (num4 == 0)
			{
				num4 = networkConnection.ServerConnectionTick;
			}
			if (localTick - num4 >= num)
			{
				networkConnection.Kick(KickReason.UnexpectedProblem, LoggingType.Common, networkConnection.ToString() + " has timed out. You can modify this feature on the ServerManager component.");
			}
			_nextClientTimeoutCheckIndex++;
		}
	}

	private void TimeManager_OnPostTick()
	{
		CheckClientTimeout();
	}

	private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
	{
		if (obj.ConnectionState != LocalConnectionState.Started)
		{
			Objects.DestroyPending();
		}
	}

	private void SceneManager_OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
	{
		if (!asServer)
		{
			return;
		}
		Objects.RebuildObservers(conn);
		if (!conn.IsLocalClient)
		{
			return;
		}
		foreach (NetworkObject value in Objects.Spawned.Values)
		{
			if (!value.Observers.Contains(conn))
			{
				value.SetRenderersVisible(visible: false);
			}
		}
	}

	private void SubscribeToTransport(bool subscribe)
	{
		if (!(NetworkManager == null) && !(NetworkManager.TransportManager == null) && !(NetworkManager.TransportManager.Transport == null))
		{
			if (subscribe)
			{
				NetworkManager.TransportManager.Transport.OnServerReceivedData += Transport_OnServerReceivedData;
				NetworkManager.TransportManager.Transport.OnServerConnectionState += Transport_OnServerConnectionState;
				NetworkManager.TransportManager.Transport.OnRemoteConnectionState += Transport_OnRemoteConnectionState;
			}
			else
			{
				NetworkManager.TransportManager.Transport.OnServerReceivedData -= Transport_OnServerReceivedData;
				NetworkManager.TransportManager.Transport.OnServerConnectionState -= Transport_OnServerConnectionState;
				NetworkManager.TransportManager.Transport.OnRemoteConnectionState -= Transport_OnRemoteConnectionState;
			}
		}
	}

	private void _authenticator_OnAuthenticationResult(NetworkConnection conn, bool authenticated)
	{
		if (!authenticated)
		{
			conn.Disconnect(immediately: false);
		}
		else
		{
			ClientAuthenticated(conn);
		}
	}

	private void Transport_OnServerConnectionState(ServerConnectionStateArgs args)
	{
		Started = IsAnyServerStarted();
		NetworkManager.ClientManager.Objects.OnServerConnectionState(args);
		if (!Started)
		{
			MatchCondition.StoreCollections(NetworkManager);
			Objects.DespawnWithoutSynchronization(recursive: true, asServer: true);
			Clients.Clear();
			_clientsList.Clear();
		}
		Objects.OnServerConnectionState(args);
		LocalConnectionState connectionState = args.ConnectionState;
		if (NetworkManager.CanLog(LoggingType.Common))
		{
			Transport transport = NetworkManager.TransportManager.GetTransport(args.TransportIndex);
			string text = ((transport == null) ? "Unknown" : transport.GetType().Name);
			string text2 = string.Empty;
			if (connectionState == LocalConnectionState.Starting)
			{
				text2 = $" Listening on port {transport.GetPort()}.";
			}
			NetworkManagerExtensions.Log("Local server is " + connectionState.ToString().ToLower() + " for " + text + "." + text2);
		}
		NetworkManager.UpdateFramerate();
		this.OnServerConnectionState?.Invoke(args);
	}

	private void ParseVersion(PooledReader reader, NetworkConnection conn, int transportId)
	{
		if (conn.HasSentVersion)
		{
			conn.Kick(reader, KickReason.ExploitAttempt, LoggingType.Common, "Connection " + conn.ToString() + " has sent their FishNet version after being authenticated; this is not possible under normal conditions.");
			return;
		}
		conn.HasSentVersion = true;
		string text = reader.ReadStringAllocated();
		if (text == "4.6.12")
		{
			bool value = false;
			PooledWriter pooledWriter = WriterPool.Retrieve();
			pooledWriter.WritePacketIdUnpacked(PacketId.Version);
			pooledWriter.WriteBoolean(value);
			conn.SendToClient(0, pooledWriter.GetArraySegment());
			WriterPool.Store(pooledWriter);
			Authenticator authenticator = GetAuthenticator();
			if (authenticator != null && !NetworkManager.TransportManager.IsLocalTransport(transportId))
			{
				authenticator.OnRemoteConnection(conn);
			}
			else
			{
				ClientAuthenticated(conn);
			}
		}
		else
		{
			conn.Kick(reader, KickReason.UnexpectedProblem, LoggingType.Warning, "Connection " + conn.ToString() + " has been kicked for being on FishNet version " + text + ". Server version is 4.6.12.");
		}
	}

	private void Transport_OnRemoteConnectionState(RemoteConnectionStateArgs args)
	{
		int connectionId = args.ConnectionId;
		NetworkConnection value;
		if (connectionId < 0 || connectionId > int.MaxValue)
		{
			Kick(args.ConnectionId, KickReason.UnexpectedProblem, LoggingType.Error, $"The transport you are using supplied an invalid connection Id of {connectionId}. Connection Id values must range between 0 and {int.MaxValue}. The client has been disconnected.");
		}
		else if (args.ConnectionState == RemoteConnectionState.Started)
		{
			NetworkManager.Log($"Remote connection started for Id {connectionId}.");
			NetworkConnection networkConnection = new NetworkConnection(NetworkManager, connectionId, args.TransportIndex, asServer: true);
			Clients.Add(args.ConnectionId, networkConnection);
			_clientsList.Add(networkConnection);
			this.OnRemoteConnectionState?.Invoke(networkConnection, args);
		}
		else if (args.ConnectionState == RemoteConnectionState.Stopped && Clients.TryGetValueIL2CPP(connectionId, out value))
		{
			value.SetDisconnecting(value: true);
			this.OnRemoteConnectionState?.Invoke(value, args);
			Clients.Remove(connectionId);
			_clientsList.Remove(value);
			Objects.ClientDisconnected(value);
			BroadcastClientConnectionChange(connected: false, value);
			Queue<int> predictedObjectIds = value.PredictedObjectIds;
			while (predictedObjectIds.Count > 0)
			{
				Objects.CacheObjectId(predictedObjectIds.Dequeue());
			}
			value.ResetState();
			NetworkManager.Log($"Remote connection stopped for Id {connectionId}.");
		}
	}

	private void SendAuthenticated(NetworkConnection conn)
	{
		PooledWriter pooledWriter = WriterPool.Retrieve();
		pooledWriter.WritePacketIdUnpacked(PacketId.Authenticated);
		pooledWriter.WriteNetworkConnection(conn);
		_ = NetworkManager.PredictionManager;
		if (GetAllowPredictedSpawning())
		{
			int num = Mathf.Min(Objects.GetObjectIdCache().Count, GetReservedObjectIds());
			if (num > 100)
			{
				num = 100;
			}
			List<int> list = CollectionCaches<int>.RetrieveList();
			for (int i = 0; i < num; i++)
			{
				if (Objects.GetNextNetworkObjectId(out var nextNetworkObjectId))
				{
					list.Add(nextNetworkObjectId);
				}
			}
			pooledWriter.WriteSignedPackedWhole(list.Count);
			foreach (int item in list)
			{
				pooledWriter.WriteNetworkObjectId(item);
				conn.PredictedObjectIds.Enqueue(item);
			}
			CollectionCaches<int>.Store(list);
		}
		NetworkManager.TransportManager.SendToClient(0, pooledWriter.GetArraySegment(), conn);
		pooledWriter.Store();
	}

	private void Transport_OnServerReceivedData(ServerReceivedDataArgs args)
	{
		ParseReceived(args);
	}

	private void ParseReceived(ServerReceivedDataArgs args)
	{
		if (args.ConnectionId < 0)
		{
			return;
		}
		ArraySegment<byte> segment = ((!NetworkManager.TransportManager.HasIntermediateLayer) ? args.Data : NetworkManager.TransportManager.ProcessIntermediateIncoming(args.Data, fromServer: false));
		if (_networkTrafficStatistics != null)
		{
			_networkTrafficStatistics.AddInboundSocketData((ulong)segment.Count, asServer: true);
		}
		if (segment.Count <= 4)
		{
			return;
		}
		int mTU = NetworkManager.TransportManager.GetMTU(args.TransportIndex, (byte)args.Channel);
		if (segment.Count > mTU)
		{
			ExceededMTUKick();
			return;
		}
		TimeManager timeManager = NetworkManager.TimeManager;
		PacketId packetId = PacketId.Unset;
		PooledReader pooledReader = null;
		try
		{
			Reader.DataSource source = Reader.DataSource.Client;
			pooledReader = ReaderPool.Retrieve(segment, NetworkManager, source);
			uint num = pooledReader.ReadTickUnpacked();
			timeManager.LastPacketTick.Update(num);
			if (pooledReader.PeekPacketId() == PacketId.Split)
			{
				pooledReader.ReadPacketId();
				_splitReader.GetHeader(pooledReader, out var expectedMessages);
				_splitReader.Write(num, pooledReader, expectedMessages);
				ArraySegment<byte> fullMessage = _splitReader.GetFullMessage();
				if (fullMessage.Count == 0)
				{
					return;
				}
				pooledReader.Initialize(fullMessage, NetworkManager, source);
			}
			while (pooledReader.Remaining > 0)
			{
				packetId = pooledReader.ReadPacketId();
				if (!Clients.TryGetValueIL2CPP(args.ConnectionId, out var value))
				{
					Kick(args.ConnectionId, KickReason.UnexpectedProblem, LoggingType.Error, $"ConnectionId {args.ConnectionId} not found within Clients. Connection will be kicked immediately.");
					break;
				}
				value.LocalTick.Update(timeManager, num);
				value.PacketTick.Update(timeManager, num, EstimatedTick.OldTickOption.SetLastRemoteTick);
				if (!value.IsAuthenticated && packetId != PacketId.Version && packetId != PacketId.Broadcast)
				{
					value.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"ConnectionId {value.ClientId} sent packetId {packetId} without being authenticated. Connection will be kicked immediately.");
					break;
				}
				switch (packetId)
				{
				case PacketId.Replicate:
					Objects.ParseReplicateRpc(pooledReader, value, args.Channel);
					break;
				case PacketId.ServerRpc:
					Objects.ParseServerRpc(pooledReader, value, args.Channel);
					break;
				case PacketId.ObjectSpawn:
					if (!GetAllowPredictedSpawning())
					{
						value.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"ConnectionId {value.ClientId} sent a predicted spawn while predicted spawning is not enabled. Connection will be kicked immediately.");
						return;
					}
					Objects.ReadSpawn(pooledReader, value);
					break;
				case PacketId.ObjectDespawn:
					if (!GetAllowPredictedSpawning())
					{
						value.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"ConnectionId {value.ClientId} sent a predicted spawn while predicted spawning is not enabled. Connection will be kicked immediately.");
						return;
					}
					Objects.ReadDespawn(pooledReader, value);
					break;
				case PacketId.Broadcast:
					ParseBroadcast(pooledReader, value, args.Channel);
					break;
				case PacketId.PingPong:
					ParsePingPong(pooledReader, value);
					break;
				case PacketId.Version:
					ParseVersion(pooledReader, value, args.TransportIndex);
					break;
				default:
					NetworkManager.LogError($"Server received an unhandled PacketId of {(ushort)packetId} on channel {args.Channel} from connectionId {args.ConnectionId}. Connection will be kicked immediately.");
					NetworkManager.TransportManager.Transport.StopConnection(args.ConnectionId, immediately: true);
					return;
				}
			}
		}
		catch (Exception ex)
		{
			Kick(args.ConnectionId, KickReason.MalformedData, LoggingType.Error, $"Server encountered an error while parsing data for packetId {packetId} from connectionId {args.ConnectionId}. Connection will be kicked immediately. Message: {ex.Message}. Exception: {ex.ToString()}");
		}
		finally
		{
			pooledReader?.Store();
		}
		void ExceededMTUKick()
		{
			Kick(args.ConnectionId, KickReason.ExploitExcessiveData, LoggingType.Common, $"ConnectionId {args.ConnectionId} sent a message larger than allowed amount. Connection will be kicked immediately.");
		}
	}

	private void ParsePingPong(PooledReader reader, NetworkConnection conn)
	{
		_ = reader.Position;
		uint clientTick = reader.ReadTickUnpacked();
		if (conn.CanPingPong())
		{
			NetworkManager.TimeManager.SendPong(conn, clientTick);
		}
	}

	private void ClientAuthenticated(NetworkConnection connection)
	{
		connection.ConnectionAuthenticated();
		BroadcastClientConnectionChange(connected: true, connection);
		SendAuthenticated(connection);
		this.OnAuthenticationResult?.Invoke(connection, arg2: true);
		NetworkManager.SceneManager.OnClientAuthenticated(connection);
	}

	private void BroadcastClientConnectionChange(bool connected, NetworkConnection conn)
	{
		if (!conn.IsAuthenticated)
		{
			return;
		}
		if (ShareIds)
		{
			ClientConnectionChangeBroadcast clientConnectionChangeBroadcast = default(ClientConnectionChangeBroadcast);
			clientConnectionChangeBroadcast.Connected = connected;
			clientConnectionChangeBroadcast.Id = conn.ClientId;
			ClientConnectionChangeBroadcast message = clientConnectionChangeBroadcast;
			foreach (NetworkConnection value in Clients.Values)
			{
				if (value.IsAuthenticated)
				{
					Broadcast(value, message);
				}
			}
			if (!connected)
			{
				return;
			}
			List<int> list = CollectionCaches<int>.RetrieveList();
			foreach (int key in Clients.Keys)
			{
				list.Add(key);
			}
			ConnectedClientsBroadcast connectedClientsBroadcast = default(ConnectedClientsBroadcast);
			connectedClientsBroadcast.Values = list;
			ConnectedClientsBroadcast message2 = connectedClientsBroadcast;
			conn.Broadcast(message2);
			CollectionCaches<int>.Store(list);
		}
		else if (connected)
		{
			ClientConnectionChangeBroadcast clientConnectionChangeBroadcast = default(ClientConnectionChangeBroadcast);
			clientConnectionChangeBroadcast.Connected = connected;
			clientConnectionChangeBroadcast.Id = conn.ClientId;
			ClientConnectionChangeBroadcast message3 = clientConnectionChangeBroadcast;
			Broadcast(conn, message3);
		}
	}
}
