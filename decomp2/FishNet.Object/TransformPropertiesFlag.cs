using System;

namespace FishNet.Object;

[Flags]
public enum TransformPropertiesFlag : uint
{
	Unset = 0u,
	Position = 1u,
	Rotation = 2u,
	Scale = 4u,
	Everything = uint.MaxValue
}
