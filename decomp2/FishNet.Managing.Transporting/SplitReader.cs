using System;
using FishNet.Serializing;

namespace FishNet.Managing.Transporting;

internal class SplitReader
{
	private long _tick = -1L;

	private int _expectedMessages;

	private ushort _receivedMessages;

	private PooledWriter _writer = WriterPool.Retrieve();

	internal SplitReader()
	{
		_writer.EnsureBufferCapacity(20000);
	}

	internal void GetHeader(PooledReader reader, out int expectedMessages)
	{
		expectedMessages = reader.ReadInt32();
	}

	internal void Write(uint tick, PooledReader reader, int expectedMessages)
	{
		if (tick != _tick)
		{
			Reset(tick, expectedMessages);
		}
		int num = expectedMessages * 1500;
		if (_writer.Capacity < num)
		{
			_writer.EnsureBufferCapacity(num);
		}
		ArraySegment<byte> value = reader.ReadArraySegment(reader.Remaining);
		_writer.WriteArraySegment(value);
		_receivedMessages++;
	}

	internal ArraySegment<byte> GetFullMessage()
	{
		if (_receivedMessages < _expectedMessages)
		{
			return default(ArraySegment<byte>);
		}
		ArraySegment<byte> arraySegment = _writer.GetArraySegment();
		Reset();
		return arraySegment;
	}

	private void Reset(uint tick = 0u, int expectedMessages = 0)
	{
		_tick = tick;
		_receivedMessages = 0;
		_expectedMessages = expectedMessages;
		_writer.Clear();
	}
}
