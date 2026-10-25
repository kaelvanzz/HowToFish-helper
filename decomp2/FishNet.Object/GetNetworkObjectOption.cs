using System;

namespace FishNet.Object;

[Flags]
internal enum GetNetworkObjectOption
{
	Self = 1,
	InitializedNested = 2,
	RuntimeNested = 4,
	Recursive = 8,
	AllNested = 6,
	AllNestedRecursive = 0xE,
	All = -1
}
