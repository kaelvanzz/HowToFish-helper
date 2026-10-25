using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using FishNet.Utility.Performance;
using LiteNetLib;

namespace FishNet.Transporting.Tugboat;

public abstract class CommonSocket
{
	private LocalConnectionState _connectionState = LocalConnectionState.Stopped;

	internal NetManager NetManager;

	protected ConcurrentQueue<LocalConnectionState> LocalConnectionStates = new ConcurrentQueue<LocalConnectionState>();

	protected Transport Transport;

	private readonly object _stopLock = new object();

	internal LocalConnectionState GetConnectionState()
	{
		return _connectionState;
	}

	protected void SetConnectionState(LocalConnectionState connectionState, bool asServer)
	{
		if (connectionState != _connectionState)
		{
			_connectionState = connectionState;
			if (asServer)
			{
				Transport.HandleServerConnectionState(new ServerConnectionStateArgs(connectionState, Transport.Index));
			}
			else
			{
				Transport.HandleClientConnectionState(new ClientConnectionStateArgs(connectionState, Transport.Index));
			}
		}
	}

	internal void Send(ref Queue<Packet> queue, byte channelId, ArraySegment<byte> segment, int connectionId, int mtu)
	{
		if (GetConnectionState() == LocalConnectionState.Started)
		{
			Packet item = new Packet(connectionId, segment, channelId, mtu);
			queue.Enqueue(item);
		}
	}

	protected void UpdateTimeout(NetManager netManager, int timeout)
	{
		if (netManager != null)
		{
			timeout = ((timeout == 0) ? int.MaxValue : Math.Min(int.MaxValue, timeout * 1000));
			netManager.DisconnectTimeout = timeout;
		}
	}

	internal void ClearGenericQueue<T>(ref ConcurrentQueue<T> queue)
	{
		T result;
		while (queue.TryDequeue(out result))
		{
		}
	}

	internal void ClearPacketQueue(ref ConcurrentQueue<Packet> queue)
	{
		Packet result;
		while (queue.TryDequeue(out result))
		{
			result.Dispose();
		}
	}

	internal void ClearPacketQueue(ref Queue<Packet> queue)
	{
		int count = queue.Count;
		for (int i = 0; i < count; i++)
		{
			queue.Dequeue().Dispose();
		}
	}

	internal virtual void Listener_NetworkReceiveEvent(ConcurrentQueue<Packet> queue, NetPeer fromPeer, NetPacketReader reader, DeliveryMethod deliveryMethod, int mtu)
	{
		int availableBytes = reader.AvailableBytes;
		byte[] array = ByteArrayPool.Retrieve(Math.Max(availableBytes, mtu));
		reader.GetBytes(array, availableBytes);
		int id = fromPeer.Id;
		byte channel = ((deliveryMethod == DeliveryMethod.Unreliable) ? ((byte)1) : ((byte)0));
		Packet item = new Packet(id, array, availableBytes, channel);
		queue.Enqueue(item);
		reader.Recycle();
	}

	internal void PollSocket(NetManager nm)
	{
		nm?.PollEvents();
	}

	protected void StopSocket()
	{
		if (NetManager == null)
		{
			return;
		}
		if (Transport is Tugboat tugboat && tugboat.StopSocketsOnThread)
		{
			Task.Run(delegate
			{
				lock (_stopLock)
				{
					NetManager?.Stop();
					NetManager = null;
				}
				if (GetConnectionState() != LocalConnectionState.Stopped)
				{
					LocalConnectionStates.Enqueue(LocalConnectionState.Stopped);
				}
			});
		}
		else
		{
			NetManager?.Stop();
			NetManager = null;
			if (GetConnectionState() != LocalConnectionState.Stopped)
			{
				LocalConnectionStates.Enqueue(LocalConnectionState.Stopped);
			}
		}
	}

	internal ushort? GetPort()
	{
		if (NetManager == null || !NetManager.IsRunning)
		{
			return null;
		}
		int num = NetManager.LocalPort;
		if (num < 0)
		{
			num = 0;
		}
		else if (num > 65535)
		{
			num = 65535;
		}
		return (ushort)num;
	}
}
