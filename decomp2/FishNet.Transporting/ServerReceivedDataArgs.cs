using System;

namespace FishNet.Transporting;

public struct ServerReceivedDataArgs
{
	public ArraySegment<byte> Data;

	public Channel Channel;

	public int ConnectionId;

	public int TransportIndex;

	public Action FinalizeMethod;

	public ServerReceivedDataArgs(ArraySegment<byte> data, Channel channel, int connectionId, int transportIndex)
	{
		Data = data;
		Channel = channel;
		ConnectionId = connectionId;
		TransportIndex = transportIndex;
		FinalizeMethod = null;
	}

	public ServerReceivedDataArgs(ArraySegment<byte> data, Channel channel, int connectionId, int transportIndex, Action finalizeMethod)
	{
		Data = data;
		Channel = channel;
		ConnectionId = connectionId;
		TransportIndex = transportIndex;
		FinalizeMethod = finalizeMethod;
	}
}
