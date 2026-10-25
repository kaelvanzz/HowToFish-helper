using FishNet.Broadcast;
using FishNet.Documenting;

namespace FishNet.Managing.Scened;

[APIExclude]
public struct UnloadScenesBroadcast : IBroadcast
{
	public UnloadQueueData QueueData;
}
