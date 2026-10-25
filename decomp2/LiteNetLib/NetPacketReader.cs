using LiteNetLib.Utils;

namespace LiteNetLib;

public sealed class NetPacketReader : NetDataReader
{
	private NetPacket _packet;

	private readonly NetManager _manager;

	private readonly NetEvent _evt;

	internal NetPacketReader(NetManager manager, NetEvent evt)
	{
		_manager = manager;
		_evt = evt;
	}

	internal void SetSource(NetPacket packet, int headerSize)
	{
		if (packet != null)
		{
			_packet = packet;
			SetSource(packet.RawData, headerSize, packet.Size);
		}
	}

	internal void RecycleInternal()
	{
		Clear();
		if (_packet != null)
		{
			_manager.PoolRecycle(_packet);
		}
		_packet = null;
		_manager.RecycleEvent(_evt);
	}

	public void Recycle()
	{
		if (!_manager.AutoRecycle)
		{
			RecycleInternal();
		}
	}
}
