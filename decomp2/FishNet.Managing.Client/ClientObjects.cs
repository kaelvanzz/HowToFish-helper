using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Object;
using FishNet.Managing.Server;
using FishNet.Managing.Utility;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Client;

public class ClientObjects : ManagedObjects
{
	private Dictionary<ushort, RpcLink> _rpcLinks = new Dictionary<ushort, RpcLink>();

	private ClientObjectCache _objectCache;

	internal void ParseRpcLink(PooledReader reader, ushort index, Channel channel)
	{
		int position = reader.Position;
		NetworkObject value2;
		if (!_rpcLinks.TryGetValueIL2CPP(index, out var value))
		{
			int packetLength = Packets.GetPacketLength(ushort.MaxValue, reader, channel);
			SkipDataLength(index, reader, packetLength);
		}
		else if (Spawned.TryGetValueIL2CPP(value.ObjectId, out value2))
		{
			NetworkBehaviour networkBehaviour = value2.NetworkBehaviours[value.ComponentIndex];
			if (value.RpcPacketId == PacketId.TargetRpc)
			{
				Packets.GetPacketLength(10, reader, channel);
				networkBehaviour.ReadTargetRpc(position, fromRpcLink: true, value.RpcHash, reader, channel);
			}
			else if (value.RpcPacketId == PacketId.ObserversRpc)
			{
				Packets.GetPacketLength(9, reader, channel);
				networkBehaviour.ReadObserversRpc(position, fromRpcLink: true, value.RpcHash, reader, channel);
			}
			else if (value.RpcPacketId == PacketId.Reconcile)
			{
				Packets.GetPacketLength(16, reader, channel);
				networkBehaviour.OnReconcileRpc(position, value.RpcHash, reader, channel);
			}
		}
		else
		{
			int packetLength = Packets.GetPacketLength(index, reader, channel);
			SkipDataLength(index, reader, packetLength, value.ObjectId);
		}
	}

	internal void SetRpcLink(ushort linkIndex, RpcLink link)
	{
		_rpcLinks[linkIndex] = link;
	}

	internal void RemoveLinkIndexes(List<ushort> values)
	{
		if (values != null)
		{
			for (int i = 0; i < values.Count; i++)
			{
				_rpcLinks.Remove(values[i]);
			}
		}
	}

	internal ClientObjects(NetworkManager networkManager)
	{
		base.Initialize(networkManager);
		_objectCache = new ClientObjectCache(this, networkManager);
	}

	internal void OnServerConnectionState(ServerConnectionStateArgs args)
	{
		if (args.ConnectionState != LocalConnectionState.Started && base.NetworkManager.IsClientStarted && args.TransportIndex == base.NetworkManager.ClientManager.GetTransportIndex())
		{
			base.NetworkManager.ClientManager.StopConnection();
		}
	}

	internal void OnClientConnectionState(ClientConnectionStateArgs args)
	{
		if (args.ConnectionState == LocalConnectionState.Started)
		{
			return;
		}
		_objectCache.Reset();
		if (!base.NetworkManager.IsServerStarted)
		{
			base.DespawnWithoutSynchronization(recursive: true, asServer: false);
		}
		else
		{
			foreach (NetworkObject value in Spawned.Values)
			{
				if (value.CanDeinitialize(asServer: false))
				{
					value.InvokeStopCallbacks(asServer: false, invokeSyncTypeCallbacks: true);
					value.SetInitializedStatus(isInitialized: false, asServer: false);
				}
			}
		}
		Spawned.Clear();
		SceneObjects_Internal.Clear();
	}

	[APIExclude]
	protected internal override void SceneManager_sceneLoaded(Scene s, LoadSceneMode arg1)
	{
		base.SceneManager_sceneLoaded(s, arg1);
		if (base.NetworkManager.IsClientStarted)
		{
			RegisterAndDespawnSceneObjects(s);
		}
	}

	internal override void AddToSpawned(NetworkObject nob, bool asServer)
	{
		base.AddToSpawned(nob, asServer);
		if (base.NetworkManager.IsServerStarted)
		{
			nob.SetRenderersVisible(visible: true);
		}
	}

	internal void PredictedSpawn(NetworkObject networkObject, NetworkConnection ownerConnection)
	{
		Queue<int> predictedObjectIds = base.NetworkManager.ClientManager.Connection.PredictedObjectIds;
		if (!predictedObjectIds.TryPeek(out var result))
		{
			base.NetworkManager.LogError("Predicted spawn for object " + networkObject.name + " failed because no more predicted ObjectIds remain. This usually occurs when the client is spawning excessively before the server can respond. Increasing ReservedObjectIds within the ServerManager component or reducing spawn rate could prevent this problem.");
			StoreNetworkObject();
			return;
		}
		networkObject.InitializePredictedObject_Client(base.NetworkManager, result, ownerConnection, base.NetworkManager.ClientManager.Connection);
		base.NetworkManager.ClientManager.Objects.AddToSpawned(networkObject, asServer: false);
		networkObject.Initialize(asServer: false, invokeSyncTypeCallbacks: true);
		PooledWriter pooledWriter = WriterPool.Retrieve();
		if (WriteSpawn(networkObject, pooledWriter, null))
		{
			base.NetworkManager.TransportManager.SendToServer(0, pooledWriter.GetArraySegment());
			predictedObjectIds.Dequeue();
		}
		else
		{
			StoreNetworkObject();
		}
		pooledWriter.Store();
		void StoreNetworkObject()
		{
			networkObject.SetIsDestroying();
			networkObject.Deinitialize(asServer: false);
			base.NetworkManager.StorePooledOrDestroyInstantiated(networkObject, asServer: false);
		}
	}

	internal void PredictedDespawn(NetworkObject networkObject)
	{
		PooledWriter pooledWriter = WriterPool.Retrieve();
		WriteDepawn(networkObject, pooledWriter);
		base.NetworkManager.TransportManager.SendToServer(0, pooledWriter.GetArraySegment());
		pooledWriter.Store();
		base.Despawn(networkObject, networkObject.GetDefaultDespawnType(), asServer: false);
	}

	public void WriteDepawn(NetworkObject nob, Writer writer)
	{
		writer.WritePacketIdUnpacked(PacketId.ObjectDespawn);
		writer.WriteNetworkObject(nob);
	}

	internal void RegisterAndDespawnSceneObjects()
	{
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			RegisterAndDespawnSceneObjects(SceneManager.GetSceneAt(i));
		}
	}

	private void RegisterAndDespawnSceneObjects(Scene s)
	{
		List<NetworkObject> result = CollectionCaches<NetworkObject>.RetrieveList();
		Scenes.GetSceneNetworkObjects(s, firstOnly: false, errorOnDuplicates: true, ignoreUnsetSceneIds: true, ref result);
		bool isServerStarted = base.NetworkManager.IsServerStarted;
		int count = result.Count;
		for (int i = 0; i < count; i++)
		{
			NetworkObject networkObject = result[i];
			if (!networkObject.IsSceneObject)
			{
				continue;
			}
			if (!isServerStarted)
			{
				networkObject.SetInitializedValues(null);
			}
			if (networkObject.GetIsNetworked())
			{
				AddToSceneObjects(networkObject);
				if (!base.NetworkManager.IsServerStarted)
				{
					networkObject.gameObject.SetActive(value: false);
				}
			}
		}
		CollectionCaches<NetworkObject>.Store(result);
	}

	internal override void NetworkObjectDestroyed(NetworkObject nob, bool asServer)
	{
		nob.RemoveClientRpcLinkIndexes();
		base.NetworkObjectDestroyed(nob, asServer);
	}

	internal void ParseOwnershipChange(PooledReader reader)
	{
		NetworkObject networkObject = reader.ReadNetworkObject();
		NetworkConnection newOwner = reader.ReadNetworkConnection();
		if (networkObject != null && networkObject.IsSpawned)
		{
			networkObject.GiveOwnership(newOwner, asServer: false, recursive: false);
		}
		else
		{
			base.NetworkManager.LogWarning("NetworkBehaviour could not be found when trying to parse OwnershipChange packet.");
		}
	}

	internal void ParseSyncType(PooledReader reader, Channel channel)
	{
		int position = reader.Position;
		NetworkBehaviour networkBehaviour = reader.ReadNetworkBehaviour();
		int num = (int)ReservedLengthWriter.ReadLength(reader, 4);
		if (networkBehaviour != null && networkBehaviour.IsSpawned)
		{
			if (num > 0)
			{
				networkBehaviour.ReadSyncType(position, reader, num);
			}
		}
		else
		{
			SkipDataLength(7, reader, num);
		}
	}

	internal void ParsePredictedSpawnResult(PooledReader reader)
	{
		_ = reader.Position;
		bool num = reader.ReadBoolean();
		int key = reader.ReadNetworkObjectId();
		int num2 = reader.ReadNetworkObjectId();
		if (num2 != 65535)
		{
			base.NetworkManager.ClientManager.Connection.PredictedObjectIds.Enqueue(num2);
		}
		if (!num && Spawned.TryGetValueIL2CPP(key, out var value))
		{
			value.SetIsDestroying(DespawnType.Destroy);
			UnityEngine.Object.Destroy(value.gameObject);
		}
	}

	internal void ParseReconcileRpc(PooledReader reader, Channel channel)
	{
		int position = reader.Position;
		NetworkBehaviour networkBehaviour = reader.ReadNetworkBehaviour();
		int packetLength = Packets.GetPacketLength(16, reader, channel);
		if (networkBehaviour != null && networkBehaviour.IsSpawned)
		{
			networkBehaviour.OnReconcileRpc(position, null, reader, channel);
		}
		else
		{
			SkipDataLength(9, reader, packetLength);
		}
	}

	internal void ParseObserversRpc(PooledReader reader, Channel channel)
	{
		int position = reader.Position;
		NetworkBehaviour networkBehaviour = reader.ReadNetworkBehaviour(logException: false);
		int packetLength = Packets.GetPacketLength(9, reader, channel);
		if (networkBehaviour != null && networkBehaviour.IsSpawned)
		{
			networkBehaviour.ReadObserversRpc(position, fromRpcLink: false, 0u, reader, channel);
			return;
		}
		base.NetworkManager.Log("NetworkBehaviour not found for an ObserverRpc. Rpc data will be discarded.");
		SkipDataLength(9, reader, packetLength);
	}

	internal void ParseTargetRpc(PooledReader reader, Channel channel)
	{
		int position = reader.Position;
		NetworkBehaviour networkBehaviour = reader.ReadNetworkBehaviour();
		int packetLength = Packets.GetPacketLength(10, reader, channel);
		if (networkBehaviour != null && networkBehaviour.IsSpawned)
		{
			networkBehaviour.ReadTargetRpc(position, fromRpcLink: false, 0u, reader, channel);
		}
		else
		{
			SkipDataLength(10, reader, packetLength);
		}
	}

	internal void ReadSpawn(PooledReader reader)
	{
		SpawnType spawnType = (SpawnType)reader.ReadUInt8Unpacked();
		bool num = spawnType.FastContains(SpawnType.Scene);
		ReadNestedSpawnIds(reader, spawnType, out var nobComponentIndex, out var parentObjectId, out var parentComponentIndex, _objectCache.ReadSpawningObjects);
		int initializeOrder;
		ushort collectionid;
		int num2 = reader.ReadNetworkObjectForSpawn(out initializeOrder, out collectionid);
		int ownerId = reader.ReadNetworkConnectionId();
		ReadTransformProperties(reader, out var localPosition, out var localRotation, out var localScale);
		int value = 0;
		ulong sceneId = 0uL;
		string empty = string.Empty;
		string empty2 = string.Empty;
		if (num)
		{
			ReadSceneObjectId(reader, out sceneId);
		}
		else
		{
			value = reader.ReadNetworkObjectId();
		}
		ArraySegment<byte> arraySegment = ReadPayload(reader);
		ArraySegment<byte> arraySegment2 = ReadRpcLinks(reader);
		ArraySegment<byte> arraySegment3 = ReadSyncTypesForSpawn(reader);
		bool flag = spawnType.FastContains(SpawnType.IsPredictedSpawner);
		if (Spawned.TryGetValue(num2, out var value2))
		{
			if (!base.NetworkManager.IsServerStarted)
			{
				if (!spawnType.FastContains(SpawnType.IsPredictedSpawner))
				{
					base.NetworkManager.LogWarning($"Received a spawn objectId of {num2} which was already found in spawned, and was not predicted. This sometimes may occur on clientHost when the server destroys an object unexpectedly before the clientHost gets the spawn message.");
					return;
				}
				PooledReader pooledReader = ReaderPool.Retrieve(ArraySegment<byte>.Empty, base.NetworkManager);
				pooledReader.Initialize(arraySegment2, base.NetworkManager, Reader.DataSource.Server);
				ApplyRpcLinks(value2, pooledReader);
				pooledReader.Initialize(arraySegment, base.NetworkManager, Reader.DataSource.Server);
				ReadPayload(null, value2, pooledReader, pooledReader.Length);
				pooledReader.Initialize(arraySegment3, base.NetworkManager, Reader.DataSource.Server);
				ApplySyncTypesForSpawn(value2, pooledReader);
				return;
			}
		}
		else if (flag)
		{
			return;
		}
		_objectCache.AddSpawn(base.NetworkManager, collectionid, num2, initializeOrder, ownerId, spawnType, nobComponentIndex, parentObjectId, parentComponentIndex, value, localPosition, localRotation, localScale, sceneId, empty, empty2, arraySegment, arraySegment2, arraySegment3);
	}

	internal void CacheDespawn(PooledReader reader)
	{
		DespawnType dt;
		int objectId = reader.ReadNetworkObjectForDespawn(out dt);
		_objectCache.AddDespawn(objectId, dt);
	}

	internal void IterateObjectCache()
	{
		_objectCache.Iterate();
	}

	internal NetworkObject GetNestedNetworkObject(CachedNetworkObject cnob)
	{
		int value = cnob.ParentObjectId.Value;
		byte value2 = cnob.ComponentId.Value;
		NetworkObject spawnedObject = _objectCache.GetSpawnedObject(value);
		if (spawnedObject == null)
		{
			if (!base.NetworkManager.IsServerStarted)
			{
				base.NetworkManager.LogError($"Nested spawned object with componentIndex of {value2} and a parentId of {value} could not be spawned because parent was not found.");
			}
			return null;
		}
		NetworkObject networkObject = null;
		List<NetworkObject> initializedNestedNetworkObjects = spawnedObject.InitializedNestedNetworkObjects;
		for (int i = 0; i < initializedNestedNetworkObjects.Count; i++)
		{
			if (initializedNestedNetworkObjects[i].ComponentIndex == value2)
			{
				networkObject = initializedNestedNetworkObjects[i];
				break;
			}
		}
		if (networkObject == null)
		{
			if (!base.NetworkManager.IsServerStarted)
			{
				base.NetworkManager.LogError($"Nested spawned object with componentIndex of {value2} could not be found as a child NetworkObject of {spawnedObject.name}.");
			}
			return null;
		}
		return networkObject;
	}

	internal void ApplyRpcLinks(NetworkObject nob, PooledReader reader)
	{
		if (reader == null)
		{
			return;
		}
		List<ushort> list = new List<ushort>();
		while (reader.Remaining > 0)
		{
			byte componentIndex = reader.ReadNetworkBehaviourId();
			ushort num = reader.ReadUInt16Unpacked();
			for (int i = 0; i < num; i++)
			{
				ushort num2 = reader.ReadUInt16Unpacked();
				RpcLink link = new RpcLink(nob.ObjectId, componentIndex, reader.ReadUInt16Unpacked(), reader.ReadPacketId());
				SetRpcLink(num2, link);
				list.Add(num2);
			}
		}
		nob.SetRpcLinkIndexes(list);
	}

	internal void ApplySyncTypesForSpawn(NetworkObject nob, PooledReader reader)
	{
		if (reader != null)
		{
			List<NetworkBehaviour> networkBehaviours = nob.NetworkBehaviours;
			while (reader.Remaining > 0)
			{
				byte index = reader.ReadUInt8Unpacked();
				networkBehaviours[index].ReadSyncTypesForSpawn(reader);
			}
		}
	}

	internal NetworkObject GetInstantiatedNetworkObject(CachedNetworkObject cnob)
	{
		if (!cnob.PrefabId.HasValue)
		{
			base.NetworkManager.LogError($"PrefabId for {cnob.ObjectId} is null. Object will not spawn.");
			return null;
		}
		NetworkManager networkManager = base.NetworkManager;
		int value = cnob.PrefabId.Value;
		if (value == 65535)
		{
			base.NetworkManager.LogError("Spawned object has an invalid prefabId. Make sure all objects which are being spawned over the network are within SpawnableObjects on the NetworkManager.");
			return null;
		}
		ushort collectionId = cnob.CollectionId;
		PrefabObjects prefabObjects = networkManager.GetPrefabObjects<PrefabObjects>(collectionId, createIfMissing: false);
		if (prefabObjects == null && collectionId > 0)
		{
			networkManager.LogError($"PrefabObjects collection is not found for CollectionId {collectionId}. Be sure to add your addressables NetworkObject prefabs to the collection on server and client before attempting to spawn them over the network.");
			return null;
		}
		NetworkObject value4;
		if (!networkManager.IsHostStarted)
		{
			Transform parent = null;
			if (cnob.HasParent)
			{
				int value2 = cnob.ParentObjectId.Value;
				NetworkObject spawnedObject = _objectCache.GetSpawnedObject(value2);
				if (spawnedObject == null)
				{
					NetworkObject @object = prefabObjects.GetObject(asServer: false, value);
					networkManager.LogError($"NetworkObject not found for ObjectId {value2}. Prefab {@object.name} will be instantiated without parent synchronization.");
				}
				else
				{
					byte value3 = cnob.ComponentId.Value;
					NetworkBehaviour networkBehaviour = spawnedObject.GetNetworkBehaviour(value3, error: false);
					if (networkBehaviour != null)
					{
						parent = networkBehaviour.transform;
					}
					else
					{
						NetworkObject object2 = prefabObjects.GetObject(asServer: false, value);
						networkManager.LogError($"NetworkBehaviour on index {value3} could not be found within NetworkObject {spawnedObject.name} with ObjectId {value2}. Prefab {object2.name} will be instantiated without parent synchronization.");
					}
				}
			}
			ObjectPoolRetrieveOption options = ObjectPoolRetrieveOption.MakeActive | ObjectPoolRetrieveOption.LocalSpace;
			value4 = networkManager.GetPooledInstantiated(value, collectionId, options, parent, cnob.Position, cnob.Rotation, cnob.Scale, asServer: false);
			bool isGlobal = cnob.SpawnType.FastContains(SpawnType.InstantiatedGlobal);
			value4.SetIsGlobal(isGlobal);
		}
		else
		{
			ServerObjects objects = networkManager.ServerManager.Objects;
			if (!objects.Spawned.TryGetValueIL2CPP(cnob.ObjectId, out value4))
			{
				value4 = objects.GetFromPending(cnob.ObjectId);
			}
			if (value4 == null)
			{
				networkManager.LogWarning($"ObjectId {cnob.ObjectId} could not be found in Server spawned, nor Server pending despawn. This may occur as clientHost when objects are destroyed before the client receives a despawn packet. In most cases this may be ignored.");
			}
		}
		return value4;
	}

	internal NetworkObject GetSpawnedNetworkObject(CachedNetworkObject cnob)
	{
		if (Spawned.TryGetValueIL2CPP(cnob.ObjectId, out var value))
		{
			return value;
		}
		return _objectCache.GetInCached(cnob.ObjectId, ClientObjectCache.CacheSearchType.Any);
	}
}
