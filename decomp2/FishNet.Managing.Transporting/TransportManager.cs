using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing.Statistic;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Managing.Transporting;

[DisallowMultipleComponent]
[AddComponentMenu("FishNet/Manager/TransportManager")]
public sealed class TransportManager : MonoBehaviour
{
	private struct DisconnectingClient
	{
		public uint Tick;

		public NetworkConnection Connection;

		public DisconnectingClient(uint tick, NetworkConnection connection)
		{
			Tick = tick;
			Connection = connection;
		}
	}

	[Tooltip("The current Transport being used.")]
	public Transport Transport;

	[Tooltip("Layer used to modify data before it is sent or received.")]
	[SerializeField]
	private IntermediateLayer _intermediateLayer;

	[Tooltip("Latency simulation settings.")]
	[SerializeField]
	private LatencySimulator _latencySimulator = new LatencySimulator();

	private List<NetworkConnection> _dirtyToClients = new List<NetworkConnection>();

	private List<PacketBundle> _toServerBundles = new List<PacketBundle>();

	private NetworkManager _networkManager;

	private List<DisconnectingClient> _disconnectingClients = new List<DisconnectingClient>();

	private int[] _lowestMtus;

	private int _lowestMtu;

	private int _customMtuReserve = 1;

	private NetworkTrafficStatistics _networkTrafficStatistics;

	public const byte PACKETID_LENGTH = 2;

	public const byte OBJECT_ID_LENGTH = 2;

	public const byte COMPONENT_INDEX_LENGTH = 1;

	public const byte UNPACKED_TICK_LENGTH = 4;

	public const byte UNPACKED_SIZE_LENGTH = 4;

	private const byte SPLIT_COUNT_LENGTH = 4;

	public const byte SPLIT_INDICATOR_LENGTH = 10;

	public const byte CHANNEL_COUNT = 2;

	public const int MINIMUM_MTU_RESERVE = 1;

	public const int INVALID_MTU = -1;

	public bool HasIntermediateLayer => _intermediateLayer != null;

	public LatencySimulator LatencySimulator
	{
		get
		{
			if (_latencySimulator == null)
			{
				_latencySimulator = new LatencySimulator();
			}
			return _latencySimulator;
		}
	}

	internal event Action OnIterateOutgoingStart;

	internal event Action OnIterateOutgoingEnd;

	internal event Action<bool> OnIterateIncomingStart;

	internal event Action<bool> OnIterateIncomingEnd;

	public bool IsLocalTransport(int transportId, int connectionId = -1)
	{
		if (Transport == null)
		{
			return false;
		}
		if (Transport is Multipass multipass)
		{
			return multipass.IsLocalTransport(transportId, connectionId);
		}
		return Transport.IsLocalTransport(connectionId);
	}

	public Transport GetTransport(int index)
	{
		if (Transport is Multipass multipass)
		{
			return multipass.GetTransport(index);
		}
		return Transport;
	}

	public T GetTransport<T>() where T : Transport
	{
		if (Transport is Multipass multipass)
		{
			if (typeof(T) == typeof(Multipass))
			{
				return (T)(Transport)multipass;
			}
			return multipass.GetTransport<T>();
		}
		if (Transport.GetType() == typeof(T))
		{
			return (T)Transport;
		}
		return null;
	}

	public List<Transport> GetAllTransports(bool includeMultipass)
	{
		List<Transport> list = CollectionCaches<Transport>.RetrieveList();
		if (Transport is Multipass multipass)
		{
			if (includeMultipass)
			{
				list.Add(Transport);
			}
			foreach (Transport transport in multipass.Transports)
			{
				list.Add(transport);
			}
		}
		else
		{
			list.Add(Transport);
		}
		return list;
	}

	internal void InitializeOnce_Internal(NetworkManager manager)
	{
		_networkManager = manager;
		TryAddDefaultTransport();
		Transport.Initialize(_networkManager, 0);
		SetLowestMTUs();
		InitializeToServerBundles();
		manager.StatisticsManager.TryGetNetworkTrafficStatistics(out _networkTrafficStatistics);
		manager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
		manager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
		if (_intermediateLayer != null)
		{
			_intermediateLayer.InitializeOnce(this);
		}
	}

	private void SetLowestMTUs()
	{
		if (_lowestMtu != 0)
		{
			return;
		}
		TryAddDefaultTransport();
		int a = int.MaxValue;
		_lowestMtus = new int[2];
		for (byte b = 0; b < 2; b++)
		{
			int num = int.MaxValue;
			if (Transport is Multipass multipass)
			{
				foreach (Transport transport in multipass.Transports)
				{
					int mTU = transport.GetMTU(b);
					if (mTU != -1)
					{
						num = Mathf.Min(num, mTU);
					}
				}
			}
			else
			{
				num = Transport.GetMTU(b);
			}
			_lowestMtus[b] = num;
			_lowestMtu = Mathf.Min(a, num);
		}
	}

	private void TryAddDefaultTransport()
	{
		if (Transport == null && !base.gameObject.TryGetComponent<Transport>(out Transport))
		{
			Transport = base.gameObject.AddComponent<Tugboat>();
		}
	}

	private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
	{
		if (obj.ConnectionState != LocalConnectionState.Stopped)
		{
			return;
		}
		foreach (PacketBundle toServerBundle in _toServerBundles)
		{
			toServerBundle.Reset(resetSendLast: true);
		}
	}

	private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
	{
		if (obj.ConnectionState != LocalConnectionState.Stopped)
		{
			return;
		}
		if (!_networkManager.ServerManager.IsAnyServerStarted())
		{
			_dirtyToClients.Clear();
			return;
		}
		int transportIndex = obj.TransportIndex;
		List<NetworkConnection> list = CollectionCaches<NetworkConnection>.RetrieveList();
		foreach (NetworkConnection dirtyToClient in _dirtyToClients)
		{
			if (dirtyToClient.TransportIndex == transportIndex)
			{
				list.Add(dirtyToClient);
			}
		}
		foreach (NetworkConnection item in list)
		{
			_dirtyToClients.Remove(item);
		}
		CollectionCaches<NetworkConnection>.Store(list);
	}

	internal void ServerDirty(NetworkConnection conn)
	{
		_dirtyToClients.Add(conn);
	}

	private void InitializeToServerBundles()
	{
		for (byte b = 0; b < 2; b++)
		{
			int lowestMTU = GetLowestMTU(b);
			_toServerBundles.Add(new PacketBundle(_networkManager, lowestMTU));
		}
	}

	private int GetMTUWithReserve(int mtu)
	{
		int num = mtu - 1 - _customMtuReserve;
		if (num <= 100)
		{
			string message = string.Format("Available MTU of {0} is significantly low; an invalid MTU will be returned. Check transport settings, or reduce MTU reserve if you set one using {1}", mtu, "SetMTUReserve");
			_networkManager.LogWarning(message);
			return -1;
		}
		return num;
	}

	public void SetMTUReserve(int value)
	{
		if ((_networkManager != null && _networkManager.IsClientStarted) || _networkManager.IsServerStarted)
		{
			_networkManager.LogError("A custom MTU reserve cannot be set after the server or client have been started or connected.");
			return;
		}
		if (value < 1)
		{
			_networkManager.Log($"MTU reserve {value} is below minimum value of {1}. Value has been updated to {1}.");
			value = 1;
		}
		_customMtuReserve = value;
		InitializeToServerBundles();
	}

	public int GetMTUReserve()
	{
		return _customMtuReserve;
	}

	public int GetLowestMTU()
	{
		SetLowestMTUs();
		return GetMTUWithReserve(_lowestMtu);
	}

	public int GetLowestMTU(byte channel)
	{
		SetLowestMTUs();
		return GetMTUWithReserve(_lowestMtus[channel]);
	}

	public int GetMTU(byte channel)
	{
		SetLowestMTUs();
		int mTU = Transport.GetMTU(channel);
		if (mTU == -1)
		{
			return mTU;
		}
		return GetMTUWithReserve(mTU);
	}

	public int GetMTU(int transportIndex, byte channel)
	{
		if (Transport is Multipass multipass)
		{
			int mTU = multipass.GetMTU(channel, transportIndex);
			if (mTU == -1)
			{
				return -1;
			}
			return GetMTUWithReserve(mTU);
		}
		if (transportIndex == 0)
		{
			return GetMTU(channel);
		}
		_networkManager.LogWarning("MTU cannot be returned with transportIndex because " + typeof(Multipass).Name + " is not in use.");
		return -1;
	}

	public int GetMTU<T>(byte channel) where T : Transport
	{
		Transport transport = GetTransport<T>();
		if (transport != null)
		{
			int mTU = transport.GetMTU(channel);
			if (mTU == -1)
			{
				return mTU;
			}
			return GetMTUWithReserve(mTU);
		}
		return -1;
	}

	internal ArraySegment<byte> ProcessIntermediateIncoming(ArraySegment<byte> src, bool fromServer)
	{
		return _intermediateLayer.HandleIncoming(src, fromServer);
	}

	private ArraySegment<byte> ProcessIntermediateOutgoing(ArraySegment<byte> src, bool toServer)
	{
		return _intermediateLayer.HandleOutgoing(src, toServer);
	}

	internal void SendToClient(byte channelId, ArraySegment<byte> segment, NetworkConnection connection, bool splitLargeMessages = true, DataOrderType orderType = DataOrderType.Default)
	{
		SetSplitValues(channelId, segment, splitLargeMessages, out var requiredMessages, out var maxSplitMessageSize);
		SendToClient(channelId, segment, connection, requiredMessages, maxSplitMessageSize, orderType);
	}

	private void SendToClient(byte channelId, ArraySegment<byte> segment, NetworkConnection connection, int requiredSplitMessages, int maxSplitMessageSize, DataOrderType orderType = DataOrderType.Default)
	{
		if (!(connection == null))
		{
			if (requiredSplitMessages > 1)
			{
				SendSplitData(connection, ref segment, requiredSplitMessages, maxSplitMessageSize, orderType);
			}
			else
			{
				connection.SendToClient(channelId, segment, forceNewBuffer: false, orderType);
			}
		}
	}

	internal void SendToClients(byte channelId, ArraySegment<byte> segment, HashSet<NetworkConnection> observers, HashSet<NetworkConnection> excludedConnections = null, bool splitLargeMessages = true, DataOrderType orderType = DataOrderType.Default)
	{
		SetSplitValues(channelId, segment, splitLargeMessages, out var requiredMessages, out var maxSplitMessageSize);
		SendToClients(channelId, segment, observers, excludedConnections, requiredMessages, maxSplitMessageSize, orderType);
	}

	private void SendToClients(byte channelId, ArraySegment<byte> segment, HashSet<NetworkConnection> observers, HashSet<NetworkConnection> excludedConnections, int requiredSplitMessages, int maxSplitMessageSize, DataOrderType orderType = DataOrderType.Default)
	{
		if (excludedConnections == null || excludedConnections.Count == 0)
		{
			foreach (NetworkConnection observer in observers)
			{
				SendToClient(channelId, segment, observer, requiredSplitMessages, maxSplitMessageSize, orderType);
			}
			return;
		}
		foreach (NetworkConnection observer2 in observers)
		{
			if (!excludedConnections.Contains(observer2))
			{
				SendToClient(channelId, segment, observer2, requiredSplitMessages, maxSplitMessageSize, orderType);
			}
		}
	}

	internal void SendToClients(byte channelId, ArraySegment<byte> segment, bool splitLargeMessages = true)
	{
		SetSplitValues(channelId, segment, splitLargeMessages, out var requiredMessages, out var maxSplitMessageSize);
		SendToClients_Internal(channelId, segment, requiredMessages, maxSplitMessageSize);
	}

	private void SendToClients_Internal(byte channelId, ArraySegment<byte> segment, int requiredSplitMessages, int maxSplitMessageSize)
	{
		foreach (NetworkConnection value in _networkManager.ServerManager.Clients.Values)
		{
			SendToClient(channelId, segment, value, requiredSplitMessages, maxSplitMessageSize);
		}
	}

	internal void SendToServer(byte channelId, ArraySegment<byte> segment, bool splitLargeMessages = true, DataOrderType orderType = DataOrderType.Default)
	{
		SetSplitValues(channelId, segment, splitLargeMessages, out var requiredMessages, out var maxSplitMessageSize);
		SendToServer(channelId, segment, requiredMessages, maxSplitMessageSize, orderType);
	}

	private void SendToServer(byte channelId, ArraySegment<byte> segment, int requiredMessages, int maxSplitMessageSize, DataOrderType orderType)
	{
		if (channelId >= _toServerBundles.Count)
		{
			channelId = 0;
		}
		if (requiredMessages > 1)
		{
			SendSplitData(null, ref segment, requiredMessages, maxSplitMessageSize, orderType);
		}
		else
		{
			_toServerBundles[channelId].Write(segment, forceNewBuffer: false, orderType);
		}
	}

	private void SetSplitValues(byte channelId, ArraySegment<byte> segment, bool split, out int requiredMessages, out int maxSplitMessageSize)
	{
		if (!split)
		{
			requiredMessages = 0;
			maxSplitMessageSize = 0;
		}
		else
		{
			SplitRequired(channelId, segment.Count, out requiredMessages, out maxSplitMessageSize);
		}
	}

	internal void CheckSetReliableChannel(int dataLength, ref Channel channel)
	{
		if (channel != 0 && GetRequiredMessageCount((byte)channel, dataLength, out var _) > 1)
		{
			channel = Channel.Reliable;
		}
	}

	private int GetRequiredMessageCount(byte channelId, int segmentSize, out int maxMessageSize)
	{
		maxMessageSize = GetLowestMTU(channelId) - 10;
		return Mathf.CeilToInt((float)segmentSize / (float)maxMessageSize);
	}

	private bool SplitRequired(byte channelId, int segmentSize, out int requiredMessages, out int maxMessageSize)
	{
		requiredMessages = GetRequiredMessageCount(channelId, segmentSize, out maxMessageSize);
		bool num = requiredMessages > 1;
		if (num && channelId != 0)
		{
			_networkManager.LogError($"A message of length {segmentSize} requires the reliable channel but was sent on channel {(Channel)channelId}. Please file this stack trace as a bug report.");
		}
		return num;
	}

	private void SendSplitData(NetworkConnection conn, ref ArraySegment<byte> segment, int requiredMessages, int maxMessageSize, DataOrderType orderType)
	{
		if (requiredMessages <= 1)
		{
			_networkManager.LogError($"SendSplitData was called with {requiredMessages} required messages. This method should only be called if messages must be split into 2 pieces or more.");
			return;
		}
		byte b = 0;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		pooledWriter.WritePacketIdUnpacked(PacketId.Split);
		pooledWriter.WriteInt32(requiredMessages);
		ArraySegment<byte> arraySegment = pooledWriter.GetArraySegment();
		int i = 0;
		bool flag = true;
		int num2;
		for (; i < segment.Count; i += num2)
		{
			int num = 0;
			if (flag)
			{
				num = arraySegment.Count;
				flag = false;
			}
			num2 = Mathf.Min(segment.Count - i - num, maxMessageSize);
			ArraySegment<byte> segment2 = new ArraySegment<byte>(segment.Array, segment.Offset + i, num2);
			if (conn != null)
			{
				conn.SendToClient(b, arraySegment, forceNewBuffer: true);
				conn.SendToClient(b, segment2);
			}
			else
			{
				_toServerBundles[b].Write(arraySegment, forceNewBuffer: true, orderType);
				_toServerBundles[b].Write(segment2, forceNewBuffer: false, orderType);
			}
		}
		pooledWriter.Store();
	}

	internal void IterateIncoming(bool asServer)
	{
		this.OnIterateIncomingStart?.Invoke(asServer);
		Transport.IterateIncoming(asServer);
		this.OnIterateIncomingEnd?.Invoke(asServer);
	}

	internal void IterateOutgoing(bool asServer)
	{
		int channelCount;
		ulong sentBytes;
		if (!asServer || !_networkManager.ServerManager.AreAllServersStopped())
		{
			this.OnIterateOutgoingStart?.Invoke();
			channelCount = 2;
			sentBytes = 0uL;
			if (asServer)
			{
				SendAsServer();
			}
			else
			{
				SendAsClient();
			}
			Transport.IterateOutgoing(asServer);
			this.OnIterateOutgoingEnd?.Invoke();
		}
		void SendAsClient()
		{
			byte channel;
			for (channel = 0; channel < channelCount; channel++)
			{
				if (PacketBundle.GetPacketBundle(channel, _toServerBundles, out var mtuBuffer))
				{
					ProcessPacketBundle(mtuBuffer);
					ProcessPacketBundle(mtuBuffer.GetSendLastBundle());
				}
			}
			if (_networkTrafficStatistics != null)
			{
				_networkTrafficStatistics.AddOutboundSocketData(sentBytes, asServer: false);
			}
			void ProcessPacketBundle(PacketBundle ppb)
			{
				for (int i = 0; i < ppb.WrittenBuffers; i++)
				{
					if (ppb.GetBuffer(i, out var bb))
					{
						ArraySegment<byte> arraySegment = new ArraySegment<byte>(bb.Data, 0, bb.Length);
						if (HasIntermediateLayer)
						{
							arraySegment = ProcessIntermediateOutgoing(arraySegment, toServer: true);
						}
						Transport.SendToServer(channel, arraySegment);
						sentBytes += (ulong)arraySegment.Count;
					}
				}
				ppb.Reset(resetSendLast: false);
			}
		}
		void SendAsServer()
		{
			TimeManager timeManager = _networkManager.TimeManager;
			uint localTick = timeManager.LocalTick;
			_networkManager.ServerManager.Objects.WriteDirtySyncTypes();
			int count = _dirtyToClients.Count;
			for (int j = 0; j < count; j++)
			{
				NetworkConnection conn = _dirtyToClients[j];
				byte channel2;
				if (!(conn == null) && conn.IsValid)
				{
					for (channel2 = 0; channel2 < channelCount; channel2++)
					{
						if (conn.GetPacketBundle(channel2, out var packetBundle))
						{
							ProcessPacketBundle(packetBundle);
							ProcessPacketBundle(packetBundle.GetSendLastBundle(), isLast: true);
						}
					}
					if (conn.Disconnecting)
					{
						uint val = timeManager.TimeToTicks(0.1, TickRounding.RoundUp);
						val = Math.Max(val, 2u);
						_disconnectingClients.Add(new DisconnectingClient(val + localTick, conn));
					}
					conn.ResetServerDirty();
				}
				void ProcessPacketBundle(PacketBundle ppb, bool isLast = false)
				{
					for (int l = 0; l < ppb.WrittenBuffers; l++)
					{
						if (ppb.GetBuffer(l, out var bb2))
						{
							ArraySegment<byte> arraySegment2 = new ArraySegment<byte>(bb2.Data, 0, bb2.Length);
							if (HasIntermediateLayer)
							{
								arraySegment2 = ProcessIntermediateOutgoing(arraySegment2, toServer: false);
							}
							Transport.SendToClient(channel2, arraySegment2, conn.ClientId);
							sentBytes += (ulong)arraySegment2.Count;
						}
					}
					ppb.Reset(resetSendLast: false);
				}
			}
			for (int k = 0; k < _disconnectingClients.Count; k++)
			{
				DisconnectingClient disconnectingClient = _disconnectingClients[k];
				if (localTick >= disconnectingClient.Tick)
				{
					_networkManager.TransportManager.Transport.StopConnection(disconnectingClient.Connection.ClientId, immediately: true);
					_disconnectingClients.RemoveAt(k);
					k--;
				}
			}
			if (_networkTrafficStatistics != null)
			{
				_networkTrafficStatistics.AddOutboundSocketData(sentBytes, asServer: true);
			}
			if (count == _dirtyToClients.Count)
			{
				_dirtyToClients.Clear();
			}
			else if (count > 0)
			{
				_dirtyToClients.RemoveRange(0, count);
			}
		}
	}
}
