using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Utility.Extension;

public static class Scenes
{
	public static void GetSceneNetworkObjects(Scene s, bool firstOnly, bool errorOnDuplicates, bool ignoreUnsetSceneIds, ref List<NetworkObject> result)
	{
		List<NetworkObject> list = CollectionCaches<NetworkObject>.RetrieveList();
		List<NetworkObject> list2 = CollectionCaches<NetworkObject>.RetrieveList();
		List<GameObject> list3 = CollectionCaches<GameObject>.RetrieveList();
		Dictionary<ulong, NetworkObject> sceneIds = CollectionCaches<ulong, NetworkObject>.RetrieveDictionary();
		s.GetRootGameObjects(list3);
		foreach (GameObject item in list3)
		{
			item.GetComponentsInChildren(includeInactive: true, list);
			if (list.Count <= 0)
			{
				continue;
			}
			if (firstOnly)
			{
				foreach (NetworkObject item2 in list)
				{
					if (!ignoreUnsetSceneIds || item2.IsSceneObject)
					{
						item2.GetComponentsInParent(includeInactive: true, list2);
						if (list2.Count == 1 && !TryDisplayDuplicateError(item2))
						{
							result.Add(item2);
						}
					}
				}
				continue;
			}
			foreach (NetworkObject item3 in list)
			{
				if ((!ignoreUnsetSceneIds || item3.IsSceneObject) && !TryDisplayDuplicateError(item3))
				{
					result.Add(item3);
				}
			}
		}
		CollectionCaches<ulong, NetworkObject>.Store(sceneIds);
		bool TryDisplayDuplicateError(NetworkObject nob)
		{
			if (!errorOnDuplicates)
			{
				return false;
			}
			ulong sceneId = nob.SceneId;
			if (sceneIds.TryGetValue(sceneId, out var value))
			{
				NetworkManagerExtensions.LogError($"Object {nob.name} and {value.name} in scene {nob.gameObject.scene.name} have the same sceneId of {sceneId}. This will result in spawning errors. Exit play mode and use the Fish-Networking menu to reserialize sceneIds for scene {nob.gameObject.scene.name}.");
				return true;
			}
			sceneIds[sceneId] = nob;
			return false;
		}
	}
}
