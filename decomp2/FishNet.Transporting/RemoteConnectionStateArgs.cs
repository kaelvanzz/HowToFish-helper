namespace FishNet.Transporting;

public struct RemoteConnectionStateArgs
{
	public int TransportIndex;

	public RemoteConnectionState ConnectionState;

	public int ConnectionId;

	public RemoteConnectionStateArgs(RemoteConnectionState connectionState, int connectionId, int transportIndex)
	{
		ConnectionState = connectionState;
		ConnectionId = connectionId;
		TransportIndex = transportIndex;
	}
}
