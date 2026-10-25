using FishNet.Connection;
using FishNet.Serializing;
using FishNet.Transporting;

namespace FishNet.Broadcast.Helping;

public abstract class BroadcastHandlerBase
{
	protected int IteratingIndex;

	public virtual bool RequireAuthentication => false;

	public abstract void RegisterHandler(object obj);

	public abstract void UnregisterHandler(object obj);

	public virtual void InvokeHandlers(PooledReader reader, Channel channel)
	{
	}

	public virtual void InvokeHandlers(NetworkConnection conn, PooledReader reader, Channel channel)
	{
	}
}
