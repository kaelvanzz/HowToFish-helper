using FishNet.Documenting;

namespace FishNet.Object.Synchronizing;

[APIExclude]
public enum SyncListOperation : byte
{
	Add,
	Insert,
	Set,
	RemoveAt,
	Clear,
	Complete
}
