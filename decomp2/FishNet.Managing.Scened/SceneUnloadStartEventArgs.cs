namespace FishNet.Managing.Scened;

public struct SceneUnloadStartEventArgs
{
	public readonly UnloadQueueData QueueData;

	internal SceneUnloadStartEventArgs(UnloadQueueData sqd)
	{
		QueueData = sqd;
	}
}
