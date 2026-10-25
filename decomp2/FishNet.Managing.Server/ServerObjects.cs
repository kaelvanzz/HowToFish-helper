using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Object;
using FishNet.Managing.Scened;
using FishNet.Managing.Timing;
using FishNet.Managing.Utility;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Observing;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Extension;
using FishNet.Utility.Performance;
using GameKit.Dependencies.Utilities;
using GameKit.Dependencies.Utilities.Types;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Server;

public class ServerObjects : ManagedObjects
{
	private List<NetworkObject> _observerChangedObjectsCache = new List<NetworkObject>(100);

	private List<NetworkObject> _timedNetworkObservers = new List<NetworkObject>();

	private int _nextTimedObserversIndex;

	private PooledWriter _writer = new PooledWriter();

	private Queue<int> _emptiedTimedIndexes = new Queue<int>();

	internal Dictionary<int, uint> RecentlyDespawnedIds = new Dictionary<int, uint>();

	private Queue<int> _objectIdCache = new Queue<int>();

	private List<NetworkBehaviour> _dirtySyncTypeBehaviours = new List<NetworkBehaviour>(20);

	private HashSet<NetworkObject> _pendingDestroy = new HashSet<NetworkObject>();

	private List<(int, List<NetworkObject>)> _loadedSceneNetworkObjects = new List<(int, List<NetworkObject>)>();

	private List<NetworkObject> _spawnCache = new List<NetworkObject>();

	private bool _scenesLoading;

	private uint _cleanRecentlyDespawnedMaxTicks => base.NetworkManager.TimeManager.TimeToTicks(30.0, TickRounding.RoundUp);

	public event Action<NetworkConnection> OnPreDestroyClientObjects;

	private void Observers_OnUpdate()
	{
		UpdateTimedObservers();
	}

	private void UpdateTimedObservers()
	{
		if (!base.NetworkManager.IsServerStarted || !base.NetworkManager.TimeManager.FrameTicked)
		{
			return;
		}
		int count = _timedNetworkObservers.Count;
		if (count == 0)
		{
			return;
		}
		float num = 1f + ((float)base.NetworkManager.ServerManager.Clients.Count * 0.005f + (float)_timedNetworkObservers.Count * 0.0005f);
		float num2 = Mathf.Min(0.5f * num, base.NetworkManager.ObserverManager.MaximumTimedObserversDuration);
		uint num3 = base.NetworkManager.TimeManager.TimeToTicks(num2, TickRounding.RoundUp);
		int num4 = Mathf.CeilToInt((float)count / (float)num3);
		if (num4 > _timedNetworkObservers.Count)
		{
			num4 = _timedNetworkObservers.Count;
		}
		List<NetworkConnection> list = RetrieveAuthenticatedConnections();
		List<NetworkObject> list2 = CollectionCaches<NetworkObject>.RetrieveList();
		for (int i = 0; i < num4; i++)
		{
			if (_nextTimedObserversIndex >= _timedNetworkObservers.Count)
			{
				_nextTimedObserversIndex = 0;
			}
			NetworkObject networkObject = _timedNetworkObservers[_nextTimedObserversIndex++];
			if (networkObject != null)
			{
				list2.Add(networkObject);
			}
		}
		RebuildObservers(list2, list, timedOnly: true);
		CollectionCaches<NetworkConnection>.Store(list);
		CollectionCaches<NetworkObject>.Store(list2);
	}

	public void AddTimedNetworkObserver(NetworkObject networkObject)
	{
		if (_emptiedTimedIndexes.TryDequeue(out var result))
		{
			_timedNetworkObservers[result] = networkObject;
		}
		else
		{
			_timedNetworkObservers.Add(networkObject);
		}
	}

	public void RemoveTimedNetworkObserver(NetworkObject networkObject)
	{
		int num = _timedNetworkObservers.IndexOf(networkObject);
		if (num == -1)
		{
			return;
		}
		_emptiedTimedIndexes.Enqueue(num);
		_timedNetworkObservers[num] = null;
		if (_emptiedTimedIndexes.Count <= 20)
		{
			return;
		}
		List<NetworkObject> list = CollectionCaches<NetworkObject>.RetrieveList();
		foreach (NetworkObject timedNetworkObserver in _timedNetworkObservers)
		{
			if (!(timedNetworkObserver == null))
			{
				list.Add(timedNetworkObserver);
			}
		}
		CollectionCaches<NetworkObject>.Store(_timedNetworkObservers);
		_timedNetworkObservers = list;
		_emptiedTimedIndexes.Clear();
	}

	private List<NetworkConnection> RetrieveAuthenticatedConnections()
	{
		List<NetworkConnection> list = CollectionCaches<NetworkConnection>.RetrieveList();
		foreach (NetworkConnection value in base.NetworkManager.ServerManager.Clients.Values)
		{
			if (value.IsAuthenticated)
			{
				list.Add(value);
			}
		}
		return list;
	}

	private List<NetworkObject> RetrieveOrderedSpawnedObjects()
	{
		List<NetworkObject> spawnedNetworkObjects = GetSpawnedNetworkObjects();
		List<NetworkObject> result = SortRootAndNestedByInitializeOrder(spawnedNetworkObjects);
		CollectionCaches<NetworkObject>.Store(spawnedNetworkObjects);
		return result;
	}

	private List<NetworkObject> GetSpawnedNetworkObjects()
	{
		return Spawned.ValuesToList(useCache: true);
	}

	internal List<NetworkObject> SortRootAndNestedByInitializeOrder(List<NetworkObject> nobs)
	{
		List<NetworkObject> list = CollectionCaches<NetworkObject>.RetrieveList();
		foreach (NetworkObject nob in nobs)
		{
			if (!nob.IsNested)
			{
				list.AddOrdered(nob);
			}
		}
		List<NetworkObject> list2 = CollectionCaches<NetworkObject>.RetrieveList();
		List<NetworkObject> list3 = CollectionCaches<NetworkObject>.RetrieveList();
		foreach (NetworkObject item in list)
		{
			List<NetworkObject> networkObjects = item.GetNetworkObjects(GetNetworkObjectOption.AllNestedRecursive);
			foreach (NetworkObject item2 in networkObjects)
			{
				if (list3.Contains(item2))
				{
					Debug.LogError("Nested cache already contains item [" + item2.name + "]. Source [" + item.name + "]. Please report this error.");
				}
				else
				{
					list3.AddOrdered(item2);
				}
			}
			CollectionCaches<NetworkObject>.Store(networkObjects);
			list2.Add(item);
			list2.AddRange(list3);
			list3.Clear();
		}
		CollectionCaches<NetworkObject>.Store(list);
		CollectionCaches<NetworkObject>.Store(list3);
		return list2;
	}

	private void RemoveFromObserversWithoutSynchronization(NetworkConnection connection)
	{
		List<NetworkObject> observerChangedObjectsCache = _observerChangedObjectsCache;
		foreach (NetworkObject value in Spawned.Values)
		{
			if (value.RemoveObserver(connection))
			{
				observerChangedObjectsCache.Add(value);
			}
		}
		for (int i = 0; i < observerChangedObjectsCache.Count; i++)
		{
			observerChangedObjectsCache[i].InvokeOnServerDespawn(connection);
		}
		observerChangedObjectsCache.Clear();
	}

	public void RebuildObservers(bool timedOnly = false)
	{
		List<NetworkObject> list = RetrieveOrderedSpawnedObjects();
		List<NetworkConnection> list2 = RetrieveAuthenticatedConnections();
		RebuildObservers(list, list2, timedOnly);
		CollectionCaches<NetworkObject>.Store(list);
		CollectionCaches<NetworkConnection>.Store(list2);
	}

	public void RebuildObservers(NetworkObject nob, bool timedOnly = false)
	{
		List<NetworkObject> list = CollectionCaches<NetworkObject>.RetrieveList(nob);
		List<NetworkConnection> list2 = RetrieveAuthenticatedConnections();
		RebuildObservers(list, list2, timedOnly);
		CollectionCaches<NetworkObject>.Store(list);
		CollectionCaches<NetworkConnection>.Store(list2);
	}

	public void RebuildObservers(NetworkConnection connection, bool timedOnly = false)
	{
		List<NetworkObject> list = RetrieveOrderedSpawnedObjects();
		List<NetworkConnection> list2 = CollectionCaches<NetworkConnection>.RetrieveList(connection);
		RebuildObservers(list, list2, timedOnly);
		CollectionCaches<NetworkObject>.Store(list);
		CollectionCaches<NetworkConnection>.Store(list2);
	}

	public void RebuildObservers(IList<NetworkObject> nobs, bool timedOnly = false)
	{
		List<NetworkConnection> list = RetrieveAuthenticatedConnections();
		RebuildObservers(nobs, list, timedOnly);
		CollectionCaches<NetworkConnection>.Store(list);
	}

	public void RebuildObservers(IList<NetworkConnection> connections, bool timedOnly = false)
	{
		List<NetworkObject> list = RetrieveOrderedSpawnedObjects();
		RebuildObservers(list, connections, timedOnly);
		CollectionCaches<NetworkObject>.Store(list);
	}

	public void RebuildObservers(IList<NetworkObject> nobs, NetworkConnection conn, bool timedOnly = false)
	{
		List<NetworkConnection> list = CollectionCaches<NetworkConnection>.RetrieveList(conn);
		RebuildObservers(nobs, list, timedOnly);
		CollectionCaches<NetworkConnection>.Store(list);
	}

	public void RebuildObservers(NetworkObject networkObject, IList<NetworkConnection> connections, bool timedOnly = false)
	{
		List<NetworkObject> list = CollectionCaches<NetworkObject>.RetrieveList(networkObject);
		RebuildObservers(list, connections, timedOnly);
		CollectionCaches<NetworkObject>.Store(list);
	}

	public void RebuildObservers(IList<NetworkObject> nobs, IList<NetworkConnection> conns, bool timedOnly = false)
	{
		List<NetworkObject> list = CollectionCaches<NetworkObject>.RetrieveList();
		int count = conns.Count;
		for (int i = 0; i < count; i++)
		{
			list.Clear();
			NetworkConnection networkConnection = conns[i];
			int count2 = nobs.Count;
			for (int j = 0; j < count2; j++)
			{
				RebuildObservers(nobs[j], networkConnection, list, timedOnly);
			}
			if (_writer.Length <= 0)
			{
				continue;
			}
			base.NetworkManager.TransportManager.SendToClient(0, _writer.GetArraySegment(), networkConnection);
			_writer.Clear();
			foreach (NetworkObject item in list)
			{
				item.OnSpawnServer(networkConnection);
			}
		}
		CollectionCaches<NetworkObject>.Store(list);
	}

	public void RebuildObservers(NetworkObject nob, NetworkConnection conn, bool timedOnly = false)
	{
		if (ApplicationState.IsQuitting())
		{
			return;
		}
		_writer.Clear();
		conn.UpdateHashGridPositions(!timedOnly);
		ObserverStateChange observerStateChange = nob.RebuildObservers(conn, timedOnly);
		switch (observerStateChange)
		{
		case ObserverStateChange.Added:
			WriteSpawn(nob, _writer, conn);
			break;
		case ObserverStateChange.Removed:
			nob.InvokeOnServerDespawn(conn);
			WriteDespawn(nob, nob.GetDefaultDespawnType(), _writer);
			break;
		default:
			return;
		}
		base.NetworkManager.TransportManager.SendToClient(0, _writer.GetArraySegment(), conn);
		if (observerStateChange == ObserverStateChange.Added)
		{
			nob.OnSpawnServer(conn);
		}
		_writer.Clear();
		foreach (NetworkBehaviour runtimeChildNetworkBehaviour in nob.RuntimeChildNetworkBehaviours)
		{
			RebuildObservers(runtimeChildNetworkBehaviour.NetworkObject, conn, timedOnly);
		}
	}

	internal void RebuildObservers(NetworkObject nob, NetworkConnection conn, List<NetworkObject> addedNobs, bool timedOnly = false)
	{
		if (ApplicationState.IsQuitting())
		{
			return;
		}
		conn.UpdateHashGridPositions(!timedOnly);
		switch (nob.RebuildObservers(conn, timedOnly))
		{
		case ObserverStateChange.Added:
			WriteSpawn(nob, _writer, conn);
			addedNobs.Add(nob);
			break;
		case ObserverStateChange.Removed:
			nob.InvokeOnServerDespawn(conn);
			WriteDespawn(nob, nob.GetDefaultDespawnType(), _writer);
			break;
		default:
			return;
		}
		foreach (NetworkBehaviour runtimeChildNetworkBehaviour in nob.RuntimeChildNetworkBehaviours)
		{
			RebuildObservers(runtimeChildNetworkBehaviour.NetworkObject, conn, addedNobs, timedOnly);
		}
	}

	internal void ParseServerRpc(PooledReader reader, NetworkConnection conn, Channel channel)
	{
		int position = reader.Position;
		NetworkBehaviour networkBehaviour = reader.ReadNetworkBehaviour();
		int packetLength = Packets.GetPacketLength(8, reader, channel);
		if (networkBehaviour != null)
		{
			networkBehaviour.ReadServerRpc(position, fromRpcLink: false, 0u, reader, conn, channel);
		}
		else
		{
			SkipDataLength(8, reader, packetLength);
		}
	}

	internal Queue<int> GetObjectIdCache()
	{
		return _objectIdCache;
	}

	internal ServerObjects(NetworkManager networkManager)
	{
		base.Initialize(networkManager);
		networkManager.SceneManager.OnLoadStart += SceneManager_OnLoadStart;
		networkManager.SceneManager.OnActiveSceneSetInternal += SceneManager_OnActiveSceneSet;
		networkManager.TimeManager.OnUpdate += TimeManager_OnUpdate;
	}

	private void TimeManager_OnUpdate()
	{
		if (!base.NetworkManager.IsServerStarted)
		{
			_scenesLoading = false;
			ClearSceneLoadedNetworkObjects();
			return;
		}
		CleanRecentlyDespawned();
		if (!_scenesLoading)
		{
			IterateLoadedScenes(ignoreFrameRestriction: false);
		}
		Observers_OnUpdate();
	}

	private void ClearSceneLoadedNetworkObjects()
	{
		for (int i = 0; i < _loadedSceneNetworkObjects.Count; i++)
		{
			CollectionCaches<NetworkObject>.Store(_loadedSceneNetworkObjects[i].Item2);
		}
		_loadedSceneNetworkObjects.Clear();
	}

	internal void WriteDirtySyncTypes()
	{
		List<NetworkBehaviour> dirtySyncTypeBehaviours = _dirtySyncTypeBehaviours;
		if (dirtySyncTypeBehaviours.Count == 0)
		{
			return;
		}
		for (int i = 0; i < dirtySyncTypeBehaviours.Count; i++)
		{
			if (dirtySyncTypeBehaviours[i].WriteDirtySyncTypes(SyncTypeWriteFlag.Unset))
			{
				dirtySyncTypeBehaviours.RemoveAt(i);
				i--;
			}
		}
	}

	internal void SetDirtySyncType(NetworkBehaviour nb)
	{
		_dirtySyncTypeBehaviours.Add(nb);
	}

	internal void OnServerConnectionState(ServerConnectionStateArgs args)
	{
		if (args.ConnectionState == LocalConnectionState.Started)
		{
			if (base.NetworkManager.ServerManager.IsOnlyOneServerStarted())
			{
				BuildObjectIdCache();
				SetupSceneObjects();
			}
			return;
		}
		if (!base.NetworkManager.ServerManager.IsAnyServerStarted())
		{
			base.DespawnWithoutSynchronization(recursive: true, asServer: true);
			SceneObjects_Internal.Clear();
			_objectIdCache.Clear();
			base.NetworkManager.ClearClientsCollection(base.NetworkManager.ServerManager.Clients);
			return;
		}
		int transportIndex = args.TransportIndex;
		foreach (NetworkConnection value in base.NetworkManager.ServerManager.Clients.Values)
		{
			if (value.TransportIndex == transportIndex)
			{
				RemoveFromObserversWithoutSynchronization(value);
			}
		}
		base.NetworkManager.ClearClientsCollection(base.NetworkManager.ServerManager.Clients, transportIndex);
	}

	internal void ClientDisconnected(NetworkConnection connection)
	{
		RemoveFromObserversWithoutSynchronization(connection);
		if (this.OnPreDestroyClientObjects != null)
		{
			this.OnPreDestroyClientObjects(connection);
		}
		List<NetworkObject> list = CollectionCaches<NetworkObject>.RetrieveList();
		foreach (NetworkObject @object in connection.Objects)
		{
			list.Add(@object);
		}
		int count = list.Count;
		for (int i = 0; i < count; i++)
		{
			NetworkObject networkObject = list[i];
			if (!networkObject.IsDeinitializing && !networkObject.PreventDespawnOnDisconnect)
			{
				base.NetworkManager.ServerManager.Despawn(list[i]);
			}
		}
		CollectionCaches<NetworkObject>.Store(list);
	}

	private void BuildObjectIdCache()
	{
		_objectIdCache.Clear();
		List<int> list = new List<int>();
		for (int i = 0; i < 65534; i++)
		{
			list.Add(i);
		}
		list.Shuffle();
		int count = list.Count;
		for (int j = 0; j < count; j++)
		{
			_objectIdCache.Enqueue(list[j]);
		}
	}

	private void CacheObjectId(NetworkObject nob)
	{
		if (nob.ObjectId != 65535)
		{
			CacheObjectId(nob.ObjectId);
		}
	}

	internal void CacheObjectId(int id)
	{
		if (!_objectIdCache.Contains(id))
		{
			_objectIdCache.Enqueue(id);
		}
		else
		{
			base.NetworkManager.LogError($"Object Id [{id}] already exists within ObjectId Cache. Please report this error.");
		}
	}

	protected internal override bool GetNextNetworkObjectId(out int nextNetworkObjectId)
	{
		if (!_objectIdCache.TryDequeue(out nextNetworkObjectId))
		{
			nextNetworkObjectId = 65535;
			base.NetworkManager.LogError($"No more available ObjectIds. How the heck did you manage to have {ushort.MaxValue} objects spawned at once?");
		}
		return nextNetworkObjectId != 65535;
	}

	private void SceneManager_OnLoadStart(SceneLoadStartEventArgs obj)
	{
		_scenesLoading = true;
	}

	private void SceneManager_OnActiveSceneSet(bool asServer)
	{
		_scenesLoading = false;
		IterateLoadedScenes(ignoreFrameRestriction: true);
	}

	internal void IterateLoadedScenes(bool ignoreFrameRestriction)
	{
		if (!base.NetworkManager.ServerManager.Started)
		{
			ClearSceneLoadedNetworkObjects();
			return;
		}
		for (int i = 0; i < _loadedSceneNetworkObjects.Count; i++)
		{
			(int, List<NetworkObject>) tuple = _loadedSceneNetworkObjects[i];
			if (ignoreFrameRestriction || Time.frameCount > tuple.Item1)
			{
				SetupSceneObjects(tuple.Item2);
				CollectionCaches<NetworkObject>.Store(tuple.Item2);
				_loadedSceneNetworkObjects.RemoveAt(i);
				i--;
			}
		}
	}

	protected internal override void SceneManager_sceneLoaded(Scene s, LoadSceneMode arg1)
	{
		base.SceneManager_sceneLoaded(s, arg1);
		if (base.NetworkManager.ServerManager.Started)
		{
			List<NetworkObject> result = CollectionCaches<NetworkObject>.RetrieveList();
			Scenes.GetSceneNetworkObjects(s, firstOnly: false, errorOnDuplicates: true, ignoreUnsetSceneIds: true, ref result);
			_loadedSceneNetworkObjects.Add((Time.frameCount, result));
			InitializeRootNetworkObjects(result);
		}
	}

	private void InitializeRootNetworkObjects(List<NetworkObject> nobs)
	{
		foreach (NetworkObject nob in nobs)
		{
			nob.SetIsNestedThroughTraversal();
			nob.UnsetInitializedValuesSet();
		}
		foreach (NetworkObject nob2 in nobs)
		{
			if (nob2.IsSceneObject && !nob2.IsNested)
			{
				nob2.SetInitializedValues(null);
			}
		}
	}

	protected internal void SetupSceneObjects()
	{
		Scene scene = DDOL.GetDDOL().gameObject.scene;
		bool isLoaded = scene.isLoaded;
		bool flag = true;
		for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
		{
			Scene sceneAt = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
			if (isLoaded && sceneAt.handle == scene.handle)
			{
				flag = false;
			}
			SetupSceneObjects(sceneAt);
		}
		if (flag)
		{
			SetupSceneObjects(scene);
		}
	}

	private void SetupSceneObjects(Scene s)
	{
		if (s.IsValid())
		{
			List<NetworkObject> result = CollectionCaches<NetworkObject>.RetrieveList();
			Scenes.GetSceneNetworkObjects(s, firstOnly: false, errorOnDuplicates: true, ignoreUnsetSceneIds: true, ref result);
			SetupSceneObjects(result);
			CollectionCaches<NetworkObject>.Store(result);
		}
	}

	private void SetupSceneObjects(List<NetworkObject> sceneNobs)
	{
		for (int i = 0; i < sceneNobs.Count; i++)
		{
			if (sceneNobs[i] == null)
			{
				sceneNobs.RemoveAt(i--);
			}
		}
		InitializeRootNetworkObjects(sceneNobs);
		List<NetworkObject> list = SortRootAndNestedByInitializeOrder(sceneNobs);
		bool isHostStarted = base.NetworkManager.IsHostStarted;
		int count = list.Count;
		for (int j = 0; j < count; j++)
		{
			NetworkObject networkObject = list[j];
			if ((!(networkObject.CurrentParentNetworkBehaviour == null) && !networkObject.CurrentParentNetworkBehaviour.IsSpawned) || !networkObject.GetIsNetworked() || !networkObject.IsSceneObject || !networkObject.IsDeinitializing)
			{
				continue;
			}
			if (!networkObject.WasActiveDuringEdit_Set1)
			{
				base.NetworkManager.LogError("NetworkObject " + networkObject.name + " in scene " + networkObject.gameObject.scene.name + " needs to be reserialized. Please use the Fish-Networking menu -> Utility -> Reserialize NetworkObjects.");
				continue;
			}
			AddToSceneObjects(networkObject);
			if (networkObject.WasActiveDuringEdit || networkObject.gameObject.activeInHierarchy)
			{
				if (!isHostStarted)
				{
					SetupWithoutSynchronization(networkObject);
				}
				else
				{
					SpawnWithoutChecks(networkObject);
				}
			}
		}
		CollectionCaches<NetworkObject>.Store(list);
	}

	private bool SetupWithoutSynchronization(NetworkObject nob, NetworkConnection ownerConnection = null, int? objectId = null, bool initializeEarly = true)
	{
		if (nob.GetIsNetworked())
		{
			int nextNetworkObjectId;
			if (objectId.HasValue)
			{
				nextNetworkObjectId = objectId.Value;
			}
			else if (!GetNextNetworkObjectId(out nextNetworkObjectId))
			{
				return false;
			}
			if (initializeEarly)
			{
				nob.InitializeEarly(base.NetworkManager, nextNetworkObjectId, ownerConnection, asServer: true);
			}
			base.AddToSpawned(nob, asServer: true);
			nob.gameObject.SetActive(value: true);
			nob.Initialize(asServer: true, invokeSyncTypeCallbacks: true);
			return true;
		}
		return false;
	}

	internal void Spawn(NetworkObject networkObject, NetworkConnection ownerConnection = null, Scene scene = default(Scene))
	{
		bool flag = false;
		if (networkObject == null)
		{
			base.NetworkManager.LogError("Specified networkObject is null.");
			return;
		}
		if (!base.NetworkManager.ServerManager.Started)
		{
			if (!base.NetworkManager.ClientManager.Started)
			{
				base.NetworkManager.LogWarning("Cannot spawn object because server nor client are active.");
				return;
			}
			if (!base.NetworkManager.ServerManager.GetAllowPredictedSpawning())
			{
				base.NetworkManager.LogWarning("Cannot spawn object because server is not active and predicted spawning is not enabled.");
				return;
			}
			if (!CanPredictedSpawn(networkObject, base.NetworkManager.ClientManager.Connection, asServer: false) || !networkObject.PredictedSpawn.OnTrySpawnClient())
			{
				return;
			}
			flag = true;
		}
		if (!networkObject.gameObject.scene.IsValid())
		{
			base.NetworkManager.LogError(networkObject.name + " is a prefab. You must instantiate the prefab first, then use Spawn on the instantiated copy.");
			return;
		}
		if (ownerConnection != null && ownerConnection.IsActive && !ownerConnection.LoadedStartScenes(!flag))
		{
			base.NetworkManager.LogWarning(networkObject.name + " was spawned but it's recommended to not spawn objects for connections until they have loaded start scenes. You can be notified when a connection loads start scenes by using connection.OnLoadedStartScenes on the connection, or SceneManager.OnClientLoadStartScenes.");
		}
		if (networkObject.IsSpawned)
		{
			base.NetworkManager.LogWarning(networkObject.name + " is already spawned.");
			return;
		}
		NetworkBehaviour currentParentNetworkBehaviour = networkObject.CurrentParentNetworkBehaviour;
		if (currentParentNetworkBehaviour != null && !currentParentNetworkBehaviour.IsSpawned)
		{
			base.NetworkManager.LogError($"{networkObject.name} cannot be spawned because it has a parent NetworkObject {currentParentNetworkBehaviour} which is not spawned.");
			return;
		}
		if (scene.IsValid())
		{
			if (networkObject.transform.parent != null)
			{
				base.NetworkManager.LogError($"{networkObject.name} cannot be moved to scene name {scene.name}, handle {scene.handle} because {networkObject.name} is not root and only root objects may be moved.");
				return;
			}
			UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(networkObject.gameObject, scene);
		}
		if (flag)
		{
			base.NetworkManager.ClientManager.Objects.PredictedSpawn(networkObject, ownerConnection);
		}
		else
		{
			SpawnWithoutChecks(networkObject, null, ownerConnection);
		}
	}

	private void SpawnWithoutChecks(NetworkObject networkObject, List<NetworkObject> recursiveSpawnCache = null, NetworkConnection ownerConnection = null, int? objectId = null, bool rebuildObservers = true, bool initializeEarly = true, bool isRecursiveIteration = false)
	{
		networkObject.SetIsNetworked(value: true);
		List<NetworkObject> value = (isRecursiveIteration ? null : networkObject.GetNetworkObjects(GetNetworkObjectOption.AllNestedRecursive));
		if (SetupWithoutSynchronization(networkObject, ownerConnection, objectId, initializeEarly))
		{
			_spawnCache.Add(networkObject);
		}
		if (value != null)
		{
			foreach (NetworkObject item in value)
			{
				if (item.gameObject.activeInHierarchy || item.State == NetworkObjectState.Spawned)
				{
					SpawnWithoutChecks(item, null, ownerConnection, null, rebuildObservers: true, initializeEarly: true, isRecursiveIteration: true);
				}
			}
		}
		bool flag = recursiveSpawnCache == null;
		if (flag)
		{
			recursiveSpawnCache = CollectionCaches<NetworkObject>.RetrieveList();
		}
		recursiveSpawnCache.AddRange(_spawnCache);
		_spawnCache.Clear();
		if (rebuildObservers)
		{
			RebuildObservers(recursiveSpawnCache);
		}
		int count = recursiveSpawnCache.Count;
		if (base.NetworkManager.IsClientStarted)
		{
			int num = count;
			NetworkConnection connection = base.NetworkManager.ClientManager.Connection;
			for (int i = 0; i < num; i++)
			{
				NetworkObject networkObject2 = recursiveSpawnCache[i];
				networkObject2.SetRenderersVisible(networkObject2.Observers.Contains(connection));
			}
		}
		CollectionCaches<NetworkObject>.StoreAndDefault(ref value);
		if (flag)
		{
			CollectionCaches<NetworkObject>.Store(recursiveSpawnCache);
		}
	}

	internal void ReadSpawn(PooledReader reader, NetworkConnection conn)
	{
		ushort spawnLength = reader.ReadUInt16Unpacked();
		int readStartPosition = reader.Position;
		SpawnType spawnType = (SpawnType)reader.ReadUInt8Unpacked();
		bool flag = spawnType.FastContains(SpawnType.Scene);
		bool isGlobal = spawnType.FastContains(SpawnType.InstantiatedGlobal);
		ReadNestedSpawnIds(reader, spawnType, out var _, out var parentObjectId, out var _);
		int initializeOrder;
		ushort collectionid;
		int num = reader.ReadNetworkObjectForSpawn(out initializeOrder, out collectionid);
		if (conn.PredictedObjectIds.Count == 0 || !conn.PredictedObjectIds.TryDequeue(out var result))
		{
			reader.Clear();
			conn.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection {conn.ClientId} used predicting spawning without any Ids in queue.");
			return;
		}
		if (num != result)
		{
			reader.Clear();
			conn.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Connection {conn.ClientId} used predicted Id of {num} while the server Id is {result}.");
			return;
		}
		NetworkObject nob = null;
		NetworkConnection owner = null;
		int trafficWritten;
		if (parentObjectId.HasValue && !Spawned.TryGetValueIL2CPP(parentObjectId.Value, out var _))
		{
			base.NetworkManager.Log($"Predicted spawn failed due to the NetworkObject's parent not being found. Scene object: {flag}, ObjectId {num}, CollectionId {collectionid}.");
			SendFailedResponse(num);
			return;
		}
		owner = reader.ReadNetworkConnection();
		ReadTransformProperties(reader, out var localPosition, out var localRotation, out var localScale);
		ulong sceneId = 0uL;
		string empty = string.Empty;
		string empty2 = string.Empty;
		if (flag)
		{
			ReadSceneObjectId(reader, out sceneId);
			nob = GetSceneNetworkObject(sceneId, empty, empty2);
		}
		else
		{
			int prefabId = reader.ReadNetworkObjectId();
			ObjectPoolRetrieveOption options = ObjectPoolRetrieveOption.MakeActive | ObjectPoolRetrieveOption.LocalSpace;
			nob = base.NetworkManager.GetPooledInstantiated(prefabId, collectionid, options, null, localPosition, localRotation, localScale, asServer: false);
		}
		if (nob == null)
		{
			base.NetworkManager.Log($"Predicted spawn failed due to the NetworkObject not being found. Scene object: {flag}, ObjectId {num}, CollectionId {collectionid}.");
			SendFailedResponse(num);
			return;
		}
		if (!nob.WasActiveDuringEdit_Set1)
		{
			string text = (flag ? ("in scene " + nob.gameObject.scene.name) : "prefab");
			base.NetworkManager.LogError("NetworkObject " + nob.name + " " + text + ". Please use the Fish-Networking menu -> Utility -> Reserialize NetworkObjects.");
		}
		if (flag)
		{
			nob.transform.SetLocalPositionRotationAndScale(localPosition, localRotation, localScale);
		}
		if (CanPredictedSpawn(nob, conn, asServer: true, reader))
		{
			nob.SetIsGlobal(isGlobal);
			nob.SetIsNetworked(value: true);
			nob.InitializeEarly(base.NetworkManager, num, owner, asServer: true);
			nob.InitializePredictedObject_Server(conn);
			ReadPayload(conn, nob, reader);
			ReadRpcLinks(reader);
			ReadSyncTypesForSpawn(reader);
			if (!nob.PredictedSpawn.OnTrySpawnServer(conn, owner))
			{
				SendFailedResponse(num);
				return;
			}
			List<NetworkConnection> value2 = RetrieveAuthenticatedConnections();
			SendSuccessResponse(num);
			CollectionCaches<NetworkConnection>.Store(value2);
		}
		void SendFailedResponse(int lObjectId)
		{
			SkipRemainingSpawnLength();
			if (nob != null)
			{
				UnityEngine.Object.Destroy(nob.gameObject);
			}
			PooledWriter pooledWriter3 = WriteResponseHeader(success: false, lObjectId);
			trafficWritten = pooledWriter3.Length;
			conn.SendToClient(0, pooledWriter3.GetArraySegment());
			WriterPool.Store(pooledWriter3);
		}
		void SendSuccessResponse(int lObjectId)
		{
			PooledWriter pooledWriter = WriteResponseHeader(success: true, lObjectId);
			trafficWritten = pooledWriter.Length;
			SpawnWithoutChecks(nob, null, owner, lObjectId, rebuildObservers: true, initializeEarly: false);
			conn.SendToClient(0, pooledWriter.GetArraySegment());
			WriterPool.Store(pooledWriter);
		}
		void SkipRemainingSpawnLength()
		{
			int value3 = spawnLength - (reader.Position - readStartPosition);
			reader.Skip(value3);
		}
		PooledWriter WriteResponseHeader(bool success, int lObjectId)
		{
			PooledWriter pooledWriter2 = WriterPool.Retrieve();
			pooledWriter2.WritePacketIdUnpacked(PacketId.PredictedSpawnResult);
			pooledWriter2.WriteBoolean(success);
			pooledWriter2.WriteNetworkObjectId(lObjectId);
			if (base.NetworkManager.ServerManager.Objects.GetObjectIdCache().TryDequeue(out var result2))
			{
				conn.PredictedObjectIds.Enqueue(result2);
			}
			else
			{
				result2 = 65535;
			}
			pooledWriter2.WriteNetworkObjectId(result2);
			return pooledWriter2;
		}
	}

	private void CleanRecentlyDespawned()
	{
		if (!base.NetworkManager.TimeManager.FrameTicked)
		{
			return;
		}
		List<int> list = CollectionCaches<int>.RetrieveList();
		uint cleanRecentlyDespawnedMaxTicks = _cleanRecentlyDespawnedMaxTicks;
		uint localTick = base.NetworkManager.TimeManager.LocalTick;
		int num = Mathf.Max(20, (int)((float)RecentlyDespawnedIds.Count * 0.05f));
		int num2 = 0;
		foreach (KeyValuePair<int, uint> recentlyDespawnedId in RecentlyDespawnedIds)
		{
			if ((long)(localTick - recentlyDespawnedId.Value) > (long)cleanRecentlyDespawnedMaxTicks)
			{
				list.Add(recentlyDespawnedId.Key);
			}
			num2++;
			if (num2 == num)
			{
				break;
			}
		}
		int count = list.Count;
		for (int i = 0; i < count; i++)
		{
			RecentlyDespawnedIds.Remove(list[i]);
		}
		CollectionCaches<int>.Store(list);
	}

	public bool RecentlyDespawned(int objectId, uint ticks)
	{
		if (!RecentlyDespawnedIds.TryGetValue(objectId, out var value))
		{
			return false;
		}
		return base.NetworkManager.TimeManager.LocalTick - value <= ticks;
	}

	internal void AddToPending(NetworkObject nob)
	{
		_pendingDestroy.Add(nob);
	}

	internal bool RemoveFromPending(NetworkObject nob)
	{
		return _pendingDestroy.Remove(nob);
	}

	internal NetworkObject GetFromPending(int objectId)
	{
		bool flag = false;
		foreach (NetworkObject item in _pendingDestroy)
		{
			if (item == null)
			{
				flag = true;
			}
			else if (item.ObjectId == objectId)
			{
				return item;
			}
		}
		if (flag)
		{
			HashSet<NetworkObject> hashSet = CollectionCaches<NetworkObject>.RetrieveHashSet();
			foreach (NetworkObject item2 in _pendingDestroy)
			{
				if (item2 != null)
				{
					hashSet.Add(item2);
				}
			}
			CollectionCaches<NetworkObject>.Store(_pendingDestroy);
			_pendingDestroy = hashSet;
		}
		return null;
	}

	internal void DestroyPending()
	{
		foreach (NetworkObject item in _pendingDestroy)
		{
			if (item != null)
			{
				UnityEngine.Object.Destroy(item.gameObject);
			}
		}
		_pendingDestroy.Clear();
	}

	internal override void Despawn(NetworkObject networkObject, DespawnType despawnType, bool asServer)
	{
		bool flag = false;
		if (networkObject == null)
		{
			base.NetworkManager.LogWarning("NetworkObject cannot be despawned because it is null.");
			return;
		}
		if (networkObject.IsDeinitializing)
		{
			base.NetworkManager.LogWarning("Object " + networkObject.name + " cannot be despawned because it is already deinitializing.");
			return;
		}
		if (!base.NetworkManager.ServerManager.Started)
		{
			if (!base.NetworkManager.ClientManager.Started)
			{
				base.NetworkManager.LogWarning("Cannot despawn object because server nor client are active.");
				return;
			}
			if (!base.NetworkManager.ServerManager.GetAllowPredictedSpawning())
			{
				base.NetworkManager.LogWarning("Cannot despawn object because server is not active and predicted spawning is not enabled.");
				return;
			}
			if (!CanPredictedDespawn(networkObject, base.NetworkManager.ClientManager.Connection, asServer: false))
			{
				return;
			}
			flag = true;
		}
		if (!networkObject.gameObject.scene.IsValid())
		{
			base.NetworkManager.LogError(networkObject.name + " is a prefab. You must instantiate the prefab first, then use Spawn on the instantiated copy.");
			return;
		}
		if (flag)
		{
			base.NetworkManager.ClientManager.Objects.PredictedDespawn(networkObject);
			return;
		}
		FinalizeDespawn(networkObject, despawnType);
		RecentlyDespawnedIds[networkObject.ObjectId] = base.NetworkManager.TimeManager.LocalTick;
		base.Despawn(networkObject, despawnType, asServer);
	}

	internal override void NetworkObjectDestroyed(NetworkObject nob, bool asServer)
	{
		if (!nob.IsDeinitializing)
		{
			FinalizeDespawn(nob, DespawnType.Destroy);
		}
		base.NetworkObjectDestroyed(nob, asServer);
	}

	private void FinalizeDespawn(NetworkObject nob, DespawnType despawnType)
	{
		List<NetworkBehaviour> dirtySyncTypeBehaviours = _dirtySyncTypeBehaviours;
		if (!(nob != null) || nob.ObjectId == 65535)
		{
			return;
		}
		int i = 0;
		for (int count = nob.NetworkBehaviours.Count; i < count; i++)
		{
			NetworkBehaviour networkBehaviour = nob.NetworkBehaviours[i];
			if (networkBehaviour.SyncTypeDirty && networkBehaviour.WriteDirtySyncTypes(SyncTypeWriteFlag.IgnoreInterval | SyncTypeWriteFlag.ForceReliable))
			{
				dirtySyncTypeBehaviours.Remove(networkBehaviour);
			}
		}
		WriteDespawnAndSend(nob, despawnType);
		CacheObjectId(nob);
	}

	private void WriteDespawnAndSend(NetworkObject nob, DespawnType despawnType)
	{
		HashSet<NetworkConnection> observers = nob.Observers;
		if (observers.Count != 0)
		{
			PooledWriter pooledWriter = WriterPool.Retrieve();
			WriteDespawn(nob, despawnType, pooledWriter);
			ArraySegment<byte> arraySegment = pooledWriter.GetArraySegment();
			List<NetworkConnection> list = CollectionCaches<NetworkConnection>.RetrieveList();
			list.AddRange(observers);
			int count = list.Count;
			for (int i = 0; i < count; i++)
			{
				NetworkConnection networkConnection = list[i];
				nob.InvokeOnServerDespawn(networkConnection);
				base.NetworkManager.TransportManager.SendToClient(0, arraySegment, networkConnection);
			}
			pooledWriter.Store();
			CollectionCaches<NetworkConnection>.Store(list);
		}
	}

	internal void ReadDespawn(Reader reader, NetworkConnection conn)
	{
		NetworkObject networkObject = reader.ReadNetworkObject();
		if (!(networkObject == null) && !networkObject.IsDeinitializing && CanPredictedDespawn(networkObject, conn, asServer: true))
		{
			networkObject.Despawn();
		}
	}
}
