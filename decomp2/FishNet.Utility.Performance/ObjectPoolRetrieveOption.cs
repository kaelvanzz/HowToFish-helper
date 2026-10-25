using System;

namespace FishNet.Utility.Performance;

[Flags]
public enum ObjectPoolRetrieveOption
{
	Unset = 0,
	MakeActive = 1,
	LocalSpace = 2
}
