using System;
using System.Collections.Generic;
using FishNet.Transporting;
using FishNet.Utility.Performance;
using UnityEngine;

namespace FishNet.Managing.Transporting;

[Serializable]
public class LatencySimulator
{
	private struct Message
	{
		public readonly int ConnectionId;

		public readonly byte[] Data;

		public readonly int Length;

		public readonly float SendTime;

		public Message(int connectionId, ArraySegment<byte> segment, float latency)
		{
			ConnectionId = connectionId;
			SendTime = Time.unscaledTime + latency;
			Length = segment.Count;
			Data = ByteArrayPool.Retrieve(Length);
			Buffer.BlockCopy(segment.Array, segment.Offset, Data, 0, Length);
		}

		public ArraySegment<byte> GetSegment()
		{
			return new ArraySegment<byte>(Data, 0, Length);
		}
	}

	[Header("Settings")]
	[Tooltip("True if latency simulator is enabled.")]
	[SerializeField]
	private bool _enabled;

	[Tooltip("True to add latency on clientHost as well.")]
	[SerializeField]
	private bool _simulateHost = true;

	[Tooltip("Milliseconds to add between packets. When acting as host this value will be doubled. Added latency will be a minimum of tick rate.")]
	[Range(0f, 60000f)]
	[SerializeField]
	private long _latency;

	[Header("Unreliable")]
	[Tooltip("Percentage of unreliable packets which should arrive out of order.")]
	[Range(0f, 1f)]
	[SerializeField]
	private double _outOfOrder;

	[Tooltip("Percentage of packets which should drop.")]
	[Range(0f, 1f)]
	[SerializeField]
	private double _packetLoss;

	private Transport _transport;

	private List<Message> _toServerReliable = new List<Message>();

	private List<Message> _toServerUnreliable = new List<Message>();

	private List<Message> _toClientReliable = new List<Message>();

	private List<Message> _toClientUnreliable = new List<Message>();

	private NetworkManager _networkManager;

	private readonly System.Random _random = new System.Random();

	internal bool CanSimulate
	{
		get
		{
			if (GetEnabled())
			{
				if (GetLatency() <= 0 && !(GetPacketLost() > 0.0))
				{
					return GetOutOfOrder() > 0.0;
				}
				return true;
			}
			return false;
		}
	}

	public bool GetEnabled()
	{
		return _enabled;
	}

	public void SetEnabled(bool value)
	{
		if (value != _enabled)
		{
			_enabled = value;
			Reset();
		}
	}

	public long GetLatency()
	{
		return _latency;
	}

	public void SetLatency(long value)
	{
		_latency = value;
	}

	public double GetOutOfOrder()
	{
		return _outOfOrder;
	}

	public void SetOutOfOrder(double value)
	{
		_outOfOrder = value;
	}

	public double GetPacketLost()
	{
		return _packetLoss;
	}

	public void SetPacketLoss(double value)
	{
		_packetLoss = value;
	}

	public void Initialize(NetworkManager manager, Transport transport)
	{
		_networkManager = manager;
		_transport = transport;
	}

	public void Reset()
	{
		bool enabled = GetEnabled();
		if (_transport != null && enabled)
		{
			IterateAndStore(_toServerReliable);
			IterateAndStore(_toServerUnreliable);
			IterateAndStore(_toClientReliable);
			IterateAndStore(_toClientUnreliable);
		}
		_toServerReliable.Clear();
		_toServerUnreliable.Clear();
		_toClientReliable.Clear();
		_toClientUnreliable.Clear();
		void IterateAndStore(List<Message> messages)
		{
			foreach (Message message in messages)
			{
				_transport.SendToServer(0, message.GetSegment());
				ByteArrayPool.Store(message.Data);
			}
		}
	}

	public void RemovePendingForConnection(int connectionId)
	{
		if (GetEnabled())
		{
			RemoveFromCollection(_toServerUnreliable);
			RemoveFromCollection(_toServerUnreliable);
			RemoveFromCollection(_toClientReliable);
			RemoveFromCollection(_toClientUnreliable);
		}
		void RemoveFromCollection(List<Message> c)
		{
			for (int i = 0; i < c.Count; i++)
			{
				if (c[i].ConnectionId == connectionId)
				{
					c.RemoveAt(i);
					i--;
				}
			}
		}
	}

	private float GetLatencyAsFloat()
	{
		return (float)_latency / 1000f;
	}

	public void AddOutgoing(byte channelId, ArraySegment<byte> segment, bool toServer = true, int connectionId = -1)
	{
		if (!_simulateHost && _networkManager != null && _networkManager.IsHostStarted)
		{
			if (toServer)
			{
				_transport.SendToServer(channelId, segment);
				return;
			}
			if (_networkManager.ClientManager.Connection.ClientId == connectionId)
			{
				_transport.SendToClient(channelId, segment, connectionId);
				return;
			}
		}
		Channel channel = (Channel)channelId;
		List<Message> list = ((!toServer) ? ((channel == Channel.Reliable) ? _toClientReliable : _toClientUnreliable) : ((channel == Channel.Reliable) ? _toServerReliable : _toServerUnreliable));
		float num = GetLatencyAsFloat();
		if (DropPacket())
		{
			if (channel != 0)
			{
				return;
			}
			num += num * 0.3f;
		}
		Message item = new Message(connectionId, segment, num);
		int count = list.Count;
		if (channel == Channel.Unreliable && count > 0 && OutOfOrderPacket(channel))
		{
			list.Insert(count - 1, item);
		}
		else
		{
			list.Add(item);
		}
	}

	public void IterateOutgoing(bool asServer)
	{
		if (_transport == null)
		{
			Reset();
			return;
		}
		if (asServer)
		{
			IterateCollection(_toClientReliable, Channel.Reliable);
			IterateCollection(_toClientUnreliable, Channel.Unreliable);
		}
		else
		{
			IterateCollection(_toServerReliable, Channel.Reliable);
			IterateCollection(_toServerUnreliable, Channel.Unreliable);
		}
		_transport.IterateOutgoing(asServer);
		void IterateCollection(List<Message> collection, Channel channel)
		{
			byte channelId = (byte)channel;
			float unscaledTime = Time.unscaledTime;
			int count = collection.Count;
			int num = 0;
			for (int i = 0; i < count; i++)
			{
				Message message = collection[i];
				if (unscaledTime < message.SendTime)
				{
					break;
				}
				if (asServer)
				{
					_transport.SendToClient(channelId, message.GetSegment(), message.ConnectionId);
				}
				else
				{
					_transport.SendToServer(channelId, message.GetSegment());
				}
				num++;
			}
			if (num > 0)
			{
				for (int j = 0; j < num; j++)
				{
					ByteArrayPool.Store(collection[j].Data);
				}
				collection.RemoveRange(0, num);
			}
		}
	}

	private bool DropPacket()
	{
		if (_packetLoss > 0.0)
		{
			return _random.NextDouble() < _packetLoss;
		}
		return false;
	}

	private bool OutOfOrderPacket(Channel c)
	{
		if (c == Channel.Reliable)
		{
			return false;
		}
		if (_outOfOrder > 0.0)
		{
			return _random.NextDouble() < _outOfOrder;
		}
		return false;
	}
}
