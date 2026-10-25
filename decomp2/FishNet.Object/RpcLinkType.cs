using FishNet.Transporting;

namespace FishNet.Object;

internal struct RpcLinkType
{
	public readonly uint RpcHash;

	public readonly PacketId RpcPacketId;

	public readonly ushort LinkPacketId;

	public RpcLinkType(uint rpcHash, PacketId packetId, ushort linkPacketId)
	{
		RpcHash = rpcHash;
		RpcPacketId = packetId;
		LinkPacketId = linkPacketId;
	}
}
