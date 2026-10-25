using System;
using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Utility.Performance;

public class DefaultObjectPool : ObjectPool
{
	private List<Dictionary<int, Stack<NetworkObject>>> _cache = new List<Dictionary<int, Stack<NetworkObject>>>();

	[Tooltip("True if to use object pooling.")]
	[SerializeField]
	private bool _enabled = true;

	private int _cacheCount;

	public IReadOnlyList<Dictionary<int, Stack<NetworkObject>>> Cache => _cache;

	public override NetworkObject RetrieveObject(int prefabId, ushort collectionId, Transform parent = null, Vector3? nullablePosition = null, Quaternion? nullableRotation = null, Vector3? nullableScale = null, bool makeActive = true, bool asServer = true)
	{
		ObjectPoolRetrieveOption objectPoolRetrieveOption = ObjectPoolRetrieveOption.Unset;
		if (makeActive)
		{
			objectPoolRetrieveOption |= ObjectPoolRetrieveOption.MakeActive;
		}
		return RetrieveObject(prefabId, collectionId, objectPoolRetrieveOption, parent, nullablePosition, nullableRotation, nullableScale, asServer);
	}

	public override NetworkObject RetrieveObject(int prefabId, ushort collectionId, ObjectPoolRetrieveOption options, Transform parent = null, Vector3? nullablePosition = null, Quaternion? nullableRotation = null, Vector3? nullableScale = null, bool asServer = true)
	{
		bool makeActive = options.FastContains(ObjectPoolRetrieveOption.MakeActive);
		bool localSpace = options.FastContains(ObjectPoolRetrieveOption.LocalSpace);
		if (!_enabled)
		{
			return GetFromInstantiate();
		}
		Stack<NetworkObject> cache = GetCache(collectionId, prefabId, createIfMissing: true);
		NetworkObject result = null;
		while (result == null && cache.TryPop(out result))
		{
			if (result != null)
			{
				result.transform.SetParent(parent);
				if (localSpace)
				{
					result.transform.SetLocalPositionRotationAndScale(nullablePosition, nullableRotation, nullableScale);
				}
				else
				{
					result.transform.SetWorldPositionRotationAndScale(nullablePosition, nullableRotation, nullableScale);
				}
				if (makeActive)
				{
					result.gameObject.SetActive(value: true);
				}
				return result;
			}
		}
		return GetFromInstantiate();
		NetworkObject GetFromInstantiate()
		{
			NetworkObject prefab = GetPrefab(prefabId, collectionId, asServer);
			if (prefab == null)
			{
				return null;
			}
			Vector3 scale;
			NetworkObject networkObject;
			if (localSpace)
			{
				prefab.transform.OutLocalPropertyValues(nullablePosition, nullableRotation, nullableScale, out var pos, out var rot, out scale);
				if (parent != null)
				{
					pos = parent.TransformPoint(pos);
					rot = parent.rotation * rot;
				}
				networkObject = UnityEngine.Object.Instantiate(prefab, pos, rot, parent);
			}
			else
			{
				prefab.transform.OutWorldPropertyValues(nullablePosition, nullableRotation, nullableScale, out var pos2, out var rot2, out scale);
				networkObject = UnityEngine.Object.Instantiate(prefab, pos2, rot2, parent);
			}
			networkObject.transform.localScale = scale;
			if (makeActive)
			{
				networkObject.gameObject.SetActive(value: true);
			}
			return networkObject;
		}
	}

	public override NetworkObject GetPrefab(int prefabId, ushort collectionId, bool asServer)
	{
		return base.NetworkManager.GetPrefabObjects<PrefabObjects>(collectionId, createIfMissing: false).GetObject(asServer, prefabId);
	}

	public override void StoreObject(NetworkObject instantiated, bool asServer)
	{
		if (!_enabled)
		{
			UnityEngine.Object.Destroy(instantiated.gameObject);
			return;
		}
		List<NetworkObject> networkObjects = instantiated.GetNetworkObjects(GetNetworkObjectOption.All);
		foreach (NetworkObject item in networkObjects)
		{
			item.ResetState(asServer);
		}
		CollectionCaches<NetworkObject>.Store(networkObjects);
		instantiated.gameObject.SetActive(value: false);
		GetCache(instantiated.SpawnableCollectionId, instantiated.PrefabId, createIfMissing: true).Push(instantiated);
	}

	public override void CacheObjects(NetworkObject prefab, int count, bool asServer)
	{
		StorePrefabObjects(prefab, count, asServer);
	}

	public override List<NetworkObject> StorePrefabObjects(NetworkObject prefab, int count, bool asServer)
	{
		if (!_enabled)
		{
			return null;
		}
		if (count <= 0)
		{
			return null;
		}
		if (prefab == null)
		{
			return null;
		}
		if (prefab.PrefabId == ushort.MaxValue)
		{
			NetworkManagerExtensions.LogError("Pefab " + prefab.name + " has an invalid prefabId and cannot be cached.");
			return null;
		}
		List<NetworkObject> list = new List<NetworkObject>();
		Stack<NetworkObject> cache = GetCache(prefab.SpawnableCollectionId, prefab.PrefabId, createIfMissing: true);
		for (int i = 0; i < count; i++)
		{
			NetworkObject networkObject = UnityEngine.Object.Instantiate(prefab);
			networkObject.gameObject.SetActive(value: false);
			cache.Push(networkObject);
			list.Add(networkObject);
		}
		return list;
	}

	public void ClearPool(NetworkObject nob)
	{
		if (_enabled && !(nob == null))
		{
			int spawnableCollectionId = nob.SpawnableCollectionId;
			Stack<NetworkObject> cache = GetCache(spawnableCollectionId, nob.PrefabId, createIfMissing: false);
			if (cache != null)
			{
				DestroyStackNetworkObjectsAndClear(cache);
				_cache[spawnableCollectionId].Clear();
			}
		}
	}

	public void ClearPool()
	{
		int count = _cache.Count;
		for (int i = 0; i < count; i++)
		{
			ClearPool(i);
		}
	}

	public void ClearPool(int spawnableCollectionId)
	{
		if (spawnableCollectionId >= _cacheCount)
		{
			return;
		}
		Dictionary<int, Stack<NetworkObject>> dictionary = _cache[spawnableCollectionId];
		foreach (Stack<NetworkObject> value in dictionary.Values)
		{
			DestroyStackNetworkObjectsAndClear(value);
		}
		dictionary.Clear();
	}

	public Stack<NetworkObject> GetCache(int collectionId, int prefabId, bool createIfMissing)
	{
		if (collectionId >= _cacheCount)
		{
			if (!createIfMissing)
			{
				return null;
			}
			while (_cache.Count <= collectionId)
			{
				Dictionary<int, Stack<NetworkObject>> item = new Dictionary<int, Stack<NetworkObject>>();
				_cache.Add(item);
			}
			_cacheCount = _cache.Count;
		}
		Dictionary<int, Stack<NetworkObject>> dictionary = _cache[collectionId];
		if (!dictionary.TryGetValueIL2CPP(prefabId, out var value) && createIfMissing)
		{
			value = (dictionary[prefabId] = new Stack<NetworkObject>());
		}
		return value;
	}

	[Obsolete("Use GetCache(int, int, bool)")]
	public Stack<NetworkObject> GetOrCreateCache(int collectionId, int prefabId)
	{
		return GetCache(collectionId, prefabId, createIfMissing: true);
	}

	private void DestroyStackNetworkObjectsAndClear(Stack<NetworkObject> stack)
	{
		foreach (NetworkObject item in stack)
		{
			if (item != null)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
		}
		stack.Clear();
	}
}
