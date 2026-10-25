using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened;

public struct SceneUnloadEndEventArgs
{
	public readonly UnloadQueueData QueueData;

	[Obsolete("Use UnloadedScenesV2.")]
	public List<Scene> UnloadedScenes;

	public List<UnloadedScene> UnloadedScenesV2;

	internal SceneUnloadEndEventArgs(UnloadQueueData sqd, List<Scene> unloadedScenes, List<UnloadedScene> newUnloadedScenes)
	{
		QueueData = sqd;
		UnloadedScenes = unloadedScenes;
		UnloadedScenesV2 = newUnloadedScenes;
	}
}
