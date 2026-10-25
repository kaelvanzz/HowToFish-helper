using System;
using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Component.Observing;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Connection;

public class NetworkConnection : IResettable, IEquatable<NetworkConnection>
{
	private List<PacketBundle> _toClientBundles = new List<PacketBundle>();

	private bool _serverDirtied;

	internal GridEntry HashGridEntry = HashGrid.EmptyGridEntry;

	private HashGrid _hashGrid;

	private float _nextHashGridUpdateTime;

	private Vector2Int _hashGridPosition = HashGrid.UnsetGridPosition;

	private uint _lastPingTick;

	private uint _requiredPingTicks;

	private const byte EXCESSIVE_PING_LIMIT = 10;

	internal List<PooledWriter> PredictionStateWriters = new List<PooledWriter>();

	internal Queue<int> PredictedObjectIds = new Queue<int>();

	internal bool HasSentVersion;

	internal uint ServerConnectionTick;

	public int ClientId = -1;

	public HashSet<NetworkObject> Objects = new HashSet<NetworkObject>();

	public object CustomData;

	private bool _loadedStartScenesAsServer;

	private bool _loadedStartScenesAsClient;

	public const int UNSET_CLIENTID_VALUE = -1;

	public const int MAXIMUM_CLIENTID_VALUE = int.MaxValue;

	public const int MAXIMUM_CLIENTID_WITHOUT_SIMULATED_VALUE = 2147483646;

	public const int SIMULATED_CLIENTID_VALUE = int.MaxValue;

	public const int CLIENTID_UNCOMPRESSED_RESERVE_LENGTH = 4;

	public EstimatedTick ReplicateTick { get; private set; } = new EstimatedTick();


	public bool IsHost
	{
		get
		{
			if (!(NetworkManager == null))
			{
				if (NetworkManager.IsServerStarted)
				{
					return this == NetworkManager.ClientManager.Connection;
				}
				return false;
			}
			return false;
		}
	}

	public bool IsLocalClient
	{
		get
		{
			if (!(NetworkManager == null))
			{
				return NetworkManager.ClientManager.Connection == this;
			}
			return false;
		}
	}

	internal uint DisconnectingTick { get; private set; }

	public NetworkManager NetworkManager { get; private set; }

	public int TransportIndex { get; private set; } = -1;


	public bool IsAuthenticated { get; private set; }

	[Obsolete("Use IsAuthenticated.")]
	public bool Authenticated
	{
		get
		{
			return IsAuthenticated;
		}
		set
		{
			IsAuthenticated = value;
		}
	}

	public bool IsActive
	{
		get
		{
			if (ClientId >= 0)
			{
				return !Disconnecting;
			}
			return false;
		}
	}

	public bool IsValid => ClientId >= 0;

	public NetworkObject FirstObject { get; private set; }

	public HashSet<Scene> Scenes { get; private set; } = new HashSet<Scene>();


	public bool Disconnecting { get; private set; }

	public EstimatedTick PacketTick { get; private set; } = new EstimatedTick();


	public EstimatedTick LocalTick { get; private set; } = new EstimatedTick();


	public event Action<NetworkConnection, bool> OnLoadedStartScenes;

	public event Action<NetworkObject> OnObjectAdded;

	public event Action<NetworkObject> OnObjectRemoved;

	private void InitializeBuffer()
	{
		for (byte b = 0; b < 2; b++)
		{
			int lowestMTU = NetworkManager.TransportManager.GetLowestMTU(b);
			_toClientBundles.Add(new PacketBundle(NetworkManager, lowestMTU));
		}
	}

	public void Broadcast<T>(T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
	{
		if (!IsActive)
		{
			NetworkManager.LogError("Connection is not valid, cannot send broadcast.");
		}
		else
		{
			NetworkManager.ServerManager.Broadcast(this, message, requireAuthenticated, channel);
		}
	}

	internal void SendToClient(byte channel, ArraySegment<byte> segment, bool forceNewBuffer = false, DataOrderType orderType = DataOrderType.Default)
	{
		if (Disconnecting)
		{
			return;
		}
		if (!IsActive)
		{
			NetworkManager.LogWarning($"Data cannot be sent to connection {ClientId} because it is not active.");
			return;
		}
		if (channel >= _toClientBundles.Count)
		{
			channel = 0;
		}
		_toClientBundles[channel].Write(segment, forceNewBuffer, orderType);
		ServerDirty();
	}

	internal bool GetPacketBundle(int channel, out PacketBundle packetBundle)
	{
		return PacketBundle.GetPacketBundle(channel, _toClientBundles, out packetBundle);
	}

	private void ServerDirty()
	{
		bool serverDirtied = _serverDirtied;
		_serverDirtied = true;
		if (!serverDirtied)
		{
			NetworkManager.TransportManager.ServerDirty(this);
		}
	}

	internal void ResetServerDirty()
	{
		_serverDirtied = false;
	}

	private void Observers_FirstObjectChanged()
	{
		UpdateHashGridPositions(force: true);
	}

	private void Observers_Initialize(NetworkManager nm)
	{
		nm.TryGetInstance<HashGrid>(out _hashGrid);
	}

	internal void UpdateHashGridPositions(bool force)
	{
		if (_hashGrid == null)
		{
			return;
		}
		float unscaledTime = Time.unscaledTime;
		if (!force && unscaledTime < _nextHashGridUpdateTime)
		{
			return;
		}
		_nextHashGridUpdateTime = unscaledTime + 1f;
		if (FirstObject == null)
		{
			HashGridEntry = HashGrid.EmptyGridEntry;
			_hashGridPosition = HashGrid.UnsetGridPosition;
			return;
		}
		Vector2Int hashGridPosition = _hashGrid.GetHashGridPosition(FirstObject);
		if (hashGridPosition != _hashGridPosition)
		{
			_hashGridPosition = hashGridPosition;
			HashGridEntry = _hashGrid.GetGridEntry(hashGridPosition);
		}
	}

	private void Observers_Reset()
	{
		_hashGrid = null;
		_hashGridPosition = HashGrid.UnsetGridPosition;
		_nextHashGridUpdateTime = 0f;
	}

	private void InitializePing()
	{
		float num = (float)(int)NetworkManager.TimeManager.PingInterval * 0.85f;
		_requiredPingTicks = NetworkManager.TimeManager.TimeToTicks(num, TickRounding.RoundDown);
	}

	private void ResetPingPong()
	{
		_lastPingTick = 0u;
	}

	internal bool CanPingPong()
	{
		TimeManager timeManager = ((NetworkManager == null) ? InstanceFinder.TimeManager : NetworkManager.TimeManager);
		if (timeManager.LowFrameRate)
		{
			return false;
		}
		uint tick = timeManager.Tick;
		uint num = tick - _lastPingTick;
		_lastPingTick = tick;
		if (num < _requiredPingTicks)
		{
			return false;
		}
		return true;
	}

	internal void Prediction_Initialize(NetworkManager manager, bool asServer)
	{
	}

	internal void WriteState(PooledWriter data)
	{
		if (IsLocalClient)
		{
			return;
		}
		TimeManager timeManager = NetworkManager.TimeManager;
		TransportManager transportManager = NetworkManager.TransportManager;
		if (((!IsLocalClient) ? PacketTick.LocalTickDifference(timeManager) : 0) <= timeManager.TickRate * 5)
		{
			int lowestMTU = transportManager.GetLowestMTU(1);
			int count = PredictionStateWriters.Count;
			Channel channel = Channel.Unreliable;
			if (count > 0)
			{
				transportManager.CheckSetReliableChannel(data.Length + PredictionStateWriters[count - 1].Length, ref channel);
			}
			PooledWriter pooledWriter;
			if (count == 0 || channel == Channel.Reliable)
			{
				pooledWriter = WriterPool.Retrieve(lowestMTU);
				PredictionStateWriters.Add(pooledWriter);
				pooledWriter.Skip(10);
			}
			else
			{
				pooledWriter = PredictionStateWriters[count - 1];
			}
			pooledWriter.WriteArraySegment(data.GetArraySegment());
		}
	}

	internal void StorePredictionStateWriters()
	{
		for (int i = 0; i < PredictionStateWriters.Count; i++)
		{
			WriterPool.Store(PredictionStateWriters[i]);
		}
		PredictionStateWriters.Clear();
	}

	internal void SetReplicateTick(uint value, EstimatedTick.OldTickOption oldTickOption = EstimatedTick.OldTickOption.Discard)
	{
		ReplicateTick.Update(value, oldTickOption);
	}

	private void Prediction_Reset()
	{
		StorePredictionStateWriters();
		ReplicateTick.Reset();
	}

	public string GetAddress()
	{
		if (!IsValid)
		{
			return string.Empty;
		}
		if (NetworkManager == null)
		{
			return string.Empty;
		}
		return NetworkManager.TransportManager.Transport.GetConnectionAddress(ClientId);
	}

	public void Kick(KickReason kickReason, LoggingType loggingType = LoggingType.Common, string log = "")
	{
		if (CanKick())
		{
			NetworkManager.ServerManager.Kick(this, kickReason, loggingType, log);
		}
	}

	public void Kick(Reader reader, KickReason kickReason, LoggingType loggingType = LoggingType.Common, string log = "")
	{
		if (CanKick())
		{
			NetworkManager.ServerManager.Kick(this, reader, kickReason, loggingType, log);
		}
	}

	private bool CanKick()
	{
		if (!IsValid)
		{
			return false;
		}
		if (NetworkManager == null)
		{
			NetworkManager = InstanceFinder.NetworkManager;
			NetworkManager.LogError("NetworkManager was not set for connection " + ToString() + ". InstanceFinder has been used.");
		}
		return true;
	}

	public bool LoadedStartScenes()
	{
		if (!_loadedStartScenesAsServer)
		{
			return _loadedStartScenesAsClient;
		}
		return true;
	}

	public bool LoadedStartScenes(bool asServer)
	{
		if (asServer)
		{
			return _loadedStartScenesAsServer;
		}
		return _loadedStartScenesAsClient;
	}

	public void SetFirstObject(NetworkObject nob)
	{
		if (!Objects.Contains(nob))
		{
			string message = $"FirstObject for {ClientId} cannot be set to {nob.name} as it's not within Objects for this connection.";
			NetworkManager.LogError(message);
		}
		else
		{
			FirstObject = nob;
		}
	}

	public override bool Equals(object obj)
	{
		if (obj is NetworkConnection networkConnection)
		{
			return networkConnection.ClientId == ClientId;
		}
		return false;
	}

	public bool Equals(NetworkConnection nc)
	{
		if ((object)nc == null)
		{
			return false;
		}
		if (ClientId == -1 || nc.ClientId == -1)
		{
			return false;
		}
		if ((object)this == nc)
		{
			return true;
		}
		return ClientId == nc.ClientId;
	}

	public override int GetHashCode()
	{
		return ClientId;
	}

	public static bool operator ==(NetworkConnection a, NetworkConnection b)
	{
		if ((object)a == null && (object)b == null)
		{
			return true;
		}
		if ((object)a == null && (object)b != null)
		{
			return false;
		}
		if (!(b == null))
		{
			return b.Equals(a);
		}
		return a.Equals(b);
	}

	public static bool operator !=(NetworkConnection a, NetworkConnection b)
	{
		return !(a == b);
	}

	[APIExclude]
	public NetworkConnection()
	{
	}

	[APIExclude]
	public NetworkConnection(NetworkManager manager, int clientId, int transportIndex, bool asServer)
	{
		Initialize(manager, clientId, transportIndex, asServer);
	}

	public override string ToString()
	{
		int clientId = ClientId;
		string arg = ((NetworkManager != null) ? NetworkManager.TransportManager.Transport.GetConnectionAddress(clientId) : "Unset");
		return $"Id [{ClientId}] Address [{arg}]";
	}

	private void Initialize(NetworkManager nm, int clientId, int transportIndex, bool asServer)
	{
		NetworkManager = nm;
		LocalTick.Initialize(nm.TimeManager);
		PacketTick.Initialize(nm.TimeManager);
		if (asServer)
		{
			ServerConnectionTick = nm.TimeManager.LocalTick;
		}
		TransportIndex = transportIndex;
		ClientId = clientId;
		PacketTick.Update(nm.TimeManager, 0u, EstimatedTick.OldTickOption.SetLastRemoteTick);
		Observers_Initialize(nm);
		Prediction_Initialize(nm, asServer);
		if (asServer)
		{
			InitializeBuffer();
			InitializePing();
		}
	}

	internal void SetDisconnecting(bool value)
	{
		Disconnecting = value;
		if (Disconnecting)
		{
			DisconnectingTick = NetworkManager.TimeManager.LocalTick;
		}
	}

	public void Disconnect(bool immediately)
	{
		if (!IsValid)
		{
			NetworkManager.LogWarning("Disconnect called on an invalid connection.");
			return;
		}
		if (Disconnecting)
		{
			NetworkManager.LogWarning($"ClientId {ClientId} is already disconnecting.");
			return;
		}
		SetDisconnecting(value: true);
		if (immediately)
		{
			NetworkManager.TransportManager.Transport.StopConnection(ClientId, immediately: true);
		}
		else
		{
			ServerDirty();
		}
	}

	internal bool SetLoadedStartScenes(bool asServer)
	{
		bool result = !(asServer ? _loadedStartScenesAsServer : _loadedStartScenesAsClient);
		if (asServer)
		{
			_loadedStartScenesAsServer = true;
		}
		else
		{
			_loadedStartScenesAsClient = true;
		}
		Action<NetworkConnection, bool> onLoadedStartScenes = this.OnLoadedStartScenes;
		if (onLoadedStartScenes != null)
		{
			onLoadedStartScenes(this, asServer);
			return result;
		}
		return result;
	}

	internal void ConnectionAuthenticated()
	{
		IsAuthenticated = true;
	}

	internal void AddObject(NetworkObject nob)
	{
		if (IsValid)
		{
			Objects.Add(nob);
			if (Objects.Count == 1)
			{
				SetFirstObject();
			}
			this.OnObjectAdded?.Invoke(nob);
		}
	}

	internal void RemoveObject(NetworkObject nob)
	{
		if (!IsValid)
		{
			ClearObjects();
			return;
		}
		Objects.Remove(nob);
		if (nob == FirstObject)
		{
			SetFirstObject();
		}
		this.OnObjectRemoved?.Invoke(nob);
	}

	private void ClearObjects()
	{
		Objects.Clear();
		FirstObject = null;
	}

	private void SetFirstObject()
	{
		if (Objects.Count == 0)
		{
			FirstObject = null;
			return;
		}
		using HashSet<NetworkObject>.Enumerator enumerator = Objects.GetEnumerator();
		if (enumerator.MoveNext())
		{
			NetworkObject current = enumerator.Current;
			FirstObject = current;
			Observers_FirstObjectChanged();
		}
	}

	internal bool AddToScene(Scene scene)
	{
		return Scenes.Add(scene);
	}

	internal bool RemoveFromScene(Scene scene)
	{
		return Scenes.Remove(scene);
	}

	public void ResetState()
	{
		MatchCondition.RemoveFromMatchesWithoutRebuild(this, NetworkManager);
		foreach (PacketBundle toClientBundle in _toClientBundles)
		{
			toClientBundle.Dispose();
		}
		_toClientBundles.Clear();
		ServerConnectionTick = 0u;
		PacketTick.Reset();
		LocalTick.Reset();
		TransportIndex = -1;
		ClientId = -1;
		ClearObjects();
		IsAuthenticated = false;
		HasSentVersion = false;
		NetworkManager = null;
		_loadedStartScenesAsClient = false;
		_loadedStartScenesAsServer = false;
		SetDisconnecting(value: false);
		Scenes.Clear();
		PredictedObjectIds.Clear();
		ResetPingPong();
		Observers_Reset();
		Prediction_Reset();
	}

	public void InitializeState()
	{
	}
}
