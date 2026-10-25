namespace FishNet.Transporting;

public struct ServerConnectionStateArgs
{
	public int TransportIndex;

	public LocalConnectionState ConnectionState;

	public ServerConnectionStateArgs(LocalConnectionState connectionState, int transportIndex)
	{
		ConnectionState = connectionState;
		TransportIndex = transportIndex;
	}
}
