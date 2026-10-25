using System;

namespace FishNet.Component.Transforming;

[Flags]
public enum SynchronizedProperty : byte
{
	None = 0,
	Parent = 1,
	Position = 2,
	Rotation = 4,
	Scale = 8
}
