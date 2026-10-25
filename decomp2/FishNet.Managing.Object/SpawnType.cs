using System;

namespace FishNet.Managing.Object;

[Flags]
internal enum SpawnType : byte
{
	Unset = 0,
	Nested = 1,
	Scene = 2,
	Instantiated = 4,
	InstantiatedGlobal = 8,
	IsPredictedSpawner = 0x10
}
