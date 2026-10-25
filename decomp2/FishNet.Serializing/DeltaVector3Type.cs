using System;

namespace FishNet.Serializing;

[Flags]
public enum DeltaVector3Type : ushort
{
	Unset = 0,
	XInt8 = 1,
	XInt16 = 2,
	XInt32 = 4,
	ZInt8 = 8,
	ZInt16 = 0x10,
	ZInt32 = 0x20,
	YInt8 = 0x40,
	YInt32 = 0x80
}
