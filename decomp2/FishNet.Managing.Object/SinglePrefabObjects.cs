using System.Collections.Generic;
using System.Linq;
using FishNet.Documenting;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Managing.Object;

[APIExclude]
[CreateAssetMenu(fileName = "New SinglePrefabObjects", menuName = "FishNet/Spawnable Prefabs/Single Prefab Objects")]
public class SinglePrefabObjects : PrefabObjects
{
	[Tooltip("Prefabs which may be spawned.")]
	[SerializeField]
	private List<NetworkObject> _prefabs = new List<NetworkObject>();

	public IReadOnlyList<NetworkObject> Prefabs => _prefabs;

	public override void Clear()
	{
		_prefabs.Clear();
	}

	public override int GetObjectCount()
	{
		return _prefabs.Count;
	}

	public override NetworkObject GetObject(bool asServer, int id)
	{
		if (id < 0 || id >= _prefabs.Count)
		{
			NetworkManagerExtensions.LogError($"PrefabId {id} is out of range.");
			return null;
		}
		NetworkObject networkObject = _prefabs[id];
		if (networkObject == null)
		{
			NetworkManagerExtensions.LogError($"Prefab on id {id} is null.");
		}
		return networkObject;
	}

	public override void RemoveNull()
	{
		for (int i = 0; i < _prefabs.Count; i++)
		{
			if (_prefabs[i] == null)
			{
				_prefabs.RemoveAt(i);
				i--;
			}
		}
	}

	public override void AddObject(NetworkObject networkObject, bool checkForDuplicates = false, bool initializeAdded = true)
	{
		if (!checkForDuplicates)
		{
			_prefabs.Add(networkObject);
		}
		else
		{
			AddUniqueNetworkObject(networkObject);
		}
		if (initializeAdded && Application.isPlaying)
		{
			InitializePrefabRange(0);
		}
	}

	public override void AddObjects(List<NetworkObject> networkObjects, bool checkForDuplicates = false, bool initializeAdded = true)
	{
		if (!checkForDuplicates)
		{
			_prefabs.AddRange(networkObjects);
		}
		else
		{
			foreach (NetworkObject networkObject in networkObjects)
			{
				AddUniqueNetworkObject(networkObject);
			}
		}
		if (initializeAdded && Application.isPlaying)
		{
			InitializePrefabRange(0);
		}
	}

	public override void AddObjects(NetworkObject[] networkObjects, bool checkForDuplicates = false, bool initializeAdded = true)
	{
		AddObjects(networkObjects.ToList(), checkForDuplicates, initializeAdded);
	}

	private void AddUniqueNetworkObject(NetworkObject nob)
	{
		if (!_prefabs.Contains(nob))
		{
			_prefabs.Add(nob);
		}
	}

	public override void InitializePrefabRange(int startIndex)
	{
		for (int i = startIndex; i < _prefabs.Count; i++)
		{
			ManagedObjects.InitializePrefab(_prefabs[i], i, base.CollectionId);
		}
	}

	public override void AddObject(DualPrefab dualPrefab, bool checkForDuplicates = false, bool initializeAdded = true)
	{
		NetworkManagerExtensions.LogError("Dual prefabs are not supported with SinglePrefabObjects. Make a DualPrefabObjects asset instead.");
	}

	public override void AddObjects(List<DualPrefab> dualPrefab, bool checkForDuplicates = false, bool initializeAdded = true)
	{
		NetworkManagerExtensions.LogError("Dual prefabs are not supported with SinglePrefabObjects. Make a DualPrefabObjects asset instead.");
	}

	public override void AddObjects(DualPrefab[] dualPrefab, bool checkForDuplicates = false, bool initializeAdded = true)
	{
		NetworkManagerExtensions.LogError("Dual prefabs are not supported with SinglePrefabObjects. Make a DualPrefabObjects asset instead.");
	}
}
