namespace FishNet.Transporting;

public struct ClientConnectionStateArgs
{
	public LocalConnectionState ConnectionState;

	public int TransportIndex;

	public ClientConnectionStateArgs(LocalConnectionState connectionState, int transportIndex)
	{
		ConnectionState = connectionState;
		TransportIndex = transportIndex;
	}
}
