using System;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Serializing;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.Scripting;

namespace FishNet.Managing.Client;

[Preserve]
internal class CachedNetworkObject : IResettable
{
	public enum ActionType
	{
		Unset,
		Spawn,
		Despawn
	}

	public ushort CollectionId;

	public int ObjectId;

	public int InitializeOrder;

	public int OwnerId;

	public SpawnType SpawnType;

	public DespawnType DespawnType;

	public byte? ComponentId;

	public int? ParentObjectId;

	public byte? ParentComponentId;

	public int? PrefabId;

	public Vector3? Position;

	public Quaternion? Rotation;

	public Vector3? Scale;

	public ulong SceneId;

	public string SceneName = string.Empty;

	public string ObjectName = string.Empty;

	public NetworkObject NetworkObject;

	public PooledReader PayloadReader;

	public PooledReader RpcLinkReader;

	public PooledReader SyncTypesReader;

	public bool IsInitializedNested => ComponentId > 0;

	public bool IsSceneObject => SceneId != 0;

	public bool HasParent
	{
		get
		{
			if (ParentObjectId.HasValue)
			{
				return ParentComponentId.HasValue;
			}
			return false;
		}
	}

	public ActionType Action { get; private set; }

	public void InitializeSpawn(NetworkManager manager, ushort collectionId, int objectId, int initializeOrder, int ownerId, SpawnType objectSpawnType, byte? nobComponentId, int? parentObjectId, byte? parentComponentId, int? prefabId, Vector3? position, Quaternion? rotation, Vector3? scale, ulong sceneId, string sceneName, string objectName, ArraySegment<byte> payload, ArraySegment<byte> rpcLinks, ArraySegment<byte> syncTypes)
	{
		ResetState();
		Action = ActionType.Spawn;
		CollectionId = collectionId;
		ObjectId = objectId;
		InitializeOrder = initializeOrder;
		OwnerId = ownerId;
		SpawnType = objectSpawnType;
		ComponentId = nobComponentId;
		ParentObjectId = parentObjectId;
		ParentComponentId = parentComponentId;
		PrefabId = prefabId;
		Position = position;
		Rotation = rotation;
		Scale = scale;
		SceneId = sceneId;
		SceneName = sceneName;
		ObjectName = objectName;
		if (payload.Count > 0)
		{
			PayloadReader = ReaderPool.Retrieve(payload, manager);
		}
		if (rpcLinks.Count > 0)
		{
			RpcLinkReader = ReaderPool.Retrieve(rpcLinks, manager);
		}
		if (syncTypes.Count > 0)
		{
			SyncTypesReader = ReaderPool.Retrieve(syncTypes, manager);
		}
	}

	public void InitializeDespawn(int objectId, DespawnType despawnType)
	{
		ResetState();
		Action = ActionType.Despawn;
		DespawnType = despawnType;
		ObjectId = objectId;
	}

	public void ResetState()
	{
		SceneName = string.Empty;
		ObjectName = string.Empty;
		NetworkObject = null;
		ReaderPool.StoreAndDefault(ref PayloadReader);
		ReaderPool.StoreAndDefault(ref RpcLinkReader);
		ReaderPool.StoreAndDefault(ref SyncTypesReader);
	}

	public void InitializeState()
	{
	}

	~CachedNetworkObject()
	{
		NetworkObject = null;
	}
}
