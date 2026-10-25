using System;
using FishNet.Object;

namespace FishNet.Managing.Object;

[Serializable]
public struct DualPrefab
{
	public NetworkObject Server;

	public NetworkObject Client;
}
