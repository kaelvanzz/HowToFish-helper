using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened;

public struct UnloadedScene
{
	public readonly string Name;

	public readonly int Handle;

	public UnloadedScene(Scene s)
	{
		Name = s.name;
		Handle = s.handle;
	}

	public UnloadedScene(string name, int handle)
	{
		Name = name;
		Handle = handle;
	}

	public Scene GetScene()
	{
		int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
		for (int i = 0; i < sceneCount; i++)
		{
			Scene sceneAt = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
			if (sceneAt.IsValid() && sceneAt.handle == Handle)
			{
				return sceneAt;
			}
		}
		return default(Scene);
	}
}
