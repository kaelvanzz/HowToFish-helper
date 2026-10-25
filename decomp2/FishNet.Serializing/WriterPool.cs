using System.Collections.Generic;
using FishNet.Managing;
using UnityEngine;

namespace FishNet.Serializing;

public static class WriterPool
{
	private static readonly Stack<PooledWriter> _pool = new Stack<PooledWriter>();

	private static readonly Dictionary<int, Stack<PooledWriter>> _lengthPool = new Dictionary<int, Stack<PooledWriter>>();

	internal const int LENGTH_BRACKET = 1000;

	public static PooledWriter Retrieve(NetworkManager networkManager)
	{
		if (!_pool.TryPop(out var result))
		{
			result = new PooledWriter();
		}
		result.Clear(networkManager);
		return result;
	}

	public static PooledWriter Retrieve()
	{
		return Retrieve(null);
	}

	public static PooledWriter Retrieve(int length)
	{
		return Retrieve(null, length);
	}

	public static PooledWriter Retrieve(NetworkManager networkManager, int length)
	{
		int dictionaryIndex = GetDictionaryIndex(length);
		if (_lengthPool.TryGetValue(dictionaryIndex, out var value) && value.TryPop(out var result))
		{
			result.Clear(networkManager);
		}
		else
		{
			result = Retrieve(networkManager);
			int count = (dictionaryIndex + 1) * 1000;
			result.EnsureBufferCapacity(count);
		}
		return result;
	}

	public static void StoreLength(PooledWriter writer)
	{
		int dictionaryIndex = GetDictionaryIndex(writer);
		if (!_lengthPool.TryGetValue(dictionaryIndex, out var value))
		{
			value = new Stack<PooledWriter>();
			_lengthPool[dictionaryIndex] = value;
		}
		value.Push(writer);
	}

	public static void Store(PooledWriter writer)
	{
		_pool.Push(writer);
	}

	public static void StoreAndDefault(ref PooledWriter writer)
	{
		if (writer != null)
		{
			_pool.Push(writer);
			writer = null;
		}
	}

	private static int GetDictionaryIndex(int length)
	{
		int num = Mathf.FloorToInt(length / 1000);
		if (num > 0 && length % 1000 == 0)
		{
			num--;
		}
		return num;
	}

	private static int GetDictionaryIndex(PooledWriter writer)
	{
		int num = writer.Capacity;
		if (num < 1000)
		{
			num = 1000;
			writer.EnsureBufferCapacity(1000);
		}
		return Mathf.FloorToInt(num / 1000) - 1;
	}
}
