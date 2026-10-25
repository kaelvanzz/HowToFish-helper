using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FishNet.Managing;
using LiteNetLib;
using LiteNetLib.Layers;

namespace FishNet.Transporting.Tugboat.Server;

public class ServerSocket : CommonSocket
{
	private ushort _port;

	private int _maximumClients;

	private int _mtu;

	private ConcurrentQueue<Packet> _incoming = new ConcurrentQueue<Packet>();

	private Queue<Packet> _outgoing = new Queue<Packet>();

	private ConcurrentQueue<RemoteConnectionEvent> _remoteConnectionEvents = new ConcurrentQueue<RemoteConnectionEvent>();

	private int _timeout;

	private string _ipv4BindAddress;

	private string _ipv6BindAddress;

	private PacketLayerBase _packetLayer;

	private bool _enableIPv6;

	internal RemoteConnectionState GetConnectionState(int connectionId)
	{
		NetPeer netPeer = GetNetPeer(connectionId, connectedOnly: false);
		if (netPeer == null || netPeer.ConnectionState != ConnectionState.Connected)
		{
			return RemoteConnectionState.Stopped;
		}
		return RemoteConnectionState.Started;
	}

	~ServerSocket()
	{
		StopConnection();
	}

	internal void Initialize(Transport t, int unreliableMTU, PacketLayerBase packetLayer, bool enableIPv6)
	{
		Transport = t;
		_mtu = unreliableMTU;
		_packetLayer = packetLayer;
		_enableIPv6 = enableIPv6;
	}

	internal void UpdateTimeout(int timeout)
	{
		_timeout = timeout;
		UpdateTimeout(NetManager, timeout);
	}

	internal void PollSocket()
	{
		PollSocket(NetManager);
	}

	private void ThreadedSocket()
	{
		EventBasedNetListener eventBasedNetListener = new EventBasedNetListener();
		eventBasedNetListener.ConnectionRequestEvent += Listener_ConnectionRequestEvent;
		eventBasedNetListener.PeerConnectedEvent += Listener_PeerConnectedEvent;
		eventBasedNetListener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
		eventBasedNetListener.PeerDisconnectedEvent += Listener_PeerDisconnectedEvent;
		NetManager = new NetManager(eventBasedNetListener, _packetLayer, useSocketFix: false);
		NetManager.DontRoute = ((Tugboat)Transport).DontRoute;
		NetManager.ReuseAddress = ((Tugboat)Transport).ReuseAddress;
		NetManager.MtuOverride = _mtu + 10;
		UpdateTimeout(_timeout);
		IPAddress address = null;
		IPAddress address2 = null;
		if (!string.IsNullOrEmpty(_ipv4BindAddress))
		{
			if (!IPAddress.TryParse(_ipv4BindAddress, out address))
			{
				address = null;
			}
			if (address == null)
			{
				IPHostEntry hostEntry = Dns.GetHostEntry(_ipv4BindAddress);
				if (hostEntry.AddressList.Length != 0)
				{
					address = hostEntry.AddressList[0];
					Transport.NetworkManager.Log("IPv4 could not parse correctly but was resolved to " + address.ToString());
				}
			}
		}
		else
		{
			IPAddress.TryParse("0.0.0.0", out address);
		}
		if (_enableIPv6 && !string.IsNullOrEmpty(_ipv6BindAddress))
		{
			if (!IPAddress.TryParse(_ipv6BindAddress, out address2))
			{
				address2 = null;
			}
		}
		else
		{
			IPAddress.TryParse("0:0:0:0:0:0:0:0", out address2);
		}
		string text = ((address == null) ? ("IPv4 address " + _ipv4BindAddress + " failed to parse. ") : string.Empty);
		string text2 = ((_enableIPv6 && address2 == null) ? ("IPv6 address " + _ipv6BindAddress + " failed to parse. ") : string.Empty);
		if (text != string.Empty || text2 != string.Empty)
		{
			Transport.NetworkManager.Log(text + text2 + "Clear the bind address field to use any bind address.");
			StopConnection();
			return;
		}
		NetManager.IPv6Enabled = _enableIPv6;
		if (NetManager.Start(address, address2, _port))
		{
			LocalConnectionStates.Enqueue(LocalConnectionState.Started);
			return;
		}
		Transport.NetworkManager.LogError("Server failed to start. This usually occurs when the specified port is unavailable, be it closed or already in use.");
		StopConnection();
	}

	internal string GetConnectionAddress(int connectionId)
	{
		if (GetConnectionState() != LocalConnectionState.Started)
		{
			NetworkManager networkManager = ((Transport == null) ? null : Transport.NetworkManager);
			string message = "Server socket is not started.";
			networkManager.LogWarning(message);
			return string.Empty;
		}
		NetPeer netPeer = GetNetPeer(connectionId, connectedOnly: false);
		if (netPeer == null)
		{
			Transport.NetworkManager.LogWarning($"Connection Id {connectionId} returned a null NetPeer.");
			return string.Empty;
		}
		return netPeer.Address.ToString();
	}

	private NetPeer GetNetPeer(int connectionId, bool connectedOnly)
	{
		if (NetManager != null)
		{
			NetPeer netPeer = NetManager.GetPeerById(connectionId);
			if (connectedOnly && netPeer != null && netPeer.ConnectionState != ConnectionState.Connected)
			{
				netPeer = null;
			}
			return netPeer;
		}
		return null;
	}

	internal bool StartConnection(ushort port, int maximumClients, string ipv4BindAddress, string ipv6BindAddress)
	{
		if (GetConnectionState() != LocalConnectionState.Stopped)
		{
			StopSocket();
		}
		LocalConnectionStates.Enqueue(LocalConnectionState.Starting);
		IterateIncoming();
		_port = port;
		_maximumClients = maximumClients;
		_ipv4BindAddress = ipv4BindAddress;
		_ipv6BindAddress = ipv6BindAddress;
		ResetQueues();
		Task.Run((Action)ThreadedSocket);
		return true;
	}

	internal bool StopConnection()
	{
		if (NetManager == null || GetConnectionState() == LocalConnectionState.Stopped || GetConnectionState() == LocalConnectionState.Stopping)
		{
			return false;
		}
		LocalConnectionStates.Enqueue(LocalConnectionState.Stopping);
		StopSocket();
		return true;
	}

	internal bool StopConnection(int connectionId)
	{
		if (NetManager == null || GetConnectionState() != LocalConnectionState.Started)
		{
			return false;
		}
		NetPeer netPeer = GetNetPeer(connectionId, connectedOnly: false);
		if (netPeer == null)
		{
			return false;
		}
		try
		{
			netPeer.Disconnect();
		}
		catch
		{
			return false;
		}
		return true;
	}

	private void ResetQueues()
	{
		ClearGenericQueue(ref LocalConnectionStates);
		ClearPacketQueue(ref _incoming);
		ClearPacketQueue(ref _outgoing);
		ClearGenericQueue(ref _remoteConnectionEvents);
	}

	private void Listener_PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
	{
		_remoteConnectionEvents.Enqueue(new RemoteConnectionEvent(connected: false, peer.Id));
	}

	private void Listener_PeerConnectedEvent(NetPeer peer)
	{
		_remoteConnectionEvents.Enqueue(new RemoteConnectionEvent(connected: true, peer.Id));
	}

	private void Listener_NetworkReceiveEvent(NetPeer fromPeer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
	{
		if (reader.AvailableBytes > _mtu)
		{
			_remoteConnectionEvents.Enqueue(new RemoteConnectionEvent(connected: false, fromPeer.Id));
			fromPeer.Disconnect();
		}
		else
		{
			base.Listener_NetworkReceiveEvent(_incoming, fromPeer, reader, deliveryMethod, _mtu);
		}
	}

	private void Listener_ConnectionRequestEvent(ConnectionRequest request)
	{
		if (NetManager != null)
		{
			if (NetManager.ConnectedPeersCount >= _maximumClients)
			{
				request.Reject();
			}
			else
			{
				request.AcceptIfKey(string.Empty);
			}
		}
	}

	private void DequeueOutgoing()
	{
		if (GetConnectionState() != LocalConnectionState.Started || NetManager == null)
		{
			ClearPacketQueue(ref _outgoing);
			return;
		}
		int count = _outgoing.Count;
		for (int i = 0; i < count; i++)
		{
			Packet packet = _outgoing.Dequeue();
			int connectionId = packet.ConnectionId;
			ArraySegment<byte> arraySegment = packet.GetArraySegment();
			DeliveryMethod options = ((packet.Channel == 0) ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable);
			if (packet.Channel == 1 && arraySegment.Count > _mtu)
			{
				Transport.NetworkManager.LogWarning($"Server is sending of {arraySegment.Count} length on the unreliable channel, while the MTU is only {_mtu}. The channel has been changed to reliable for this send.");
				options = DeliveryMethod.ReliableOrdered;
			}
			if (connectionId == -1)
			{
				NetManager.SendToAll(arraySegment.Array, arraySegment.Offset, arraySegment.Count, options);
			}
			else
			{
				GetNetPeer(connectionId, connectedOnly: true)?.Send(arraySegment.Array, arraySegment.Offset, arraySegment.Count, options);
			}
			packet.Dispose();
		}
	}

	internal void IterateOutgoing()
	{
		DequeueOutgoing();
	}

	internal void IterateIncoming()
	{
		LocalConnectionState result;
		while (LocalConnectionStates.TryDequeue(out result))
		{
			SetConnectionState(result, asServer: true);
		}
		LocalConnectionState connectionState = GetConnectionState();
		if (connectionState != LocalConnectionState.Started)
		{
			ResetQueues();
			if (connectionState == LocalConnectionState.Stopped)
			{
				StopSocket();
				return;
			}
		}
		RemoteConnectionEvent result2;
		while (_remoteConnectionEvents.TryDequeue(out result2))
		{
			RemoteConnectionState connectionState2 = (result2.Connected ? RemoteConnectionState.Started : RemoteConnectionState.Stopped);
			Transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(connectionState2, result2.ConnectionId, Transport.Index));
		}
		Packet result3;
		while (_incoming.TryDequeue(out result3))
		{
			if (GetNetPeer(result3.ConnectionId, connectedOnly: true) != null)
			{
				ServerReceivedDataArgs receivedDataArgs = new ServerReceivedDataArgs(result3.GetArraySegment(), (Channel)result3.Channel, result3.ConnectionId, Transport.Index);
				Transport.HandleServerReceivedDataArgs(receivedDataArgs);
			}
			result3.Dispose();
		}
	}

	internal void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
	{
		Send(ref _outgoing, channelId, segment, connectionId, _mtu);
	}

	internal int GetMaximumClients()
	{
		return Math.Min(_maximumClients, 2147483646);
	}

	internal void SetMaximumClients(int value)
	{
		_maximumClients = value;
	}
}
