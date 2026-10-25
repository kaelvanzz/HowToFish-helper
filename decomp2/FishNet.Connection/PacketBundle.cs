using System;
using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;

namespace FishNet.Connection;

internal class PacketBundle
{
	private List<ByteBuffer> _buffers = new List<ByteBuffer>();

	private int _bufferIndex;

	private int _maximumTransportUnit;

	private int _reserve;

	private NetworkManager _networkManager;

	private PacketBundle _sendLastBundle;

	private bool _isSendLastBundle;

	internal bool HasData
	{
		get
		{
			if (!_buffers[0].HasData)
			{
				if (!_isSendLastBundle)
				{
					return _sendLastBundle.HasData;
				}
				return false;
			}
			return true;
		}
	}

	public int WrittenBuffers
	{
		get
		{
			if (HasData)
			{
				return _bufferIndex + 1;
			}
			return 0;
		}
	}

	internal PacketBundle(NetworkManager manager, int mtu, int reserve = 0, DataOrderType orderType = DataOrderType.Default)
	{
		_isSendLastBundle = orderType == DataOrderType.Last;
		if (!_isSendLastBundle)
		{
			_sendLastBundle = new PacketBundle(manager, mtu, reserve, DataOrderType.Last);
		}
		_networkManager = manager;
		_maximumTransportUnit = mtu;
		reserve += 4;
		_reserve = reserve;
		AddBuffer();
		Reset(resetSendLast: false);
	}

	public void Dispose()
	{
		for (int i = 0; i < _buffers.Count; i++)
		{
			_buffers[i].Dispose();
		}
		_sendLastBundle?.Dispose();
	}

	private ByteBuffer AddBuffer()
	{
		ByteBuffer byteBuffer = new ByteBuffer(_maximumTransportUnit, _reserve);
		_buffers.Add(byteBuffer);
		return byteBuffer;
	}

	internal void Reset(bool resetSendLast)
	{
		_bufferIndex = 0;
		for (int i = 0; i < _buffers.Count; i++)
		{
			_buffers[i].Reset();
		}
		if (resetSendLast)
		{
			_sendLastBundle.Reset(resetSendLast: false);
		}
	}

	internal void Write(ArraySegment<byte> segment, bool forceNewBuffer = false, DataOrderType orderType = DataOrderType.Default)
	{
		if (!_isSendLastBundle && orderType == DataOrderType.Last)
		{
			_sendLastBundle.Write(segment, forceNewBuffer, orderType);
		}
		else
		{
			if (segment.Count == 0)
			{
				return;
			}
			if (segment.Count > _maximumTransportUnit)
			{
				_networkManager.LogError($"Segment is length of {segment.Count} while MTU is {_maximumTransportUnit}. Packet was not split properly and will not be sent.");
				return;
			}
			ByteBuffer byteBuffer = _buffers[_bufferIndex];
			if ((forceNewBuffer && byteBuffer.Length > _reserve) || segment.Count > byteBuffer.Remaining)
			{
				_bufferIndex++;
				if (_buffers.Count <= _bufferIndex)
				{
					byteBuffer = AddBuffer();
				}
				else
				{
					byteBuffer = _buffers[_bufferIndex];
					byteBuffer.Reset();
				}
			}
			uint localTick = _networkManager.TimeManager.LocalTick;
			byteBuffer.CopySegment(localTick, segment);
		}
	}

	internal PacketBundle GetSendLastBundle()
	{
		return _sendLastBundle;
	}

	internal bool GetBuffer(int index, out ByteBuffer bb)
	{
		bb = null;
		if (index >= _buffers.Count || index < 0)
		{
			_networkManager.LogError($"Index of {index} is out of bounds. There are {_buffers.Count} available.");
			return false;
		}
		if (index > _bufferIndex)
		{
			_networkManager.LogError($"Index of {index} exceeds the number of written buffers. There are {WrittenBuffers} written buffers.");
			return false;
		}
		bb = _buffers[index];
		return bb.HasData;
	}

	internal static bool GetPacketBundle(int channel, List<PacketBundle> bundles, out PacketBundle mtuBuffer)
	{
		if (channel >= bundles.Count)
		{
			mtuBuffer = null;
			return false;
		}
		mtuBuffer = bundles[channel];
		return mtuBuffer.HasData;
	}
}
