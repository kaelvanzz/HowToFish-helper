using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened;

public class SceneUnloadData
{
	public PreferredScene PreferredActiveScene;

	public SceneLookupData[] SceneLookupDatas = new SceneLookupData[0];

	public UnloadParams Params = new UnloadParams();

	public UnloadOptions Options = new UnloadOptions();

	public SceneUnloadData()
	{
	}

	public SceneUnloadData(Scene scene)
		: this(new Scene[1] { scene })
	{
	}

	public SceneUnloadData(string sceneName)
		: this(new string[1] { sceneName })
	{
	}

	public SceneUnloadData(int sceneHandle)
		: this(new int[1] { sceneHandle })
	{
	}

	public SceneUnloadData(SceneLookupData sceneLookupData)
	{
		SceneLookupDatas = new SceneLookupData[1] { sceneLookupData };
	}

	public SceneUnloadData(List<Scene> scenes)
		: this(scenes.ToArray())
	{
	}

	public SceneUnloadData(List<string> sceneNames)
		: this(sceneNames.ToArray())
	{
	}

	public SceneUnloadData(List<int> sceneHandles)
		: this(sceneHandles.ToArray())
	{
	}

	public SceneUnloadData(Scene[] scenes)
	{
		SceneLookupDatas = SceneLookupData.CreateData(scenes);
	}

	public SceneUnloadData(string[] sceneNames)
	{
		SceneLookupDatas = SceneLookupData.CreateData(sceneNames);
	}

	public SceneUnloadData(int[] sceneHandles)
	{
		SceneLookupDatas = SceneLookupData.CreateData(sceneHandles);
	}

	public SceneUnloadData(SceneLookupData[] sceneLookupDatas)
	{
		SceneLookupDatas = sceneLookupDatas;
	}

	internal bool DataInvalid()
	{
		if (Params == null || SceneLookupDatas == null || Options == null)
		{
			return true;
		}
		if (SceneLookupDatas.Length == 0)
		{
			return true;
		}
		return false;
	}
}
