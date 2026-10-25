using System;

namespace FishNet.Serializing.Helping;

[Flags]
internal enum QuaternionPrecisionFlag : byte
{
	Unset = 0,
	AIsNegative = 1,
	BIsNegative = 2,
	CIsNegative = 4,
	DIsNegative = 8,
	LargestIsX = 0x10,
	LargestIsY = 0x20,
	LargestIsZ = 0x40,
	LargestIsW = 0x80
}
