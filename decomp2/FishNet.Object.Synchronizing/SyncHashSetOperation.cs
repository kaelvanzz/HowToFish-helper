using FishNet.Documenting;

namespace FishNet.Object.Synchronizing;

[APIExclude]
public enum SyncHashSetOperation : byte
{
	Add,
	Remove,
	Clear,
	Set,
	Complete
}
