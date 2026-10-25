using FishNet.Broadcast;

namespace FishNet.Managing.Server;

public struct ClientConnectionChangeBroadcast : IBroadcast
{
	public bool Connected;

	public int Id;
}
