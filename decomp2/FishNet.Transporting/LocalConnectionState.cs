using System;

namespace FishNet.Transporting;

[Flags]
public enum LocalConnectionState
{
	Stopped = 1,
	Stopping = 2,
	Starting = 4,
	Started = 8
}
