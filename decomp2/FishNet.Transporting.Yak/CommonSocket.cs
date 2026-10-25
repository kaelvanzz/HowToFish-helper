using System.Collections.Generic;

namespace FishNet.Transporting.Yak;

public abstract class CommonSocket
{
	private LocalConnectionState _connectionState = LocalConnectionState.Stopped;

	protected Transport Transport;

	internal LocalConnectionState GetLocalConnectionState()
	{
		return _connectionState;
	}

	internal virtual void Initialize(Transport t, CommonSocket socket)
	{
		Transport = t;
	}

	internal void ClearQueue(ref Queue<LocalPacket> queue)
	{
	}
}
