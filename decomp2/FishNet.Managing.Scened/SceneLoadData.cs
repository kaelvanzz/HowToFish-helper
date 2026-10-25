using System.Collections.Generic;
using FishNet.Object;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened;

public class SceneLoadData
{
	public PreferredScene PreferredActiveScene;

	public SceneLookupData[] SceneLookupDatas = new SceneLookupData[0];

	public NetworkObject[] MovedNetworkObjects = new NetworkObject[0];

	public ReplaceOption ReplaceScenes = ReplaceOption.None;

	public LoadParams Params = new LoadParams();

	public LoadOptions Options = new LoadOptions();

	public SceneLoadData()
	{
	}

	public SceneLoadData(Scene scene)
		: this(new Scene[1] { scene }, null)
	{
	}

	public SceneLoadData(string sceneName)
		: this(new string[1] { sceneName }, null)
	{
	}

	public SceneLoadData(int sceneHandle)
		: this(new int[1] { sceneHandle }, null)
	{
	}

	public SceneLoadData(int sceneHandle, string sceneName)
		: this(new SceneLookupData(sceneHandle, sceneName))
	{
	}

	public SceneLoadData(SceneLookupData sceneLookupData)
		: this(new SceneLookupData[1] { sceneLookupData })
	{
	}

	public SceneLoadData(List<Scene> scenes)
		: this(scenes.ToArray(), null)
	{
	}

	public SceneLoadData(List<string> sceneNames)
		: this(sceneNames.ToArray(), null)
	{
	}

	public SceneLoadData(List<int> sceneHandles)
		: this(sceneHandles.ToArray(), null)
	{
	}

	public SceneLoadData(Scene[] scenes)
		: this(scenes, null)
	{
	}

	public SceneLoadData(string[] sceneNames)
		: this(sceneNames, null)
	{
	}

	public SceneLoadData(int[] sceneHandles)
		: this(sceneHandles, null)
	{
	}

	public SceneLoadData(SceneLookupData[] sceneLookupDatas)
		: this(sceneLookupDatas, null)
	{
	}

	public SceneLoadData(Scene scene, NetworkObject[] movedNetworkObjects)
	{
		SceneLookupData sceneLookupData = SceneLookupData.CreateData(scene);
		Construct(new SceneLookupData[1] { sceneLookupData }, movedNetworkObjects);
	}

	public SceneLoadData(Scene[] scenes, NetworkObject[] movedNetworkObjects)
	{
		SceneLookupData[] datas = SceneLookupData.CreateData(scenes);
		Construct(datas, movedNetworkObjects);
	}

	public SceneLoadData(string[] sceneNames, NetworkObject[] movedNetworkObjects)
	{
		SceneLookupData[] datas = SceneLookupData.CreateData(sceneNames);
		Construct(datas, movedNetworkObjects);
	}

	public SceneLoadData(int[] sceneHandles, NetworkObject[] movedNetworkObjects)
	{
		SceneLookupData[] datas = SceneLookupData.CreateData(sceneHandles);
		Construct(datas, movedNetworkObjects);
	}

	public SceneLoadData(SceneLookupData[] sceneLookupDatas, NetworkObject[] movedNetworkObjects)
	{
		sceneLookupDatas = SceneLookupData.ValidateData(sceneLookupDatas);
		Construct(sceneLookupDatas, movedNetworkObjects);
	}

	private void Construct(SceneLookupData[] datas, NetworkObject[] movedNetworkObjects)
	{
		SceneLookupDatas = datas;
		if (movedNetworkObjects == null)
		{
			movedNetworkObjects = new NetworkObject[0];
		}
		MovedNetworkObjects = movedNetworkObjects;
	}

	public Scene GetFirstLookupScene()
	{
		SceneLookupData[] sceneLookupDatas = SceneLookupDatas;
		for (int i = 0; i < sceneLookupDatas.Length; i++)
		{
			bool foundByHandle;
			Scene scene = sceneLookupDatas[i].GetScene(out foundByHandle, warnIfDuplicates: false);
			if (!string.IsNullOrEmpty(scene.name))
			{
				return scene;
			}
		}
		return default(Scene);
	}

	internal bool DataInvalid()
	{
		if (Params == null || MovedNetworkObjects == null || SceneLookupDatas == null || Options == null)
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
