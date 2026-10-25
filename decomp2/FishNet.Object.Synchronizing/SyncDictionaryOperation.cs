using FishNet.Documenting;

namespace FishNet.Object.Synchronizing;

[APIExclude]
public enum SyncDictionaryOperation : byte
{
	Add,
	Clear,
	Remove,
	Set,
	Complete
}
