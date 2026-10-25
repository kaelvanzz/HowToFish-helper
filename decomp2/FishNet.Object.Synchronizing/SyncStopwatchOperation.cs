namespace FishNet.Object.Synchronizing;

public enum SyncStopwatchOperation : byte
{
	Start = 1,
	Pause = 2,
	PauseUpdated = 3,
	Unpause = 4,
	Stop = 6,
	StopUpdated = 7,
	Complete = 9
}
