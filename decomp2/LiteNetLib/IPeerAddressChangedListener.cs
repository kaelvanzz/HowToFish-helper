using System.Net;

namespace LiteNetLib;

public interface IPeerAddressChangedListener
{
	void OnPeerAddressChanged(NetPeer peer, IPEndPoint previousAddress);
}
