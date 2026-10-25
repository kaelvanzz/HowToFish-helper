using System.Collections.Generic;
using FishNet.Transporting.Yak.Client;

namespace FishNet.Transporting.Yak.Server;

public class ServerSocket : CommonSocket
{
	private Queue<LocalPacket> _incoming = new Queue<LocalPacket>();

	private ClientSocket _client;

	internal RemoteConnectionState GetConnectionState(int connectionId)
	{
		if (connectionId != int.MaxValue)
		{
			return RemoteConnectionState.Stopped;
		}
		if (_client.GetLocalConnectionState() != LocalConnectionState.Started)
		{
			return RemoteConnectionState.Stopped;
		}
		return RemoteConnectionState.Started;
	}

	internal bool StartConnection()
	{
		return true;
	}

	internal bool StopConnection()
	{
		return true;
	}

	internal bool StopConnection(int connectionId)
	{
		return true;
	}
}
