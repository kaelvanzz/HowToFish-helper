using System;

namespace FishNet.Serializing.Helping;

[Flags]
internal enum QuaternionDeltaPrecisionFlag : byte
{
	Unset = 0,
	NextAIsLarger = 1,
	NextBIsLarger = 2,
	NextCIsLarger = 4,
	NextDIsNegative = 8,
	LargestIsX = 0x10,
	LargestIsY = 0x20,
	LargestIsZ = 0x40,
	LargestIsW = 0x80
}
