using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using FishNet.Managing;
using LiteNetLib;
using LiteNetLib.Layers;

namespace FishNet.Transporting.Tugboat.Client;

public class ClientSocket : CommonSocket
{
	private string _address = string.Empty;

	private ushort _port;

	private int _mtu;

	private ConcurrentQueue<Packet> _incoming = new ConcurrentQueue<Packet>();

	private Queue<Packet> _outgoing = new Queue<Packet>();

	private int _timeout;

	private PacketLayerBase _packetLayer;

	~ClientSocket()
	{
		StopConnection();
	}

	internal void Initialize(Transport t, int unreliableMTU, PacketLayerBase packetLayer)
	{
		Transport = t;
		_mtu = unreliableMTU;
		_packetLayer = packetLayer;
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
		eventBasedNetListener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
		eventBasedNetListener.PeerConnectedEvent += Listener_PeerConnectedEvent;
		eventBasedNetListener.PeerDisconnectedEvent += Listener_PeerDisconnectedEvent;
		NetManager = new NetManager(eventBasedNetListener, _packetLayer, useSocketFix: false);
		NetManager.DontRoute = ((Tugboat)Transport).DontRoute;
		NetManager.MtuOverride = _mtu + 10;
		UpdateTimeout(_timeout);
		LocalConnectionStates.Enqueue(LocalConnectionState.Starting);
		NetManager.Start();
		NetManager.Connect(_address, _port, string.Empty);
	}

	internal bool StartConnection(string address, ushort port)
	{
		if (GetConnectionState() != LocalConnectionState.Stopped)
		{
			StopSocket();
		}
		LocalConnectionStates.Enqueue(LocalConnectionState.Starting);
		IterateIncoming();
		_port = port;
		_address = address;
		ResetQueues();
		Task.Run((Action)ThreadedSocket);
		return true;
	}

	internal bool StopConnection(DisconnectInfo? info = null)
	{
		if (GetConnectionState() == LocalConnectionState.Stopped || GetConnectionState() == LocalConnectionState.Stopping)
		{
			return false;
		}
		if (info.HasValue)
		{
			Transport.NetworkManager.Log($"Local client disconnect reason: {info.Value.Reason}.");
		}
		SetConnectionState(LocalConnectionState.Stopping, asServer: false);
		StopSocket();
		return true;
	}

	private void ResetQueues()
	{
		ClearGenericQueue(ref LocalConnectionStates);
		ClearPacketQueue(ref _incoming);
		ClearPacketQueue(ref _outgoing);
	}

	private void Listener_PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
	{
		StopConnection(disconnectInfo);
	}

	private void Listener_PeerConnectedEvent(NetPeer peer)
	{
		LocalConnectionStates.Enqueue(LocalConnectionState.Started);
	}

	private void Listener_NetworkReceiveEvent(NetPeer fromPeer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
	{
		base.Listener_NetworkReceiveEvent(_incoming, fromPeer, reader, deliveryMethod, _mtu);
	}

	private void DequeueOutgoing()
	{
		NetPeer netPeer = null;
		if (NetManager != null)
		{
			netPeer = NetManager.FirstPeer;
		}
		if (netPeer == null)
		{
			ClearPacketQueue(ref _outgoing);
			return;
		}
		int count = _outgoing.Count;
		for (int i = 0; i < count; i++)
		{
			Packet packet = _outgoing.Dequeue();
			ArraySegment<byte> arraySegment = packet.GetArraySegment();
			DeliveryMethod options = ((packet.Channel == 0) ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable);
			if (packet.Channel == 1 && arraySegment.Count > _mtu)
			{
				Transport.NetworkManager.LogWarning($"Client is sending of {arraySegment.Count} length on the unreliable channel, while the MTU is only {_mtu}. The channel has been changed to reliable for this send.");
				options = DeliveryMethod.ReliableOrdered;
			}
			netPeer.Send(arraySegment.Array, arraySegment.Offset, arraySegment.Count, options);
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
			SetConnectionState(result, asServer: false);
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
		Packet result2;
		while (_incoming.TryDequeue(out result2))
		{
			ClientReceivedDataArgs receivedDataArgs = new ClientReceivedDataArgs(result2.GetArraySegment(), (Channel)result2.Channel, Transport.Index);
			Transport.HandleClientReceivedDataArgs(receivedDataArgs);
			result2.Dispose();
		}
	}

	internal void SendToServer(byte channelId, ArraySegment<byte> segment)
	{
		if (GetConnectionState() == LocalConnectionState.Started)
		{
			Send(ref _outgoing, channelId, segment, -1, _mtu);
		}
	}
}
