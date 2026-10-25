using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.CodeGenerating;

namespace FishNet.Managing.Server;

[UseGlobalCustomSerializer]
public struct ConnectedClientsBroadcast : IBroadcast
{
	public List<int> Values;
}
