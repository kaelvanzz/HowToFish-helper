using FishNet.Serializing;
using FishNet.Transporting;

namespace FishNet.Managing.Utility;

public class Packets
{
	internal static int GetPacketLength(ushort packetId, PooledReader reader, Channel channel)
	{
		PacketId packetId2 = (PacketId)packetId;
		if (channel == Channel.Reliable || packetId2 == PacketId.Broadcast || packetId2 == PacketId.SyncType || packetId2 == PacketId.Reconcile)
		{
			return reader.ReadInt32();
		}
		if (channel == Channel.Unreliable)
		{
			return -2;
		}
		reader.NetworkManager.LogError($"Operation is unhandled for packetId {(PacketId)packetId} on channel {channel}.");
		return -2;
	}
}
