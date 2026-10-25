using System;

namespace FishNet.Serializing;

[Flags]
public enum DeltaVector2Type : byte
{
	Unset = 0,
	XUInt8 = 1,
	XUInt16 = 2,
	XUInt32 = 4,
	YUInt8 = 8,
	YUInt16 = 0x10,
	YUInt32 = 0x20,
	XNextIsLarger = 0x40,
	YNextIsLarger = 0x80
}
