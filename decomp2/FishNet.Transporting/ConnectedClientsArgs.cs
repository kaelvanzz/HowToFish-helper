using System.Collections.Generic;

namespace FishNet.Transporting;

public struct ConnectedClientsArgs
{
	public List<int> ClientIds { get; private set; }

	public ConnectedClientsArgs(List<int> clientIds)
	{
		ClientIds = clientIds;
	}
}
