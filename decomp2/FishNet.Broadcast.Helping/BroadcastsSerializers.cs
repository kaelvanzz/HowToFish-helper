using FishNet.Managing;
using FishNet.Serializing;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;

namespace FishNet.Broadcast.Helping;

internal static class BroadcastsSerializers
{
	internal static PooledWriter WriteBroadcast<T>(NetworkManager networkManager, PooledWriter writer, T message, ref Channel channel)
	{
		writer.WritePacketIdUnpacked(PacketId.Broadcast);
		writer.WriteUInt16(typeof(T).FullName.GetStableHashU16());
		PooledWriter pooledWriter = WriterPool.Retrieve();
		pooledWriter.Write(message);
		writer.WriteInt32(pooledWriter.Length);
		writer.WriteArraySegment(pooledWriter.GetArraySegment());
		networkManager.TransportManager.CheckSetReliableChannel(writer.Length, ref channel);
		pooledWriter.Store();
		return writer;
	}
}
