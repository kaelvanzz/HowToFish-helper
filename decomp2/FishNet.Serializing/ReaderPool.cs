using System;
using System.Collections.Generic;
using FishNet.Managing;

namespace FishNet.Serializing;

public static class ReaderPool
{
	private static readonly Stack<PooledReader> _pool = new Stack<PooledReader>();

	public static PooledReader Retrieve(byte[] bytes, NetworkManager networkManager, Reader.DataSource source = Reader.DataSource.Unset)
	{
		return Retrieve(new ArraySegment<byte>(bytes), networkManager, source);
	}

	public static PooledReader Retrieve(ArraySegment<byte> segment, NetworkManager networkManager, Reader.DataSource source = Reader.DataSource.Unset)
	{
		if (_pool.TryPop(out var result))
		{
			result.Initialize(segment, networkManager, source);
			return result;
		}
		return new PooledReader(segment, networkManager, source);
	}

	public static void Store(PooledReader reader)
	{
		_pool.Push(reader);
	}

	public static void StoreAndDefault(ref PooledReader reader)
	{
		if (reader != null)
		{
			_pool.Push(reader);
			reader = null;
		}
	}
}
