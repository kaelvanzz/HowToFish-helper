using System;

namespace FishNet.Serializing;

[Flags]
public enum UDeltaPrecisionType : byte
{
	Unset = 0,
	UInt8 = 1,
	UInt16 = 2,
	UInt32 = 4,
	UInt64 = 8,
	UInt128 = 0x10,
	NextValueIsLarger = 0x80
}
