using System;

namespace FishNet.Observing;

[Flags]
public enum HostVisibilityUpdateTypes : byte
{
	Manager = 1,
	Spawned = 2
}
