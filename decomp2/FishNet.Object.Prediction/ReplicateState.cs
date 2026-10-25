using System;

namespace FishNet.Object.Prediction;

[Flags]
public enum ReplicateState : byte
{
	Invalid = 0,
	Ticked = 1,
	Replayed = 2,
	Created = 4
}
