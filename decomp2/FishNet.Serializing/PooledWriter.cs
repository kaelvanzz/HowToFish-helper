using System;

namespace FishNet.Serializing;

public sealed class PooledWriter : Writer
{
	public void Store()
	{
		WriterPool.Store(this);
	}

	public void StoreLength()
	{
		WriterPool.StoreLength(this);
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
