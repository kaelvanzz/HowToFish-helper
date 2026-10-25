using System;
using FishNet.Serializing;
using FishNet.Utility.Performance;

namespace FishNet.Connection;

internal class ByteBuffer
{
	private int _reserve;

	internal int Remaining => Size - Length;

	internal byte[] Data { get; private set; }

	internal int Length { get; private set; }

	internal int Size { get; private set; }

	internal bool HasData { get; private set; }

	internal ByteBuffer(int size, int reserve = 0)
	{
		Data = ByteArrayPool.Retrieve(size);
		Size = size;
		_reserve = reserve;
		Reset();
	}

	public void Dispose()
	{
		if (Data != null)
		{
			ByteArrayPool.Store(Data);
		}
		Data = null;
	}

	internal void Reset()
	{
		Length = _reserve;
		HasData = false;
	}

	internal void CopySegment(uint tick, ArraySegment<byte> segment)
	{
		if (!HasData)
		{
			int position = 0;
			Writer.WriteUInt32Unpacked(Data, tick, ref position);
		}
		Buffer.BlockCopy(segment.Array, segment.Offset, Data, Length, segment.Count);
		Length += segment.Count;
		HasData = true;
	}

	internal void CopySegment(ArraySegment<byte> segment)
	{
		Buffer.BlockCopy(segment.Array, segment.Offset, Data, Length, segment.Count);
		Length += segment.Count;
		HasData = true;
	}
}
