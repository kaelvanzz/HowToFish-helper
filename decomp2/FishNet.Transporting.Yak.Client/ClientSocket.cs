using System.Collections.Generic;
using FishNet.Transporting.Yak.Server;

namespace FishNet.Transporting.Yak.Client;

public class ClientSocket : CommonSocket
{
	private ServerSocket _server;

	private Queue<LocalPacket> _incoming = new Queue<LocalPacket>();

	internal bool StartConnection()
	{
		return true;
	}

	internal bool StopConnection()
	{
		return true;
	}
}
