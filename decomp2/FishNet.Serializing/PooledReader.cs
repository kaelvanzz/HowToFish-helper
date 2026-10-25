using System;
using FishNet.Managing;

namespace FishNet.Serializing;

public sealed class PooledReader : Reader
{
	public PooledReader()
	{
	}

	internal PooledReader(byte[] bytes, NetworkManager networkManager, DataSource source = DataSource.Unset)
		: base(bytes, networkManager, null, source)
	{
	}

	internal PooledReader(ArraySegment<byte> segment, NetworkManager networkManager, DataSource source = DataSource.Unset)
		: base(segment, networkManager, null, source)
	{
	}

	public void Store()
	{
		ReaderPool.Store(this);
	}

	[Obsolete("Use Clear instead.")]
	public void ResetState()
	{
		Clear();
	}

	[Obsolete("This does not function.")]
	public void InitializeState()
	{
	}
}
