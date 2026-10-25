using System;

namespace LiteNetLib;

internal sealed class ReliableChannel : BaseChannel
{
	private struct PendingPacket
	{
		private NetPacket _packet;

		private long _timeStamp;

		private bool _isSent;

		public override string ToString()
		{
			if (_packet != null)
			{
				return _packet.Sequence.ToString();
			}
			return "Empty";
		}

		public void Init(NetPacket packet)
		{
			_packet = packet;
			_isSent = false;
		}

		public bool TrySend(long currentTime, NetPeer peer)
		{
			if (_packet == null)
			{
				return false;
			}
			if (_isSent)
			{
				double num = peer.ResendDelay * 10000.0;
				if ((double)(currentTime - _timeStamp) < num)
				{
					return true;
				}
			}
			_timeStamp = currentTime;
			_isSent = true;
			peer.SendUserData(_packet);
			return true;
		}

		public bool Clear(NetPeer peer)
		{
			if (_packet != null)
			{
				peer.RecycleAndDeliver(_packet);
				_packet = null;
				return true;
			}
			return false;
		}
	}

	private readonly NetPacket _outgoingAcks;

	private readonly PendingPacket[] _pendingPackets;

	private readonly NetPacket[] _receivedPackets;

	private readonly bool[] _earlyReceived;

	private int _localSeqence;

	private int _remoteSequence;

	private int _localWindowStart;

	private int _remoteWindowStart;

	private bool _mustSendAcks;

	private readonly DeliveryMethod _deliveryMethod;

	private readonly bool _ordered;

	private readonly int _windowSize;

	private const int BitsInByte = 8;

	private readonly byte _id;

	public ReliableChannel(NetPeer peer, bool ordered, byte id)
		: base(peer)
	{
		_id = id;
		_windowSize = 64;
		_ordered = ordered;
		_pendingPackets = new PendingPacket[_windowSize];
		for (int i = 0; i < _pendingPackets.Length; i++)
		{
			_pendingPackets[i] = default(PendingPacket);
		}
		if (_ordered)
		{
			_deliveryMethod = DeliveryMethod.ReliableOrdered;
			_receivedPackets = new NetPacket[_windowSize];
		}
		else
		{
			_deliveryMethod = DeliveryMethod.ReliableUnordered;
			_earlyReceived = new bool[_windowSize];
		}
		_localWindowStart = 0;
		_localSeqence = 0;
		_remoteSequence = 0;
		_remoteWindowStart = 0;
		_outgoingAcks = new NetPacket(PacketProperty.Ack, (_windowSize - 1) / 8 + 2)
		{
			ChannelId = id
		};
	}

	private void ProcessAck(NetPacket packet)
	{
		if (packet.Size != _outgoingAcks.Size)
		{
			return;
		}
		ushort sequence = packet.Sequence;
		int num = NetUtils.RelativeSequenceNumber(_localWindowStart, sequence);
		if (sequence >= 32768 || num < 0 || num >= _windowSize)
		{
			return;
		}
		byte[] rawData = packet.RawData;
		lock (_pendingPackets)
		{
			int num2 = _localWindowStart;
			while (num2 != _localSeqence && NetUtils.RelativeSequenceNumber(num2, sequence) < _windowSize)
			{
				int num3 = num2 % _windowSize;
				int num4 = 4 + num3 / 8;
				int num5 = num3 % 8;
				if ((rawData[num4] & (1 << num5)) == 0)
				{
					if (Peer.NetManager.EnableStatistics)
					{
						Peer.Statistics.IncrementPacketLoss();
						Peer.NetManager.Statistics.IncrementPacketLoss();
					}
				}
				else
				{
					if (num2 == _localWindowStart)
					{
						_localWindowStart = (_localWindowStart + 1) % 32768;
					}
					_pendingPackets[num3].Clear(Peer);
				}
				num2 = (num2 + 1) % 32768;
			}
		}
	}

	protected override bool SendNextPackets()
	{
		if (_mustSendAcks)
		{
			_mustSendAcks = false;
			lock (_outgoingAcks)
			{
				Peer.SendUserData(_outgoingAcks);
			}
		}
		long ticks = DateTime.UtcNow.Ticks;
		bool flag = false;
		lock (_pendingPackets)
		{
			lock (OutgoingQueue)
			{
				while (OutgoingQueue.Count > 0 && NetUtils.RelativeSequenceNumber(_localSeqence, _localWindowStart) < _windowSize)
				{
					NetPacket netPacket = OutgoingQueue.Dequeue();
					netPacket.Sequence = (ushort)_localSeqence;
					netPacket.ChannelId = _id;
					_pendingPackets[_localSeqence % _windowSize].Init(netPacket);
					_localSeqence = (_localSeqence + 1) % 32768;
				}
			}
			for (int num = _localWindowStart; num != _localSeqence; num = (num + 1) % 32768)
			{
				if (_pendingPackets[num % _windowSize].TrySend(ticks, Peer))
				{
					flag = true;
				}
			}
		}
		if (!flag && !_mustSendAcks)
		{
			return OutgoingQueue.Count > 0;
		}
		return true;
	}

	public override bool ProcessPacket(NetPacket packet)
	{
		if (packet.Property == PacketProperty.Ack)
		{
			ProcessAck(packet);
			return false;
		}
		int sequence = packet.Sequence;
		if (sequence >= 32768)
		{
			return false;
		}
		int num = NetUtils.RelativeSequenceNumber(sequence, _remoteWindowStart);
		if (NetUtils.RelativeSequenceNumber(sequence, _remoteSequence) > _windowSize)
		{
			return false;
		}
		if (num < 0)
		{
			return false;
		}
		if (num >= _windowSize * 2)
		{
			return false;
		}
		int num3;
		lock (_outgoingAcks)
		{
			int num4;
			int num5;
			if (num >= _windowSize)
			{
				int num2 = (_remoteWindowStart + num - _windowSize + 1) % 32768;
				_outgoingAcks.Sequence = (ushort)num2;
				while (_remoteWindowStart != num2)
				{
					num3 = _remoteWindowStart % _windowSize;
					num4 = 4 + num3 / 8;
					num5 = num3 % 8;
					_outgoingAcks.RawData[num4] &= (byte)(~(1 << num5));
					_remoteWindowStart = (_remoteWindowStart + 1) % 32768;
				}
			}
			_mustSendAcks = true;
			num3 = sequence % _windowSize;
			num4 = 4 + num3 / 8;
			num5 = num3 % 8;
			if ((_outgoingAcks.RawData[num4] & (1 << num5)) != 0)
			{
				AddToPeerChannelSendQueue();
				return false;
			}
			_outgoingAcks.RawData[num4] |= (byte)(1 << num5);
		}
		AddToPeerChannelSendQueue();
		if (sequence == _remoteSequence)
		{
			Peer.AddReliablePacket(_deliveryMethod, packet);
			_remoteSequence = (_remoteSequence + 1) % 32768;
			if (_ordered)
			{
				NetPacket p;
				while ((p = _receivedPackets[_remoteSequence % _windowSize]) != null)
				{
					_receivedPackets[_remoteSequence % _windowSize] = null;
					Peer.AddReliablePacket(_deliveryMethod, p);
					_remoteSequence = (_remoteSequence + 1) % 32768;
				}
			}
			else
			{
				while (_earlyReceived[_remoteSequence % _windowSize])
				{
					_earlyReceived[_remoteSequence % _windowSize] = false;
					_remoteSequence = (_remoteSequence + 1) % 32768;
				}
			}
			return true;
		}
		if (_ordered)
		{
			_receivedPackets[num3] = packet;
		}
		else
		{
			_earlyReceived[num3] = true;
			Peer.AddReliablePacket(_deliveryMethod, packet);
		}
		return true;
	}
}
