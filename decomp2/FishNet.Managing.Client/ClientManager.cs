using System;
using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Broadcast.Helping;
using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Managing.Statistic;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Managing.Utility;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Managing.Client;

[DisallowMultipleComponent]
[AddComponentMenu("FishNet/Manager/ClientManager")]
public sealed class ClientManager : MonoBehaviour
{
	private readonly Dictionary<ushort, BroadcastHandlerBase> _broadcastHandlers = new Dictionary<ushort, BroadcastHandlerBase>();

	public NetworkConnection Connection = NetworkManager.EmptyConnection;

	public Dictionary<int, NetworkConnection> Clients = new Dictionary<int, NetworkConnection>();

	[Tooltip("What platforms to enable remote server timeout.")]
	[SerializeField]
	private RemoteTimeoutType _remoteServerTimeout = RemoteTimeoutType.Development;

	[Tooltip("How long in seconds server must go without sending any packets before the local client disconnects. This is independent of any transport settings.")]
	[Range(1f, 1500f)]
	[SerializeField]
	private ushort _remoteServerTimeoutDuration = 60;

	[Tooltip("True to automatically set the frame rate when the client connects.")]
	[SerializeField]
	private bool _changeFrameRate = true;

	[Tooltip("Maximum frame rate the client may run at. When as host this value runs at whichever is higher between client and server.")]
	[Range(1f, 500f)]
	[SerializeField]
	private ushort _frameRate = 500;

	private float _lastPacketTime;

	private SplitReader _splitReader = new SplitReader();

	private NetworkTrafficStatistics _networkTrafficStatistics;

	public bool IsServerDevelopment { get; private set; }

	public bool Started { get; private set; }

	public ClientObjects Objects { get; private set; }

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

	public event Action OnAuthenticated;

	public event Action OnClientTimeOut;

	public event Action<ClientConnectionStateArgs> OnClientConnectionState;

	public event Action<RemoteConnectionStateArgs> OnRemoteConnectionState;

	public event Action<ConnectedClientsArgs> OnConnectedClients;

	public void RegisterBroadcast<T>(Action<T, Channel> handler) where T : struct, IBroadcast
	{
		if (handler == null)
		{
			NetworkManager.LogError("Broadcast cannot be registered because handler is null. This may occur when trying to register to objects which require initialization, such as events.");
			return;
		}
		ushort key = BroadcastExtensions.GetKey<T>();
		if (!_broadcastHandlers.TryGetValueIL2CPP(key, out var value))
		{
			value = new ServerBroadcastHandler<T>();
			_broadcastHandlers.Add(key, value);
		}
		value.RegisterHandler(handler);
	}

	public void UnregisterBroadcast<T>(Action<T, Channel> handler) where T : struct, IBroadcast
	{
		ushort key = BroadcastExtensions.GetKey<T>();
		if (_broadcastHandlers.TryGetValueIL2CPP(key, out var value))
		{
			value.UnregisterHandler(handler);
		}
	}

	private void ParseBroadcast(PooledReader reader, Channel channel)
	{
		_ = reader.Position;
		ushort key = reader.ReadUInt16();
		int packetLength = Packets.GetPacketLength(12, reader, channel);
		if (_broadcastHandlers.TryGetValueIL2CPP(key, out var value))
		{
			value.InvokeHandlers(reader, channel);
		}
		else
		{
			reader.Skip(packetLength);
		}
	}

	public void Broadcast<T>(T message, Channel channel = Channel.Reliable) where T : struct, IBroadcast
	{
		if (!Started)
		{
			NetworkManager.LogWarning("Cannot send broadcast to server because client is not active.");
			return;
		}
		PooledWriter pooledWriter = WriterPool.Retrieve();
		BroadcastsSerializers.WriteBroadcast(NetworkManager, pooledWriter, message, ref channel);
		ArraySegment<byte> arraySegment = pooledWriter.GetArraySegment();
		NetworkManager.TransportManager.SendToServer((byte)channel, arraySegment);
		pooledWriter.Store();
	}

	public void SetRemoteServerTimeout(RemoteTimeoutType timeoutType, ushort duration)
	{
		_remoteServerTimeout = timeoutType;
		duration = (ushort)Mathf.Clamp(duration, 1, 1500);
		_remoteServerTimeoutDuration = duration;
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

	private void OnDestroy()
	{
		Objects?.SubscribeToSceneLoaded(subscribe: false);
	}

	internal void InitializeOnce_Internal(NetworkManager manager)
	{
		NetworkManager = manager;
		manager.StatisticsManager.TryGetNetworkTrafficStatistics(out _networkTrafficStatistics);
		Objects = new ClientObjects(manager);
		Objects.SubscribeToSceneLoaded(subscribe: true);
		SubscribeToEvents(subscribe: false);
		SubscribeToEvents(subscribe: true);
		RegisterBroadcast<ClientConnectionChangeBroadcast>(OnClientConnectionBroadcast);
		RegisterBroadcast<ConnectedClientsBroadcast>(OnConnectedClientsBroadcast);
	}

	private void OnClientConnectionBroadcast(ClientConnectionChangeBroadcast args, Channel channel)
	{
		RemoteConnectionStateArgs obj = new RemoteConnectionStateArgs(args.Connected ? RemoteConnectionState.Started : RemoteConnectionState.Stopped, args.Id, -1);
		if (args.Connected)
		{
			Clients[args.Id] = new NetworkConnection(NetworkManager, args.Id, -1, asServer: false);
			this.OnRemoteConnectionState?.Invoke(obj);
			return;
		}
		this.OnRemoteConnectionState?.Invoke(obj);
		if (Clients.TryGetValue(args.Id, out var value))
		{
			value.ResetState();
			Clients.Remove(args.Id);
		}
	}

	private void OnConnectedClientsBroadcast(ConnectedClientsBroadcast args, Channel channel)
	{
		NetworkManager.ClearClientsCollection(Clients);
		List<int> list = args.Values;
		if (list == null)
		{
			list = new List<int>();
		}
		else
		{
			int count = list.Count;
			for (int i = 0; i < count; i++)
			{
				int num = list[i];
				Clients[num] = new NetworkConnection(NetworkManager, num, -1, asServer: false);
			}
		}
		this.OnConnectedClients?.Invoke(new ConnectedClientsArgs(list));
	}

	private void SubscribeToEvents(bool subscribe)
	{
		if (!(NetworkManager == null) && !(NetworkManager.TransportManager == null) && !(NetworkManager.TransportManager.Transport == null))
		{
			if (subscribe)
			{
				NetworkManager.TransportManager.OnIterateIncomingEnd += TransportManager_OnIterateIncomingEnd;
				NetworkManager.TransportManager.Transport.OnClientReceivedData += Transport_OnClientReceivedData;
				NetworkManager.TransportManager.Transport.OnClientConnectionState += Transport_OnClientConnectionState;
				NetworkManager.TimeManager.OnPostTick += TimeManager_OnPostTick;
			}
			else
			{
				NetworkManager.TransportManager.OnIterateIncomingEnd -= TransportManager_OnIterateIncomingEnd;
				NetworkManager.TransportManager.Transport.OnClientReceivedData -= Transport_OnClientReceivedData;
				NetworkManager.TransportManager.Transport.OnClientConnectionState -= Transport_OnClientConnectionState;
				NetworkManager.TimeManager.OnPostTick -= TimeManager_OnPostTick;
			}
		}
	}

	public int GetTransportIndex()
	{
		if (NetworkManager.TransportManager.Transport is Multipass multipass)
		{
			return multipass.ClientTransport.Index;
		}
		return 0;
	}

	public bool StopConnection()
	{
		return NetworkManager.TransportManager.Transport.StopConnection(server: false);
	}

	public bool StartConnection()
	{
		return NetworkManager.TransportManager.Transport.StartConnection(server: false);
	}

	public bool StartConnection(string address)
	{
		NetworkManager.TransportManager.Transport.SetClientAddress(address);
		return StartConnection();
	}

	public bool StartConnection(string address, ushort port)
	{
		NetworkManager.TransportManager.Transport.SetClientAddress(address);
		NetworkManager.TransportManager.Transport.SetPort(port);
		return StartConnection();
	}

	private void Transport_OnClientConnectionState(ClientConnectionStateArgs args)
	{
		LocalConnectionState connectionState = args.ConnectionState;
		Started = connectionState == LocalConnectionState.Started;
		Objects.OnClientConnectionState(args);
		if (!Started)
		{
			Connection = NetworkManager.EmptyConnection;
			NetworkManager.ClearClientsCollection(Clients);
		}
		else
		{
			_lastPacketTime = Time.unscaledTime;
			PooledWriter pooledWriter = WriterPool.Retrieve();
			pooledWriter.WritePacketIdUnpacked(PacketId.Version);
			pooledWriter.WriteString("4.6.12");
			NetworkManager.TransportManager.SendToServer(0, pooledWriter.GetArraySegment());
			WriterPool.Store(pooledWriter);
		}
		if (NetworkManager.CanLog(LoggingType.Common))
		{
			Transport transport = NetworkManager.TransportManager.GetTransport(args.TransportIndex);
			string text = ((transport == null) ? "Unknown" : transport.GetType().Name);
			string text2 = string.Empty;
			if (connectionState == LocalConnectionState.Starting)
			{
				text2 = $" Server IP is {transport.GetClientAddress()}, port is {transport.GetPort()}.";
			}
			NetworkManager.Log("Local client is " + connectionState.ToString().ToLower() + " for " + text + "." + text2);
		}
		NetworkManager.UpdateFramerate();
		this.OnClientConnectionState?.Invoke(args);
	}

	private void Transport_OnClientReceivedData(ClientReceivedDataArgs args)
	{
		ParseReceived(args);
	}

	private void TransportManager_OnIterateIncomingEnd(bool server)
	{
		if (Started && !server)
		{
			Objects.IterateObjectCache();
		}
	}

	private void ParseReceived(ClientReceivedDataArgs args)
	{
		_lastPacketTime = Time.unscaledTime;
		ArraySegment<byte> segment = ((!NetworkManager.TransportManager.HasIntermediateLayer) ? args.Data : NetworkManager.TransportManager.ProcessIntermediateIncoming(args.Data, fromServer: true));
		if (_networkTrafficStatistics != null)
		{
			_networkTrafficStatistics.AddInboundSocketData((ulong)segment.Count, asServer: false);
		}
		if (segment.Count > 4)
		{
			PooledReader pooledReader = ReaderPool.Retrieve(segment, NetworkManager, Reader.DataSource.Server);
			NetworkManager.TimeManager.LastPacketTick.Update(pooledReader.ReadTickUnpacked(), EstimatedTick.OldTickOption.Discard, resetValue: false);
			ParseReader(pooledReader, args.Channel);
			ReaderPool.Store(pooledReader);
		}
	}

	internal void ParseReader(PooledReader reader, Channel channel, bool print = false)
	{
		PacketId packetId = PacketId.Unset;
		try
		{
			Reader.DataSource source = Reader.DataSource.Server;
			if (reader.PeekPacketId() == PacketId.Split)
			{
				reader.ReadPacketId();
				_splitReader.GetHeader(reader, out var expectedMessages);
				_splitReader.Write(NetworkManager.TimeManager.LastPacketTick.LastRemoteTick, reader, expectedMessages);
				ArraySegment<byte> fullMessage = _splitReader.GetFullMessage();
				if (fullMessage.Count == 0)
				{
					return;
				}
				reader.Initialize(fullMessage, NetworkManager, source);
			}
			while (reader.Remaining > 0)
			{
				packetId = reader.ReadPacketId();
				if (packetId == PacketId.ObjectSpawn || packetId == PacketId.ObjectDespawn)
				{
					switch (packetId)
					{
					case PacketId.ObjectSpawn:
						Objects.ReadSpawn(reader);
						break;
					case PacketId.ObjectDespawn:
						Objects.CacheDespawn(reader);
						break;
					}
					continue;
				}
				Objects.IterateObjectCache();
				if ((int)packetId >= (int)NetworkManager.StartingRpcLinkIndex)
				{
					Objects.ParseRpcLink(reader, (ushort)packetId, channel);
					continue;
				}
				switch (packetId)
				{
				case PacketId.StateUpdate:
					NetworkManager.PredictionManager.ParseStateUpdate(reader, channel);
					break;
				case PacketId.Replicate:
					Objects.ParseReplicateRpc(reader, null, channel);
					break;
				case PacketId.Reconcile:
					Objects.ParseReconcileRpc(reader, channel);
					break;
				case PacketId.ObserversRpc:
					Objects.ParseObserversRpc(reader, channel);
					break;
				case PacketId.TargetRpc:
					Objects.ParseTargetRpc(reader, channel);
					break;
				case PacketId.Broadcast:
					ParseBroadcast(reader, channel);
					break;
				case PacketId.PingPong:
					ParsePingPong(reader);
					break;
				case PacketId.SyncType:
					Objects.ParseSyncType(reader, channel);
					break;
				case PacketId.PredictedSpawnResult:
					Objects.ParsePredictedSpawnResult(reader);
					break;
				case PacketId.TimingUpdate:
					NetworkManager.TimeManager.ParseTimingUpdate(reader);
					break;
				case PacketId.OwnershipChange:
					Objects.ParseOwnershipChange(reader);
					break;
				case PacketId.Authenticated:
					ParseAuthenticated(reader);
					break;
				case PacketId.Disconnect:
					reader.Clear();
					StopConnection();
					break;
				case PacketId.Version:
					ParseVersion(reader);
					break;
				default:
					NetworkManager.LogError($"Client received an unhandled PacketId of {(ushort)packetId} on channel {channel}. Remaining data has been purged.");
					return;
				}
			}
			Objects.IterateObjectCache();
		}
		catch (Exception ex)
		{
			NetworkManagerExtensions.LogError($"Client encountered an error while parsing data for packetId {packetId}. Message: {ex.Message}.");
		}
	}

	private void ParsePingPong(PooledReader reader)
	{
		_ = reader.Position;
		uint clientTick = reader.ReadTickUnpacked();
		NetworkManager.TimeManager.ModifyPing(clientTick);
	}

	private void ParseVersion(PooledReader reader)
	{
		IsServerDevelopment = reader.ReadBoolean();
	}

	private void ParseAuthenticated(PooledReader reader)
	{
		NetworkManager networkManager = NetworkManager;
		int num = reader.ReadNetworkConnectionId();
		NetworkConnection value;
		if (!networkManager.IsServerStarted)
		{
			Clients.TryGetValueIL2CPP(num, out Connection);
			if (Connection == null)
			{
				NetworkManager.LogWarning("Client connection could not be found while parsing authenticated status. This usually occurs when the client is receiving a packet immediately before losing connection.");
				Connection = new NetworkConnection(networkManager, num, GetTransportIndex(), asServer: false);
			}
		}
		else if (networkManager.ServerManager.Clients.TryGetValueIL2CPP(num, out value))
		{
			Connection = value;
		}
		else
		{
			networkManager.LogError($"Unable to lookup LocalConnection for {num} as host.");
			Connection = new NetworkConnection(networkManager, num, GetTransportIndex(), asServer: false);
		}
		if (NetworkManager.ServerManager.GetAllowPredictedSpawning())
		{
			int num2 = (int)reader.ReadSignedPackedWhole();
			Queue<int> predictedObjectIds = Connection.PredictedObjectIds;
			for (int i = 0; i < num2; i++)
			{
				predictedObjectIds.Enqueue(reader.ReadNetworkObjectId());
			}
		}
		if (!networkManager.IsServerStarted)
		{
			networkManager.TimeManager.Tick = networkManager.TimeManager.LastPacketTick.LastRemoteTick;
		}
		Connection.ConnectionAuthenticated();
		this.OnAuthenticated?.Invoke();
		Objects.RegisterAndDespawnSceneObjects();
	}

	private void TimeManager_OnPostTick()
	{
		CheckServerTimeout();
	}

	private void CheckServerTimeout()
	{
		if (Started && !NetworkManager.IsServerStarted && _remoteServerTimeout != 0 && !NetworkManager.SceneManager.IsIteratingQueue(2f) && Time.unscaledTime - _lastPacketTime > (float)(int)_remoteServerTimeoutDuration)
		{
			this.OnClientTimeOut?.Invoke();
			NetworkManager.Log("Server has timed out. You can modify this feature on the ClientManager component.");
			StopConnection();
		}
	}
}
