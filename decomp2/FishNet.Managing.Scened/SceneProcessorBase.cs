using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened;

public abstract class SceneProcessorBase : MonoBehaviour
{
	protected SceneManager SceneManager;

	protected Scene MovedObjectsScene;

	protected Scene DelayedDestroyScene;

	protected Scene FallbackActiveScene;

	public virtual void Initialize(SceneManager manager)
	{
		SceneManager = manager;
	}

	public virtual void LoadStart(LoadQueueData queueData)
	{
	}

	public virtual void LoadEnd(LoadQueueData queueData)
	{
	}

	public virtual void UnloadStart(LoadQueueData queueData)
	{
	}

	public virtual void UnloadEnd(LoadQueueData queueData)
	{
	}

	public virtual void UnloadStart(UnloadQueueData queueData)
	{
	}

	public virtual void UnloadEnd(UnloadQueueData queueData)
	{
	}

	public abstract void BeginLoadAsync(string sceneName, LoadSceneParameters parameters);

	public abstract void BeginUnloadAsync(Scene scene);

	public abstract bool IsPercentComplete();

	public abstract float GetPercentComplete();

	public virtual Scene GetLastLoadedScene()
	{
		return default(Scene);
	}

	public virtual void AddLoadedScene(Scene scene)
	{
	}

	public abstract List<Scene> GetLoadedScenes();

	public abstract void ActivateLoadedScenes();

	public abstract IEnumerator AsyncsIsDone();

	public virtual Scene GetMovedObjectsScene()
	{
		if (string.IsNullOrEmpty(MovedObjectsScene.name))
		{
			MovedObjectsScene = FindOrCreateScene("MovedObjectsHolder");
		}
		return MovedObjectsScene;
	}

	public virtual Scene GetDelayedDestroyScene()
	{
		if (string.IsNullOrEmpty(DelayedDestroyScene.name))
		{
			DelayedDestroyScene = FindOrCreateScene("DelayedDestroy");
		}
		return DelayedDestroyScene;
	}

	public virtual Scene GetFallbackActiveScene()
	{
		if (string.IsNullOrEmpty(FallbackActiveScene.name))
		{
			FallbackActiveScene = FindOrCreateScene("FallbackActiveScene");
		}
		return FallbackActiveScene;
	}

	public virtual Scene FindOrCreateScene(string name)
	{
		Scene result = UnityEngine.SceneManagement.SceneManager.GetSceneByName(name);
		if (!result.IsValid())
		{
			result = UnityEngine.SceneManagement.SceneManager.CreateScene(name);
		}
		return result;
	}
}
