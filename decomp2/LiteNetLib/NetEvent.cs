using System.Net;
using System.Net.Sockets;

namespace LiteNetLib;

internal sealed class NetEvent
{
	public enum EType
	{
		Connect,
		Disconnect,
		Receive,
		ReceiveUnconnected,
		Error,
		ConnectionLatencyUpdated,
		Broadcast,
		ConnectionRequest,
		MessageDelivered,
		PeerAddressChanged
	}

	public NetEvent Next;

	public EType Type;

	public NetPeer Peer;

	public IPEndPoint RemoteEndPoint;

	public object UserData;

	public int Latency;

	public SocketError ErrorCode;

	public DisconnectReason DisconnectReason;

	public ConnectionRequest ConnectionRequest;

	public DeliveryMethod DeliveryMethod;

	public byte ChannelNumber;

	public readonly NetPacketReader DataReader;

	public NetEvent(NetManager manager)
	{
		DataReader = new NetPacketReader(manager, this);
	}
}
