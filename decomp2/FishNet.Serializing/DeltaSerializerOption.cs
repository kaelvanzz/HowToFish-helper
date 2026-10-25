using System;

namespace FishNet.Serializing;

[Flags]
public enum DeltaSerializerOption : ulong
{
	Unset = 0uL,
	FullSerialize = 1uL,
	RootSerialize = 2uL
}
