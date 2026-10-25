using FishNet.Transporting;

namespace FishNet.Object;

internal readonly struct RpcLink
{
	public readonly int ObjectId;

	public readonly byte ComponentIndex;

	public readonly uint RpcHash;

	public readonly PacketId RpcPacketId;

	public RpcLink(int objectId, byte componentIndex, uint rpcHash, PacketId packetId)
	{
		ObjectId = objectId;
		ComponentIndex = componentIndex;
		RpcHash = rpcHash;
		RpcPacketId = packetId;
	}
}
