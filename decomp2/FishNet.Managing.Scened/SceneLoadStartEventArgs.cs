namespace FishNet.Managing.Scened;

public struct SceneLoadStartEventArgs
{
	public readonly LoadQueueData QueueData;

	internal SceneLoadStartEventArgs(LoadQueueData lqd)
	{
		QueueData = lqd;
	}
}
