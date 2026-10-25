namespace FishNet.Object.Synchronizing;

public enum SyncTimerOperation : byte
{
	Start = 1,
	Pause = 2,
	PauseUpdated = 3,
	Unpause = 4,
	Stop = 6,
	StopUpdated = 7,
	Finished = 8,
	Complete = 9
}
