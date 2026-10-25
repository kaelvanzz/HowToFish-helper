using System;

namespace FishNet.Object.Synchronizing;

[Flags]
internal enum SyncTypeWriteFlag
{
	Unset = 0,
	IgnoreInterval = 1,
	ForceReliable = 2
}
