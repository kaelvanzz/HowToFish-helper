using FishNet.Broadcast;
using FishNet.Documenting;

namespace FishNet.Managing.Scened;

[APIExclude]
public struct ClientScenesLoadedBroadcast : IBroadcast
{
	public SceneLookupData[] SceneLookupDatas;
}
