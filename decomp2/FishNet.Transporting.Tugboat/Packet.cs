using System;
using FishNet.Utility.Performance;

namespace FishNet.Transporting.Tugboat;

internal struct Packet
{
	public readonly int ConnectionId;

	public readonly byte[] Data;

	public readonly int Length;

	public readonly byte Channel;

	public Packet(int connectionId, byte[] data, int length, byte channel)
	{
		ConnectionId = connectionId;
		Data = data;
		Length = length;
		Channel = channel;
	}

	public Packet(int sender, ArraySegment<byte> segment, byte channel, int mtu)
	{
		int minimumLength = Math.Max(segment.Count, mtu);
		Data = ByteArrayPool.Retrieve(minimumLength);
		Buffer.BlockCopy(segment.Array, segment.Offset, Data, 0, segment.Count);
		ConnectionId = sender;
		Length = segment.Count;
		Channel = channel;
	}

	public ArraySegment<byte> GetArraySegment()
	{
		return new ArraySegment<byte>(Data, 0, Length);
	}

	public void Dispose()
	{
		ByteArrayPool.Store(Data);
	}
}
