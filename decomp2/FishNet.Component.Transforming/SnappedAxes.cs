using System;

namespace FishNet.Component.Transforming;

[Flags]
public enum SnappedAxes : uint
{
	Unset = 0u,
	X = 1u,
	Y = 2u,
	Z = 4u,
	Everything = uint.MaxValue
}
