using System.Collections.Generic;
using FishNet.Documenting;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Managing.Object;

[APIExclude]
public class DefaultPrefabObjects : SinglePrefabObjects
{
	internal bool SetAssetPathHashes(int index)
	{
		return false;
	}

	internal void Sort()
	{
		if (GetObjectCount() == 0)
		{
			return;
		}
		Dictionary<ulong, NetworkObject> dictionary = new Dictionary<ulong, NetworkObject>();
		List<ulong> list = new List<ulong>();
		bool flag = false;
		foreach (NetworkObject prefab in base.Prefabs)
		{
			list.Add(prefab.AssetPathHash);
			if (prefab.AssetPathHash == 0L)
			{
				flag = true;
				Debug.LogError("AssetPathHash is not set for GameObject " + prefab.name + ".");
			}
			dictionary.Add(prefab.AssetPathHash, prefab);
		}
		if (flag)
		{
			Debug.LogError("One or more NetworkObject prefabs did not have their AssetPathHash set. This usually occurs when a prefab cannot be saved. Check the specified prefabs for missing scripts or serialization errors and correct them, then use Fish-Networking -> Refresh Default Prefabs.");
			return;
		}
		list.Sort();
		List<NetworkObject> list2 = new List<NetworkObject>();
		foreach (ulong item in list)
		{
			list2.Add(dictionary[item]);
		}
		Clear();
		AddObjects(list2, checkForDuplicates: false, initializeAdded: false);
	}
}
