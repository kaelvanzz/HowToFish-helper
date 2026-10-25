using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened;

public class DefaultSceneProcessor : SceneProcessorBase
{
	protected List<AsyncOperation> LoadingAsyncOperations = new List<AsyncOperation>();

	protected List<Scene> Scenes = new List<Scene>();

	protected AsyncOperation CurrentAsyncOperation;

	private Scene _lastLoadedScene;

	public override void LoadStart(LoadQueueData queueData)
	{
		base.LoadStart(queueData);
		ResetValues();
	}

	public override void LoadEnd(LoadQueueData queueData)
	{
		base.LoadEnd(queueData);
		ResetValues();
	}

	private void ResetValues()
	{
		CurrentAsyncOperation = null;
		LoadingAsyncOperations.Clear();
	}

	public override void UnloadStart(UnloadQueueData queueData)
	{
		base.UnloadStart(queueData);
		Scenes.Clear();
	}

	public override void BeginLoadAsync(string sceneName, LoadSceneParameters parameters)
	{
		AsyncOperation asyncOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, parameters);
		LoadingAsyncOperations.Add(asyncOperation);
		_lastLoadedScene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(UnityEngine.SceneManagement.SceneManager.sceneCount - 1);
		CurrentAsyncOperation = asyncOperation;
		CurrentAsyncOperation.allowSceneActivation = false;
	}

	public override void BeginUnloadAsync(Scene scene)
	{
		CurrentAsyncOperation = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
	}

	public override bool IsPercentComplete()
	{
		return GetPercentComplete() >= 0.9f;
	}

	public override float GetPercentComplete()
	{
		if (CurrentAsyncOperation != null)
		{
			return CurrentAsyncOperation.progress;
		}
		return 1f;
	}

	public override Scene GetLastLoadedScene()
	{
		return _lastLoadedScene;
	}

	public override void AddLoadedScene(Scene scene)
	{
		base.AddLoadedScene(scene);
		Scenes.Add(scene);
	}

	public override List<Scene> GetLoadedScenes()
	{
		return Scenes;
	}

	public override void ActivateLoadedScenes()
	{
		for (int i = 0; i < LoadingAsyncOperations.Count; i++)
		{
			try
			{
				LoadingAsyncOperations[i].allowSceneActivation = true;
			}
			catch (Exception ex)
			{
				SceneManager.NetworkManager.LogError("An error occured while activating scenes. " + ex.Message);
			}
		}
	}

	public override IEnumerator AsyncsIsDone()
	{
		bool notDone;
		do
		{
			notDone = false;
			foreach (AsyncOperation loadingAsyncOperation in LoadingAsyncOperations)
			{
				if (!loadingAsyncOperation.isDone)
				{
					notDone = true;
					break;
				}
			}
			yield return null;
		}
		while (notDone);
	}
}
