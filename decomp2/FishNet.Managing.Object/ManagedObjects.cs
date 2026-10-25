using System;
using System.Collections.Generic;
using FishNet.Component.Observing;
using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Managing.Statistic;
using FishNet.Managing.Utility;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Object;

public abstract class ManagedObjects
{
	internal const byte PREDICTED_SPAWN_BYTES = 2;

	public Dictionary<int, NetworkObject> Spawned = new Dictionary<int, NetworkObject>();

	protected Dictionary<ulong, NetworkObject> SceneObjects_Internal = new Dictionary<ulong, NetworkObject>();

	protected NetworkTrafficStatistics NetworkTrafficStatistics;

	private HashGrid _hashGrid;

	protected NetworkManager NetworkManager { get; private set; }

	public IReadOnlyDictionary<ulong, NetworkObject> SceneObjects => SceneObjects_Internal;

	protected void ReadTransformProperties(Reader reader, out Vector3? localPosition, out Quaternion? localRotation, out Vector3? localScale)
	{
		byte whole = reader.ReadUInt8Unpacked();
		if (((TransformPropertiesFlag)whole).FastContains(TransformPropertiesFlag.Position))
		{
			localPosition = reader.ReadVector3();
		}
		else
		{
			localPosition = null;
		}
		if (((TransformPropertiesFlag)whole).FastContains(TransformPropertiesFlag.Rotation))
		{
			localRotation = reader.ReadQuaternion(NetworkManager.ServerManager.SpawnPacking.Rotation);
		}
		else
		{
			localRotation = null;
		}
		if (((TransformPropertiesFlag)whole).FastContains(TransformPropertiesFlag.Scale))
		{
			localScale = reader.ReadVector3();
		}
		else
		{
			localScale = null;
		}
	}

	internal bool WriteSpawn(NetworkObject nob, PooledWriter writer, NetworkConnection connection)
	{
		writer.WritePacketIdUnpacked(PacketId.ObjectSpawn);
		ReservedLengthWriter reservedLengthWriter = ReservedWritersExtensions.Retrieve();
		bool num = connection == null;
		if (num)
		{
			reservedLengthWriter.Initialize(writer, 2);
		}
		bool isSceneObject = nob.IsSceneObject;
		SpawnType spawnType = SpawnType.Unset;
		spawnType = ((!isSceneObject) ? (spawnType | (nob.IsGlobal ? SpawnType.InstantiatedGlobal : SpawnType.Instantiated)) : (spawnType | SpawnType.Scene));
		if (connection == nob.PredictedSpawner)
		{
			spawnType |= SpawnType.IsPredictedSpawner;
		}
		PooledWriter pooledWriter = WriteNestedSpawn(nob, ref spawnType);
		writer.WriteUInt8Unpacked((byte)spawnType);
		if (pooledWriter != null)
		{
			writer.WriteArraySegment(pooledWriter.GetArraySegment());
			WriterPool.Store(pooledWriter);
		}
		writer.WriteSpawnedNetworkObject(nob);
		writer.WriteNetworkConnection(nob.Owner);
		WriteChangedTransformProperties(nob, isSceneObject, writer);
		if (isSceneObject)
		{
			writer.WriteUInt64Unpacked(nob.SceneId);
		}
		else
		{
			writer.WriteNetworkObjectId(nob.PrefabId);
		}
		NetworkConnection sender = (num ? NetworkManager.EmptyConnection : connection);
		WritePayload(sender, nob, writer);
		WriteRpcLinks(nob, writer);
		WriteSyncTypesForSpawn(nob, writer, connection);
		bool flag;
		if (num)
		{
			int num2 = 65535;
			flag = reservedLengthWriter.Length <= num2;
			if (!flag)
			{
				NetworkManager.LogError($"A single predicted spawns may not exceed {num2} bytes in length. Written length is {reservedLengthWriter.Length}. Predicted spawn for {nob.name} will be despawned immediately.");
			}
			else
			{
				reservedLengthWriter.WriteLength();
			}
		}
		else
		{
			flag = true;
		}
		reservedLengthWriter.Store();
		return flag;
	}

	protected void WriteRpcLinks(NetworkObject nob, PooledWriter writer)
	{
		ReservedLengthWriter reservedLengthWriter = ReservedWritersExtensions.Retrieve();
		reservedLengthWriter.Initialize(writer, 2);
		if (NetworkManager.IsServerStarted)
		{
			foreach (NetworkBehaviour networkBehaviour in nob.NetworkBehaviours)
			{
				networkBehaviour.WriteRpcLinks(writer);
			}
		}
		reservedLengthWriter.WriteLength();
		reservedLengthWriter.Store();
	}

	protected ArraySegment<byte> ReadRpcLinks(PooledReader reader)
	{
		uint count = ReservedLengthWriter.ReadLength(reader, 2);
		return reader.ReadArraySegment((int)count);
	}

	protected void WriteSyncTypesForSpawn(NetworkObject nob, PooledWriter writer, NetworkConnection connection)
	{
		ReservedLengthWriter reservedLengthWriter = ReservedWritersExtensions.Retrieve();
		reservedLengthWriter.Initialize(writer, 4);
		if (NetworkManager.IsServerStarted)
		{
			foreach (NetworkBehaviour networkBehaviour in nob.NetworkBehaviours)
			{
				networkBehaviour.WriteSyncTypesForSpawn(writer, connection);
			}
		}
		reservedLengthWriter.WriteLength();
		reservedLengthWriter.Store();
	}

	protected ArraySegment<byte> ReadSyncTypesForSpawn(PooledReader reader)
	{
		uint count = ReservedLengthWriter.ReadLength(reader, 4);
		return reader.ReadArraySegment((int)count);
	}

	internal PooledWriter WriteNestedSpawn(NetworkObject nob, ref SpawnType st)
	{
		Transform parent = nob.transform.parent;
		if (parent != null)
		{
			NetworkBehaviour currentParentNetworkBehaviour = nob.CurrentParentNetworkBehaviour;
			if (currentParentNetworkBehaviour == null)
			{
				return null;
			}
			if (!currentParentNetworkBehaviour.IsSpawned)
			{
				NetworkManager.LogWarning("Parent " + parent.name + " is not spawned. " + nob.name + " will not have it's parent sent in the spawn message.");
				return null;
			}
			st |= SpawnType.Nested;
			PooledWriter pooledWriter = WriterPool.Retrieve();
			pooledWriter.WriteUInt8Unpacked(nob.ComponentIndex);
			pooledWriter.WriteNetworkBehaviour(currentParentNetworkBehaviour);
			return pooledWriter;
		}
		return null;
	}

	internal void ReadNestedSpawnIds(PooledReader reader, SpawnType st, out byte? nobComponentIndex, out int? parentObjectId, out byte? parentComponentIndex, HashSet<int> readSpawningObjects = null)
	{
		if (st.FastContains(SpawnType.Nested))
		{
			nobComponentIndex = reader.ReadUInt8Unpacked();
			reader.ReadNetworkBehaviour(out var objectId, out var componentIndex, readSpawningObjects);
			if (objectId != 65535)
			{
				parentObjectId = objectId;
				parentComponentIndex = componentIndex;
				return;
			}
		}
		nobComponentIndex = null;
		parentObjectId = null;
		parentComponentIndex = null;
	}

	protected void ReadSceneObjectId(PooledReader reader, out ulong sceneId)
	{
		sceneId = reader.ReadUInt64Unpacked();
	}

	protected void WriteChangedTransformProperties(NetworkObject nob, bool sceneObject, Writer headerWriter)
	{
		TransformPropertiesFlag transformPropertiesFlag;
		if (sceneObject || nob.InitializedParentNetworkBehaviour != null)
		{
			transformPropertiesFlag = nob.GetTransformChanges(nob.SerializedTransformProperties);
		}
		else if (nob.PrefabId == ushort.MaxValue)
		{
			NetworkManager.LogWarning("NetworkObject " + nob.ToString() + " unexpectedly has an unset PrefabId while it's not nested. Please report this warning.");
			transformPropertiesFlag = TransformPropertiesFlag.Everything;
		}
		else
		{
			PrefabObjects prefabObjects = NetworkManager.GetPrefabObjects<PrefabObjects>(nob.SpawnableCollectionId, createIfMissing: false);
			transformPropertiesFlag = nob.GetTransformChanges(prefabObjects.GetObject(asServer: true, nob.PrefabId).gameObject);
		}
		headerWriter.WriteUInt8Unpacked((byte)transformPropertiesFlag);
		if (transformPropertiesFlag != 0)
		{
			if (transformPropertiesFlag.FastContains(TransformPropertiesFlag.Position))
			{
				headerWriter.WriteVector3(nob.transform.localPosition);
			}
			if (transformPropertiesFlag.FastContains(TransformPropertiesFlag.Rotation))
			{
				headerWriter.WriteQuaternion(nob.transform.localRotation, NetworkManager.ServerManager.SpawnPacking.Rotation);
			}
			if (transformPropertiesFlag.FastContains(TransformPropertiesFlag.Scale))
			{
				headerWriter.WriteVector3(nob.transform.localScale);
			}
		}
	}

	protected void WriteDespawn(NetworkObject nob, DespawnType despawnType, Writer everyoneWriter)
	{
		everyoneWriter.WritePacketIdUnpacked(PacketId.ObjectDespawn);
		everyoneWriter.WriteNetworkObjectForDespawn(nob, despawnType);
	}

	internal NetworkObject GetSceneNetworkObject(ulong sceneId, string sceneName, string objectName)
	{
		SceneObjects_Internal.TryGetValueIL2CPP(sceneId, out var value);
		if (value == null)
		{
			NetworkManager.LogError($"SceneId of {sceneId} not found in SceneObjects. This may occur if your scene differs between client and server, if client does not have the scene loaded, or if networked scene objects do not have a SceneCondition. See ObserverManager in the documentation for more on conditions.");
		}
		return value;
	}

	protected bool CanPredictedSpawn(NetworkObject nob, NetworkConnection spawner, bool asServer, Reader reader = null)
	{
		if (!nob.AllowPredictedSpawning)
		{
			if (asServer)
			{
				spawner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection {spawner.ClientId} tried to spawn an object {nob.name} which does not support predicted spawning.");
			}
			else
			{
				NetworkManager.LogError("Object " + nob.name + " does not support predicted spawning. Add a PredictedSpawn component to the object and configure appropriately.");
			}
			reader?.Clear();
			return false;
		}
		if (nob.InitializedNestedNetworkObjects.Count > 0)
		{
			if (asServer)
			{
				spawner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection {spawner.ClientId} tried to spawn an object {nob.name} which has nested NetworkObjects.");
			}
			else
			{
				NetworkManager.LogError("Predicted spawning prefabs which contain nested NetworkObjects is not yet supported but will be in a later release.");
			}
			reader?.Clear();
			return false;
		}
		return true;
	}

	protected bool CanPredictedDespawn(NetworkObject nob, NetworkConnection despawner, bool asServer, Reader reader = null)
	{
		if (!nob.AllowPredictedDespawning)
		{
			if (asServer)
			{
				despawner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection {despawner.ClientId} tried to despawn an object {nob.name} which does not support predicted despawning.");
			}
			else
			{
				NetworkManager.LogError("Object " + nob.name + " does not support predicted despawning. Modify the PredictedSpawn component settings to allow predicted despawning.");
			}
			reader?.Clear();
			return false;
		}
		if (nob.InitializedNestedNetworkObjects.Count > 0)
		{
			if (asServer)
			{
				despawner.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection {despawner.ClientId} tried to despawn an object {nob.name} which has nested NetworkObjects.");
			}
			else
			{
				NetworkManager.LogError("Predicted despawning prefabs which contain nested NetworkObjects is not yet supported but will be in a later release.");
			}
			reader?.Clear();
			return false;
		}
		if ((asServer && !nob.PredictedSpawn.OnTryDespawnServer(despawner)) || (!asServer && !nob.PredictedSpawn.OnTryDespawnClient()))
		{
			return false;
		}
		return true;
	}

	internal void ReadPayload(NetworkConnection sender, NetworkObject nob, PooledReader reader, int? payloadLength = null)
	{
		if (!payloadLength.HasValue)
		{
			payloadLength = (int)ReservedLengthWriter.ReadLength(reader, 4);
		}
		if (!(payloadLength > 0))
		{
			return;
		}
		if (nob != null)
		{
			foreach (NetworkBehaviour networkBehaviour in nob.NetworkBehaviours)
			{
				networkBehaviour.ReadPayload(sender, reader);
			}
			return;
		}
		reader.Skip(payloadLength.Value);
	}

	internal ArraySegment<byte> ReadPayload(PooledReader reader)
	{
		int count = (int)ReservedLengthWriter.ReadLength(reader, 4);
		return reader.ReadArraySegment(count);
	}

	protected void WritePayload(NetworkConnection sender, NetworkObject nob, PooledWriter writer)
	{
		ReservedLengthWriter reservedLengthWriter = ReservedWritersExtensions.Retrieve();
		reservedLengthWriter.Initialize(writer, 4);
		foreach (NetworkBehaviour networkBehaviour in nob.NetworkBehaviours)
		{
			networkBehaviour.WritePayload(sender, writer);
		}
		reservedLengthWriter.WriteLength();
	}

	protected internal virtual bool GetNextNetworkObjectId(out int nextNetworkObjectId)
	{
		nextNetworkObjectId = 65535;
		return false;
	}

	protected virtual void Initialize(NetworkManager manager)
	{
		NetworkManager = manager;
		manager.StatisticsManager.TryGetNetworkTrafficStatistics(out NetworkTrafficStatistics);
		manager.TryGetInstance<HashGrid>(out _hashGrid);
	}

	internal void SubscribeToSceneLoaded(bool subscribe)
	{
		if (subscribe)
		{
			SceneManager.sceneLoaded += SceneManager_sceneLoaded;
		}
		else
		{
			SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
		}
	}

	protected internal virtual void SceneManager_sceneLoaded(Scene s, LoadSceneMode arg1)
	{
	}

	internal virtual void NetworkObjectDestroyed(NetworkObject nob, bool asServer)
	{
		if (!(nob == null))
		{
			RemoveFromSpawned(nob, fromOnDestroy: true, asServer);
		}
	}

	protected virtual void RemoveFromSpawned(NetworkObject nob, bool fromOnDestroy, bool asServer)
	{
		Spawned.Remove(nob.ObjectId);
		if (fromOnDestroy && nob.IsSceneObject)
		{
			RemoveFromSceneObjects(nob);
		}
	}

	internal virtual void Despawn(NetworkObject nob, DespawnType despawnType, bool asServer)
	{
		if (nob == null)
		{
			NetworkManager.LogWarning("Cannot despawn a null NetworkObject.");
			return;
		}
		if (!asServer && !nob.IsClientInitialized)
		{
			NetworkManager.LogError("Object " + nob.ToString() + " is already despawned. Please report this error.");
			return;
		}
		bool flag = false;
		bool wasRemovedFromPending = false;
		if (!nob.IsNested)
		{
			if (asServer)
			{
				if (!nob.IsSceneObject)
				{
					if (nob.Observers.Contains(NetworkManager.ClientManager.Connection))
					{
						NetworkManager.ServerManager.Objects.AddToPending(nob);
					}
					else
					{
						flag = true;
					}
				}
			}
			else
			{
				bool isServerStarted = NetworkManager.IsServerStarted;
				if (!nob.IsSceneObject)
				{
					wasRemovedFromPending = NetworkManager.ServerManager.Objects.RemoveFromPending(nob);
					flag = !isServerStarted || wasRemovedFromPending;
				}
			}
		}
		TryUnsetParent();
		nob.SetIsDestroying(despawnType);
		nob.Deinitialize(asServer);
		if (asServer)
		{
			MatchCondition.RemoveFromMatchWithoutRebuild(nob, NetworkManager);
		}
		RemoveFromSpawned(nob, fromOnDestroy: false, asServer);
		if (flag)
		{
			if (despawnType == DespawnType.Destroy)
			{
				UnityEngine.Object.Destroy(nob.gameObject);
			}
			else
			{
				NetworkManager.StorePooledInstantiated(nob, asServer);
			}
			return;
		}
		if (asServer)
		{
			if (!NetworkManager.IsClientStarted || !nob.Observers.Contains(NetworkManager.ClientManager.Connection))
			{
				nob.gameObject.SetActive(value: false);
			}
		}
		else if (!NetworkManager.IsServerStarted)
		{
			nob.gameObject.SetActive(value: false);
		}
		else if (NetworkManager.ServerManager.Objects.Spawned.ContainsKey(nob.ObjectId))
		{
			nob.SetRenderersVisible(visible: false);
		}
		else
		{
			nob.gameObject.SetActive(value: false);
		}
		if (!asServer)
		{
			return;
		}
		foreach (NetworkObject networkObject in nob.GetNetworkObjects(GetNetworkObjectOption.AllNested))
		{
			if (networkObject != null && !networkObject.IsDeinitializing)
			{
				Despawn(networkObject, despawnType, asServer: true);
			}
		}
		void TryUnsetParent()
		{
			if ((!asServer || wasRemovedFromPending) && nob.RuntimeParentNetworkBehaviour != null)
			{
				nob.UnsetParent();
				despawnType = nob.GetDefaultDespawnType();
			}
		}
	}

	public static void InitializePrefab(NetworkObject prefab, int index, ushort? collectionId = null)
	{
		if (index == -1)
		{
			Debug.LogError($"An index of {-1} cannot be assigned as a PrefabId for {prefab.name}.");
		}
		else if (!(prefab == null))
		{
			prefab.PrefabId = (ushort)index;
			if (collectionId.HasValue)
			{
				prefab.SpawnableCollectionId = collectionId.Value;
			}
			prefab.SetInitializedValues(null, force: true);
		}
	}

	internal virtual void DespawnWithoutSynchronization(bool recursive, bool asServer)
	{
		foreach (NetworkObject value in Spawned.Values)
		{
			if (!(value == null))
			{
				DespawnWithoutSynchronization(value, recursive, asServer, value.GetDefaultDespawnType(), removeFromSpawned: false);
			}
		}
		Spawned.Clear();
	}

	protected virtual void DespawnWithoutSynchronization(NetworkObject nob, bool recursive, bool asServer, DespawnType despawnType, bool removeFromSpawned)
	{
		GetNetworkObjectOption option = ((!recursive) ? GetNetworkObjectOption.Self : GetNetworkObjectOption.All);
		List<NetworkObject> networkObjects = nob.GetNetworkObjects(option);
		bool flag = asServer || !NetworkManager.IsServerStarted;
		foreach (NetworkObject item in networkObjects)
		{
			item.SetIsDestroying(despawnType);
			item.Deinitialize(asServer);
			if (flag && removeFromSpawned)
			{
				RemoveFromSpawned(item, fromOnDestroy: false, asServer);
			}
		}
		if (flag)
		{
			NetworkObject networkObject = networkObjects[0];
			if (networkObject.IsSceneObject || networkObject.IsInitializedNested)
			{
				networkObject.gameObject.SetActive(value: false);
			}
			else if (despawnType == DespawnType.Destroy)
			{
				UnityEngine.Object.Destroy(networkObject.gameObject);
			}
			else
			{
				NetworkManager.StorePooledInstantiated(networkObject, asServer);
			}
		}
		CollectionCaches<NetworkObject>.Store(networkObjects);
	}

	internal virtual void AddToSpawned(NetworkObject nob, bool asServer)
	{
		Spawned[nob.ObjectId] = nob;
	}

	protected internal void AddToSceneObjects(NetworkObject nob)
	{
		SceneObjects_Internal[nob.SceneId] = nob;
	}

	protected internal void RemoveFromSceneObjects(NetworkObject nob)
	{
		SceneObjects_Internal.Remove(nob.SceneId);
	}

	protected internal void RemoveFromSceneObjects(ulong sceneId)
	{
		SceneObjects_Internal.Remove(sceneId);
	}

	protected internal NetworkObject GetSpawnedNetworkObject(int objectId)
	{
		if (!Spawned.TryGetValueIL2CPP(objectId, out var value))
		{
			NetworkManager.LogError($"Spawned NetworkObject not found for ObjectId {objectId}.");
		}
		return value;
	}

	protected internal void SkipDataLength(ushort packetId, PooledReader reader, int dataLength, int rpcLinkObjectId = -1)
	{
		if (dataLength == -1)
		{
			NetworkManagerExtensions.LogError(message: (packetId < NetworkManager.StartingRpcLinkIndex) ? $"NetworkBehaviour could not be found for packetId {(PacketId)packetId}. Remaining data will be purged." : ((rpcLinkObjectId == -1) ? $"RPCLink of Id {(PacketId)packetId} could not be found. Remaining data will be purged." : $"ObjectId {rpcLinkObjectId} for RPCLink {(PacketId)packetId} could not be found."), networkManager: NetworkManager);
			reader.Clear();
		}
		else if (dataLength >= 0)
		{
			reader.Skip(Math.Min(dataLength, reader.Remaining));
		}
		else if (dataLength == -2)
		{
			reader.Clear();
		}
	}

	internal void ParseReplicateRpc(PooledReader reader, NetworkConnection conn, Channel channel)
	{
		int position = reader.Position;
		NetworkBehaviour networkBehaviour = reader.ReadNetworkBehaviour();
		int packetLength = Packets.GetPacketLength(8, reader, channel);
		if (networkBehaviour != null && networkBehaviour.IsSpawned)
		{
			networkBehaviour.OnReplicateRpc(position, null, reader, conn, channel);
		}
		else
		{
			SkipDataLength(8, reader, packetLength);
		}
	}
}
