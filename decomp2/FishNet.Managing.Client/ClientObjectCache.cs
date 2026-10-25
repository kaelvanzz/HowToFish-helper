using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Managing.Client;

internal class ClientObjectCache
{
	public enum CacheSearchType
	{
		Any,
		Spawning,
		Despawning
	}

	internal Dictionary<int, NetworkObject> IteratedSpawningObjects = new Dictionary<int, NetworkObject>();

	internal HashSet<int> ReadSpawningObjects = new HashSet<int>();

	private List<CachedNetworkObject> _cachedObjects = new List<CachedNetworkObject>();

	private HashSet<NetworkObject> _iteratedSpawns = new HashSet<NetworkObject>();

	private HashSet<int> _conflictingDespawns = new HashSet<int>();

	private ClientObjects _clientObjects;

	private NetworkManager _networkManager;

	private bool _initializeOrderChanged;

	public ClientObjectCache(ClientObjects cobs, NetworkManager networkManager)
	{
		_clientObjects = cobs;
		_networkManager = networkManager;
	}

	public NetworkObject GetInCached(int objectId, CacheSearchType searchType)
	{
		int count = _cachedObjects.Count;
		List<CachedNetworkObject> cachedObjects = _cachedObjects;
		for (int i = 0; i < count; i++)
		{
			CachedNetworkObject cachedNetworkObject = cachedObjects[i];
			if (cachedNetworkObject.ObjectId == objectId)
			{
				if (searchType == CacheSearchType.Any)
				{
					return cachedNetworkObject.NetworkObject;
				}
				bool num = searchType == CacheSearchType.Spawning;
				bool flag = cachedNetworkObject.Action == CachedNetworkObject.ActionType.Spawn;
				if (num == flag)
				{
					return cachedNetworkObject.NetworkObject;
				}
				return null;
			}
		}
		return null;
	}

	public void AddSpawn(NetworkManager manager, ushort collectionId, int objectId, int initializeOrder, int ownerId, SpawnType ost, byte? nobComponentId, int? parentObjectId, byte? parentComponentId, int? prefabId, Vector3? localPosition, Quaternion? localRotation, Vector3? localScale, ulong sceneId, string sceneName, string objectName, ArraySegment<byte> payload, ArraySegment<byte> rpcLinks, ArraySegment<byte> syncValues)
	{
		_initializeOrderChanged |= initializeOrder != 0;
		CachedNetworkObject cachedNetworkObject = null;
		if (!_initializeOrderChanged)
		{
			cachedNetworkObject = ResettableObjectCaches<CachedNetworkObject>.Retrieve();
			_cachedObjects.Add(cachedNetworkObject);
		}
		else
		{
			int count = _cachedObjects.Count;
			for (int i = 0; i < count; i++)
			{
				CachedNetworkObject cachedNetworkObject2 = _cachedObjects[i];
				if (initializeOrder < cachedNetworkObject2.InitializeOrder)
				{
					cachedNetworkObject = ResettableObjectCaches<CachedNetworkObject>.Retrieve();
					_cachedObjects.Insert(i, cachedNetworkObject);
					break;
				}
			}
			if (cachedNetworkObject == null)
			{
				cachedNetworkObject = ResettableObjectCaches<CachedNetworkObject>.Retrieve();
				_cachedObjects.Add(cachedNetworkObject);
			}
		}
		cachedNetworkObject.InitializeSpawn(manager, collectionId, objectId, initializeOrder, ownerId, ost, nobComponentId, parentObjectId, parentComponentId, prefabId, localPosition, localRotation, localScale, sceneId, sceneName, objectName, payload, rpcLinks, syncValues);
		ReadSpawningObjects.Add(objectId);
	}

	public void AddDespawn(int objectId, DespawnType despawnType)
	{
		CachedNetworkObject cachedNetworkObject = ResettableObjectCaches<CachedNetworkObject>.Retrieve();
		_cachedObjects.Add(cachedNetworkObject);
		cachedNetworkObject.InitializeDespawn(objectId, despawnType);
	}

	public void Iterate()
	{
		int count = _cachedObjects.Count;
		if (count == 0)
		{
			return;
		}
		try
		{
			HashSet<int> processedIndexes = new HashSet<int>();
			List<CachedNetworkObject> cachedObjects = _cachedObjects;
			_conflictingDespawns.Clear();
			for (int i = 0; i < count; i++)
			{
				if (processedIndexes.Contains(i))
				{
					continue;
				}
				CachedNetworkObject cachedNetworkObject = cachedObjects[i];
				bool flag = cachedNetworkObject.Action == CachedNetworkObject.ActionType.Spawn;
				if (flag && cachedNetworkObject.HasParent)
				{
					bool isInitializedNested = cachedNetworkObject.IsInitializedNested;
					int value = cachedNetworkObject.ParentObjectId.Value;
					if (GetSpawnedObject(value) == null)
					{
						bool isServerStarted = _networkManager.IsServerStarted;
						bool flag2 = false;
						for (int j = i + 1; j < count; j++)
						{
							CachedNetworkObject cachedNetworkObject2 = cachedObjects[j];
							if (cachedNetworkObject2.ObjectId != value)
							{
								continue;
							}
							flag2 = true;
							if (cachedNetworkObject.Action != CachedNetworkObject.ActionType.Spawn)
							{
								if (!isServerStarted)
								{
									string message = (isInitializedNested ? $"ObjectId {value} was found for a nested spawn, but ActionType is not spawn. ComponentIndex {cachedNetworkObject.ComponentId} will not be spawned." : $"ObjectId {value} was found for a parented spawn, but ActionType is not spawn. ObjectId {cachedNetworkObject.ObjectId} will not be spawned.");
									_networkManager.LogError(message);
								}
							}
							else
							{
								ProcessObject(cachedNetworkObject2, spawn: true, j);
							}
							break;
						}
						if (!flag2 && !isServerStarted)
						{
							string message = (isInitializedNested ? $"ObjectId {value} could not be found for a nested spawn. ComponentIndex {cachedNetworkObject.ComponentId} will not be spawned." : $"ObjectId {value} was found for a parented spawn. ObjectId {cachedNetworkObject.ObjectId} will not be spawned.");
							_networkManager.LogError(message);
						}
					}
				}
				ProcessObject(cachedNetworkObject, flag, i);
			}
			for (int k = 0; k < count; k++)
			{
				CachedNetworkObject cachedNetworkObject3 = cachedObjects[k];
				if (cachedNetworkObject3.Action == CachedNetworkObject.ActionType.Spawn && cachedNetworkObject3.NetworkObject != null)
				{
					_clientObjects.ApplySyncTypesForSpawn(cachedNetworkObject3.NetworkObject, cachedNetworkObject3.SyncTypesReader);
					if (!_conflictingDespawns.Contains(cachedNetworkObject3.ObjectId) || !_iteratedSpawns.Contains(cachedNetworkObject3.NetworkObject))
					{
						cachedNetworkObject3.NetworkObject.Initialize(asServer: false, invokeSyncTypeCallbacks: false);
					}
				}
			}
			for (int l = 0; l < count; l++)
			{
				CachedNetworkObject cachedNetworkObject4 = cachedObjects[l];
				if (cachedNetworkObject4.Action == CachedNetworkObject.ActionType.Spawn && cachedNetworkObject4.NetworkObject != null)
				{
					cachedNetworkObject4.NetworkObject.InvokeOnStartSyncTypeCallbacks(asServer: false);
				}
			}
			void ProcessObject(CachedNetworkObject cnob, bool spawn, int index)
			{
				processedIndexes.Add(index);
				if (spawn)
				{
					if (cnob.IsSceneObject)
					{
						cnob.NetworkObject = _clientObjects.GetSceneNetworkObject(cnob.SceneId, cnob.SceneName, cnob.ObjectName);
						if (cnob.NetworkObject != null)
						{
							SetParentAndTransformProperties(cnob);
						}
					}
					else if (cnob.IsInitializedNested)
					{
						cnob.NetworkObject = _clientObjects.GetNestedNetworkObject(cnob);
						if (cnob.NetworkObject != null)
						{
							cnob.NetworkObject.transform.SetLocalPositionRotationAndScale(cnob.Position, cnob.Rotation, cnob.Scale);
						}
					}
					else
					{
						cnob.NetworkObject = _clientObjects.GetInstantiatedNetworkObject(cnob);
					}
				}
				else
				{
					cnob.NetworkObject = _clientObjects.GetSpawnedNetworkObject(cnob);
				}
				NetworkObject networkObject = cnob.NetworkObject;
				if (!(networkObject == null))
				{
					if (spawn)
					{
						int objectId;
						NetworkConnection value2;
						if (!_networkManager.IsServerStarted)
						{
							objectId = cnob.ObjectId;
							int ownerId = cnob.OwnerId;
							NetworkConnection connection = _networkManager.ClientManager.Connection;
							if (ownerId == connection.ClientId)
							{
								value2 = connection;
							}
							else if (!_networkManager.ClientManager.Clients.TryGetValueIL2CPP(ownerId, out value2))
							{
								value2 = NetworkManager.EmptyConnection;
							}
						}
						else
						{
							value2 = networkObject.Owner;
							objectId = networkObject.ObjectId;
						}
						networkObject.InitializeEarly(_networkManager, objectId, value2, asServer: false);
						if (cnob.PayloadReader != null)
						{
							_networkManager.ClientManager.Objects.ReadPayload(NetworkManager.EmptyConnection, networkObject, cnob.PayloadReader, cnob.PayloadReader.Length);
						}
						_clientObjects.AddToSpawned(cnob.NetworkObject, asServer: false);
						IteratedSpawningObjects.Add(cnob.ObjectId, cnob.NetworkObject);
						_clientObjects.ApplyRpcLinks(cnob.NetworkObject, cnob.RpcLinkReader);
						_iteratedSpawns.Add(cnob.NetworkObject);
						if (!_networkManager.IsServerStarted && cnob.NetworkObject != null)
						{
							cnob.NetworkObject.gameObject.SetActive(value: true);
						}
					}
					else
					{
						NetworkObject networkObject2 = cnob.NetworkObject;
						if (_iteratedSpawns.Contains(networkObject2))
						{
							_conflictingDespawns.Add(cnob.ObjectId);
							networkObject2.gameObject.SetActive(value: true);
							networkObject2.Initialize(asServer: false, invokeSyncTypeCallbacks: true);
						}
						IterateDespawn(cnob);
					}
				}
			}
		}
		finally
		{
			Reset();
		}
	}

	private void SetParentAndTransformProperties(CachedNetworkObject cnob)
	{
		if (_networkManager.IsHostStarted || !(cnob.NetworkObject != null))
		{
			return;
		}
		if (cnob.HasParent)
		{
			if (_networkManager.ClientManager.Objects.Spawned.TryGetValueIL2CPP(cnob.ParentObjectId.Value, out var value))
			{
				if (!cnob.ParentComponentId.HasValue)
				{
					cnob.NetworkObject.SetParent(value);
				}
				else
				{
					cnob.NetworkObject.SetParent(value.NetworkBehaviours[cnob.ParentComponentId.Value]);
				}
			}
			else
			{
				_networkManager.Log($"Parent NetworkObject Id {cnob.ParentObjectId} could not be found in spawned. NetworkObject {cnob.NetworkObject} will not have it's parent set.");
			}
		}
		cnob.NetworkObject.transform.SetLocalPositionRotationAndScale(cnob.Position, cnob.Rotation, cnob.Scale);
	}

	private void IterateDespawn(CachedNetworkObject cnob)
	{
		_clientObjects.Despawn(cnob.NetworkObject, cnob.DespawnType, asServer: false);
	}

	internal NetworkObject GetSpawnedObject(int objectId)
	{
		if (!IteratedSpawningObjects.TryGetValue(objectId, out var value))
		{
			(_networkManager.IsHostStarted ? _networkManager.ServerManager.Objects.Spawned : _networkManager.ClientManager.Objects.Spawned).TryGetValue(objectId, out value);
		}
		return value;
	}

	public void Reset()
	{
		_initializeOrderChanged = false;
		foreach (CachedNetworkObject cachedObject in _cachedObjects)
		{
			ResettableObjectCaches<CachedNetworkObject>.Store(cachedObject);
		}
		_cachedObjects.Clear();
		_iteratedSpawns.Clear();
		IteratedSpawningObjects.Clear();
		ReadSpawningObjects.Clear();
	}
}
