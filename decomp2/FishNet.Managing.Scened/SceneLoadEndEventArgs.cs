using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened;

public struct SceneLoadEndEventArgs
{
	public readonly LoadQueueData QueueData;

	public readonly Scene[] LoadedScenes;

	public readonly string[] SkippedSceneNames;

	public readonly string[] UnloadedSceneNames;

	internal SceneLoadEndEventArgs(LoadQueueData lqd, string[] skipped, Scene[] loaded, string[] unloadedSceneNames)
	{
		QueueData = lqd;
		SkippedSceneNames = skipped;
		LoadedScenes = loaded;
		UnloadedSceneNames = unloadedSceneNames;
	}
}
