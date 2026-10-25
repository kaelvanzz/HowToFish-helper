using System;
using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Utility.Performance;

public abstract class ObjectPool : MonoBehaviour
{
	protected NetworkManager NetworkManager { get; private set; }

	public virtual void LateUpdate()
	{
	}

	public virtual void InitializeOnce(NetworkManager nm)
	{
		NetworkManager = nm;
	}

	[Obsolete("Use RetrieveObject(int, ushort, RetrieveOption, parent, Vector3?, Quaternion? Vector3?, bool) instead.")]
	public virtual NetworkObject RetrieveObject(int prefabId, ushort collectionId, Transform parent = null, Vector3? position = null, Quaternion? rotation = null, Vector3? scale = null, bool makeActive = true, bool asServer = true)
	{
		return null;
	}

	public virtual NetworkObject RetrieveObject(int prefabId, ushort collectionId, ObjectPoolRetrieveOption options, Transform parent = null, Vector3? position = null, Quaternion? rotation = null, Vector3? scale = null, bool asServer = true)
	{
		return null;
	}

	public virtual NetworkObject GetPrefab(int prefabId, ushort collectionId, bool asServer)
	{
		return NetworkManager.GetPrefabObjects<PrefabObjects>(collectionId, createIfMissing: false).GetObject(asServer, prefabId);
	}

	public abstract void StoreObject(NetworkObject instantiated, bool asServer);

	[Obsolete("Use AddPrefabObjects.")]
	public virtual void CacheObjects(NetworkObject prefab, int count, bool asServer)
	{
	}

	public virtual List<NetworkObject> StorePrefabObjects(NetworkObject prefab, int count, bool asServer)
	{
		return null;
	}
}
