using FishNet.Broadcast;
using FishNet.Documenting;

namespace FishNet.Managing.Scened;

[APIExclude]
public struct LoadScenesBroadcast : IBroadcast
{
	public LoadQueueData QueueData;
}
