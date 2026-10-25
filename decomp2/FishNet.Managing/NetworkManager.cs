using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Component.ColliderRollback;
using FishNet.Configuring.EditorCloning;
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing.Client;
using FishNet.Managing.Debugging;
using FishNet.Managing.Logging;
using FishNet.Managing.Object;
using FishNet.Managing.Observing;
using FishNet.Managing.Predicting;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
using FishNet.Managing.Statistic;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting;
using FishNet.Utility.Performance;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Managing;

[DefaultExecutionOrder(-32768)]
[DisallowMultipleComponent]
[AddComponentMenu("FishNet/Manager/NetworkManager")]
public sealed class NetworkManager : MonoBehaviour
{
	public enum PersistenceType
	{
		DestroyNewest,
		DestroyOldest,
		AllowMultiple
	}

	[Tooltip("Logging configuration to use. When empty default logging settings will be used.")]
	[SerializeField]
	private LoggingConfiguration _logging;

	private const string ERROR_LOGGING_PREFIX = "Error - ";

	private const string WARNING_LOGGING_PREFIX = "Warning - ";

	private const string COMMON_LOGGING_PREFIX = "Log - ";

	[Tooltip("Collection to use for spawnable objects.")]
	[SerializeField]
	private PrefabObjects _spawnablePrefabs;

	private Dictionary<ushort, PrefabObjects> _runtimeSpawnablePrefabs = new Dictionary<ushort, PrefabObjects>();

	private Dictionary<string, List<Action<UnityEngine.Component>>> _pendingInvokes = new Dictionary<string, List<Action<UnityEngine.Component>>>();

	private Dictionary<string, UnityEngine.Component> _registeredComponents = new Dictionary<string, UnityEngine.Component>();

	private static List<NetworkManager> _instances = new List<NetworkManager>();

	internal static ushort StartingRpcLinkIndex;

	[Tooltip("True to have your application run while in the background.")]
	[SerializeField]
	private bool _runInBackground = true;

	[Tooltip("True to make this instance DontDestroyOnLoad. This is typical if you only want one NetworkManager.")]
	[SerializeField]
	private bool _dontDestroyOnLoad = true;

	[Tooltip("Object pool to use for this NetworkManager. Value may be null.")]
	[SerializeField]
	private ObjectPool _objectPool;

	[Tooltip("How to persist when other NetworkManagers are introduced.")]
	[SerializeField]
	private PersistenceType _persistence;

	private bool _canPersist;

	public const string FISHNET_VERSION = "4.6.12";

	internal const ushort MAXIMUM_FRAMERATE = 500;

	public RollbackManager RollbackManager { get; private set; }

	[Obsolete("Use IsClientOnlyStarted. Note the difference between IsClientOnlyInitialized and IsClientOnlyStarted.")]
	public bool IsClientOnly => IsClientOnlyStarted;

	[Obsolete("Use IsServerOnlyStarted. Note the difference between IsServerOnlyInitialized and IsServerOnlyStarted.")]
	public bool IsServerOnly => IsServerOnlyStarted;

	[Obsolete("Use IsHostStarted. Note the difference between IsHostInitialized and IsHostStarted.")]
	public bool IsHost => IsHostStarted;

	[Obsolete("Use IsClientStarted. Note the difference between IsClientInitialized and IsClientStarted.")]
	public bool IsClient => IsClientStarted;

	[Obsolete("Use IsServerStarted. Note the difference between IsServerInitialized and IsServerStarted.")]
	public bool IsServer => IsServerStarted;

	public bool IsServerStarted => ServerManager.Started;

	public bool IsServerOnlyStarted
	{
		get
		{
			if (IsServerStarted)
			{
				return !IsClientStarted;
			}
			return false;
		}
	}

	public bool IsClientStarted
	{
		get
		{
			if (ClientManager.Started)
			{
				return ClientManager.Connection.IsAuthenticated;
			}
			return false;
		}
	}

	public bool IsClientOnlyStarted
	{
		get
		{
			if (!IsServerStarted)
			{
				return IsClientStarted;
			}
			return false;
		}
	}

	public bool IsHostStarted
	{
		get
		{
			if (IsServerStarted)
			{
				return IsClientStarted;
			}
			return false;
		}
	}

	public bool IsOffline
	{
		get
		{
			if (!IsServerStarted)
			{
				return !IsClientStarted;
			}
			return false;
		}
	}

	public PrefabObjects SpawnablePrefabs
	{
		get
		{
			return _spawnablePrefabs;
		}
		set
		{
			_spawnablePrefabs = value;
		}
	}

	public IReadOnlyDictionary<ushort, PrefabObjects> RuntimeSpawnablePrefabs => _runtimeSpawnablePrefabs;

	public bool Initialized { get; private set; }

	public static IReadOnlyList<NetworkManager> Instances
	{
		get
		{
			for (int i = 0; i < _instances.Count; i++)
			{
				if (_instances[i] == null)
				{
					_instances.RemoveAt(i);
					i--;
				}
			}
			return _instances;
		}
	}

	internal PredictionManager PredictionManager { get; private set; }

	public ServerManager ServerManager { get; private set; }

	public ClientManager ClientManager { get; private set; }

	public TransportManager TransportManager { get; private set; }

	public TimeManager TimeManager { get; private set; }

	public SceneManager SceneManager { get; private set; }

	public ObserverManager ObserverManager { get; private set; }

	public DebugManager DebugManager { get; private set; }

	public StatisticsManager StatisticsManager { get; private set; }

	[APIExclude]
	public static NetworkConnection EmptyConnection { get; private set; } = new NetworkConnection();


	public ObjectPool ObjectPool => _objectPool;

	private void InitializeLogging()
	{
		if (_logging == null)
		{
			_logging = ScriptableObject.CreateInstance<LevelLoggingConfiguration>();
		}
		else
		{
			_logging = _logging.Clone();
		}
		_logging.InitializeOnce();
	}

	internal bool InternalCanLog(LoggingType loggingType)
	{
		return _logging.CanLog(loggingType);
	}

	internal void InternalLog(string value)
	{
		_logging.Log(value);
	}

	internal void InternalLog(LoggingType loggingType, string value)
	{
		switch (loggingType)
		{
		case LoggingType.Common:
			_logging.Log(value);
			break;
		case LoggingType.Warning:
			_logging.LogWarning(value);
			break;
		case LoggingType.Error:
			_logging.LogError(value);
			break;
		}
	}

	internal void InternalLogWarning(string value)
	{
		_logging.LogWarning(value);
	}

	internal void InternalLogError(string value)
	{
		_logging.LogError(value);
	}

	public NetworkObject GetPooledInstantiated(NetworkObject prefab, Transform parent, bool asServer)
	{
		return GetPooledInstantiated(prefab.PrefabId, prefab.SpawnableCollectionId, ObjectPoolRetrieveOption.MakeActive, parent, null, null, null, asServer);
	}

	public NetworkObject GetPooledInstantiated(NetworkObject prefab, bool asServer)
	{
		return GetPooledInstantiated(prefab.PrefabId, prefab.SpawnableCollectionId, ObjectPoolRetrieveOption.MakeActive, null, null, null, null, asServer);
	}

	public NetworkObject GetPooledInstantiated(NetworkObject prefab, Vector3 position, Quaternion rotation, bool asServer)
	{
		return GetPooledInstantiated(prefab.PrefabId, prefab.SpawnableCollectionId, ObjectPoolRetrieveOption.MakeActive, null, position, rotation, null, asServer);
	}

	public NetworkObject GetPooledInstantiated(GameObject prefab, bool asServer)
	{
		if (SetPrefabInformation(prefab, out var _, out var prefabId, out var collectionId))
		{
			return GetPooledInstantiated(prefabId, collectionId, ObjectPoolRetrieveOption.MakeActive, null, null, null, null, asServer);
		}
		return null;
	}

	public NetworkObject GetPooledInstantiated(GameObject prefab, Transform parent, bool asServer)
	{
		if (SetPrefabInformation(prefab, out var _, out var prefabId, out var collectionId))
		{
			return GetPooledInstantiated(prefabId, collectionId, ObjectPoolRetrieveOption.MakeActive, parent, null, null, null, asServer);
		}
		return null;
	}

	public NetworkObject GetPooledInstantiated(GameObject prefab, Vector3 position, Quaternion rotation, bool asServer)
	{
		if (SetPrefabInformation(prefab, out var _, out var prefabId, out var collectionId))
		{
			return GetPooledInstantiated(prefabId, collectionId, ObjectPoolRetrieveOption.MakeActive, null, position, rotation, null, asServer);
		}
		return null;
	}

	public NetworkObject GetPooledInstantiated(NetworkObject prefab, Vector3 position, Quaternion rotation, Transform parent, bool asServer)
	{
		return GetPooledInstantiated(prefab.PrefabId, prefab.SpawnableCollectionId, ObjectPoolRetrieveOption.MakeActive, parent, position, rotation, null, asServer);
	}

	public NetworkObject GetPooledInstantiated(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent, bool asServer)
	{
		if (SetPrefabInformation(prefab, out var _, out var prefabId, out var collectionId))
		{
			return GetPooledInstantiated(prefabId, collectionId, ObjectPoolRetrieveOption.MakeActive, parent, position, rotation, null, asServer);
		}
		return null;
	}

	public NetworkObject GetPooledInstantiated(int prefabId, ushort collectionId, bool asServer)
	{
		return GetPooledInstantiated(prefabId, collectionId, ObjectPoolRetrieveOption.MakeActive, null, null, null, null, asServer);
	}

	public NetworkObject GetPooledInstantiated(int prefabId, ushort collectionId, Vector3 position, Quaternion rotation, bool asServer)
	{
		return GetPooledInstantiated(prefabId, collectionId, ObjectPoolRetrieveOption.MakeActive, null, position, rotation, null, asServer);
	}

	[Obsolete("Use GetPooledInstantiated(int, ushort, RetrieveOption, parent, Vector3?, Quaternion? Vector3?, bool) instead.")]
	public NetworkObject GetPooledInstantiated(int prefabId, ushort collectionId, Transform parent, Vector3? position, Quaternion? rotation, Vector3? scale, bool makeActive, bool asServer)
	{
		return _objectPool.RetrieveObject(prefabId, collectionId, parent, position, rotation, scale, makeActive, asServer);
	}

	public NetworkObject GetPooledInstantiated(int prefabId, ushort collectionId, ObjectPoolRetrieveOption options, Transform parent, Vector3? position, Quaternion? rotation, Vector3? scale, bool asServer)
	{
		return _objectPool.RetrieveObject(prefabId, collectionId, options, parent, position, rotation, scale, asServer);
	}

	public void StorePooledInstantiated(NetworkObject instantiated, bool asServer)
	{
		_objectPool.StoreObject(instantiated, asServer);
	}

	public void StorePooledOrDestroyInstantiated(NetworkObject instantiated, bool asServer)
	{
		if (instantiated.GetDefaultDespawnType() == DespawnType.Destroy)
		{
			UnityEngine.Object.Destroy(instantiated.gameObject);
		}
		else
		{
			_objectPool.StoreObject(instantiated, asServer);
		}
	}

	public void CacheObjects(NetworkObject prefab, int count, bool asServer)
	{
		_objectPool.CacheObjects(prefab, count, asServer);
	}

	private bool SetPrefabInformation(GameObject prefab, out NetworkObject nob, out int prefabId, out ushort collectionId)
	{
		if (!prefab.TryGetComponent<NetworkObject>(out nob))
		{
			prefabId = 0;
			collectionId = 0;
			InternalLogError($"NetworkObject was not found on {prefab}. An instantiated NetworkObject cannot be returned.");
			return false;
		}
		prefabId = nob.PrefabId;
		collectionId = nob.SpawnableCollectionId;
		return true;
	}

	public PrefabObjects GetPrefabObjects<T>(ushort spawnableCollectionId, bool createIfMissing) where T : PrefabObjects
	{
		if (spawnableCollectionId == 0)
		{
			if (createIfMissing)
			{
				InternalLogError("SpawnableCollectionId cannot be 0 when create missing is true.");
				return null;
			}
			return SpawnablePrefabs;
		}
		if (!_runtimeSpawnablePrefabs.TryGetValue(spawnableCollectionId, out var value))
		{
			if (!createIfMissing)
			{
				return null;
			}
			value = ScriptableObject.CreateInstance<T>();
			value.SetCollectionId(spawnableCollectionId);
			_runtimeSpawnablePrefabs[spawnableCollectionId] = value;
		}
		return value;
	}

	public bool RemoveSpawnableCollection(ushort spawnableCollectionId)
	{
		return _runtimeSpawnablePrefabs.Remove(spawnableCollectionId);
	}

	public int GetPrefabIndex(GameObject prefab, bool asServer)
	{
		int objectCount = SpawnablePrefabs.GetObjectCount();
		for (int i = 0; i < objectCount; i++)
		{
			if (SpawnablePrefabs.GetObject(asServer, i).gameObject == prefab)
			{
				return i;
			}
		}
		return -1;
	}

	public NetworkObject GetPrefab(int prefabId, bool asServer)
	{
		return SpawnablePrefabs.GetObject(asServer, prefabId);
	}

	public void RegisterInvokeOnInstance<T>(Action<UnityEngine.Component> handler) where T : UnityEngine.Component
	{
		if (!TryGetInstance<T>(out var result))
		{
			string instanceName = GetInstanceName<T>();
			if (!_pendingInvokes.TryGetValue(instanceName, out var value))
			{
				value = new List<Action<UnityEngine.Component>>();
				_pendingInvokes[instanceName] = value;
			}
			value.Add(handler);
		}
		else
		{
			handler(result);
		}
	}

	public void UnregisterInvokeOnInstance<T>(Action<UnityEngine.Component> handler) where T : UnityEngine.Component
	{
		string instanceName = GetInstanceName<T>();
		if (_pendingInvokes.TryGetValue(instanceName, out var value))
		{
			value.Remove(handler);
		}
	}

	public bool HasInstance<T>() where T : UnityEngine.Component
	{
		T result;
		return TryGetInstance<T>(out result);
	}

	public T GetInstance<T>() where T : UnityEngine.Component
	{
		if (TryGetInstance<T>(out var result))
		{
			return result;
		}
		InternalLogWarning("Component " + GetInstanceName<T>() + " is not registered. To avoid this warning use TryGetInstance(T).");
		return null;
	}

	public bool TryGetInstance<T>(out T result) where T : UnityEngine.Component
	{
		string instanceName = GetInstanceName<T>();
		if (_registeredComponents.TryGetValue(instanceName, out var value))
		{
			result = (T)value;
			return true;
		}
		result = null;
		return false;
	}

	public void RegisterInstance<T>(T component, bool replace = true) where T : UnityEngine.Component
	{
		string instanceName = GetInstanceName<T>();
		if (_registeredComponents.ContainsKey(instanceName) && !replace)
		{
			InternalLogWarning("Component " + instanceName + " is already registered.");
			return;
		}
		_registeredComponents[instanceName] = component;
		RemoveNullPendingDelegates();
		if (_pendingInvokes.TryGetValue(instanceName, out var value))
		{
			for (int i = 0; i < value.Count; i++)
			{
				value[i](component);
			}
			value.Clear();
		}
	}

	public bool TryRegisterInstance<T>(T component) where T : UnityEngine.Component
	{
		string instanceName = GetInstanceName<T>();
		if (_registeredComponents.ContainsKey(instanceName))
		{
			return false;
		}
		RegisterInstance(component, replace: false);
		return true;
	}

	public void UnregisterInstance<T>() where T : UnityEngine.Component
	{
		string instanceName = GetInstanceName<T>();
		_registeredComponents.Remove(instanceName);
	}

	private void RemoveNullPendingDelegates()
	{
		foreach (List<Action<UnityEngine.Component>> value in _pendingInvokes.Values)
		{
			for (int i = 0; i < value.Count; i++)
			{
				if (value[i] == null)
				{
					value.RemoveAt(i);
					i--;
				}
			}
		}
	}

	private string GetInstanceName<T>()
	{
		return typeof(T).FullName;
	}

	private void Awake()
	{
		InitializeLogging();
		if (!ValidateSpawnablePrefabs(print: true))
		{
			return;
		}
		if (StartingRpcLinkIndex == 0)
		{
			StartingRpcLinkIndex = (ushort)(Enums.GetHighestValue<PacketId>() + 1);
		}
		bool num = SpawnablePrefabs != null && SpawnablePrefabs is DefaultPrefabObjects;
		CloneChecker.IsMultiplayerClone(out var _);
		if (num)
		{
			DefaultPrefabObjects defaultPrefabObjects = (DefaultPrefabObjects)SpawnablePrefabs;
			DefaultPrefabObjects defaultPrefabObjects2 = ScriptableObject.CreateInstance<DefaultPrefabObjects>();
			defaultPrefabObjects2.AddObjects(defaultPrefabObjects.Prefabs.ToList(), checkForDuplicates: false, initializeAdded: false);
			defaultPrefabObjects2.Sort();
			SpawnablePrefabs = defaultPrefabObjects2;
		}
		_canPersist = CanInitialize();
		if (_canPersist)
		{
			if (TryGetComponent<NetworkObject>(out var _))
			{
				InternalLogError("NetworkObject component found on the NetworkManager object " + base.gameObject.name + ". This is not allowed and will cause problems. Remove the NetworkObject component from this object.");
			}
			SpawnablePrefabs.InitializePrefabRange(0);
			SpawnablePrefabs.SetCollectionId(0);
			SetDontDestroyOnLoad();
			SetRunInBackground();
			DebugManager = GetOrCreateComponent<DebugManager>();
			TransportManager = GetOrCreateComponent<TransportManager>();
			ServerManager = GetOrCreateComponent<ServerManager>();
			ClientManager = GetOrCreateComponent<ClientManager>();
			TimeManager = GetOrCreateComponent<TimeManager>();
			SceneManager = GetOrCreateComponent<SceneManager>();
			ObserverManager = GetOrCreateComponent<ObserverManager>();
			RollbackManager = GetOrCreateComponent<RollbackManager>();
			PredictionManager = GetOrCreateComponent<PredictionManager>();
			StatisticsManager = GetOrCreateComponent<StatisticsManager>();
			if (_objectPool == null)
			{
				_objectPool = GetOrCreateComponent<DefaultObjectPool>();
			}
			InitializeComponents();
			_instances.Add(this);
			Initialized = true;
		}
	}

	private void Start()
	{
		ServerManager.StartForHeadless();
	}

	private void OnDestroy()
	{
		_instances.Remove(this);
	}

	private void InitializeComponents()
	{
		TimeManager.InitializeOnce_Internal(this);
		TimeManager.OnLateUpdate += TimeManager_OnLateUpdate;
		TransportManager.InitializeOnce_Internal(this);
		ClientManager.InitializeOnce_Internal(this);
		ServerManager.InitializeOnce_Internal(this);
		SceneManager.InitializeOnce_Internal(this);
		ObserverManager.InitializeOnce_Internal(this);
		RollbackManager.InitializeOnce_Internal(this);
		PredictionManager.InitializeOnce(this);
		StatisticsManager.InitializeOnce_Internal(this);
		_objectPool.InitializeOnce(this);
	}

	internal void UpdateFramerate()
	{
		bool started = ClientManager.Started;
		bool started2 = ServerManager.Started;
		int num = 0;
		if (started && started2)
		{
			num = Math.Max(ServerManager.FrameRate, ClientManager.FrameRate);
		}
		else if (started)
		{
			num = ClientManager.FrameRate;
		}
		else if (started2)
		{
			num = ServerManager.FrameRate;
		}
		if (num > 0)
		{
			Application.targetFrameRate = num;
		}
	}

	private void TimeManager_OnLateUpdate()
	{
		SetRunInBackground();
		_objectPool.LateUpdate();
	}

	private bool CanInitialize()
	{
		if (_persistence == PersistenceType.AllowMultiple)
		{
			return true;
		}
		List<NetworkManager> list = Instances.ToList();
		if (list.Count == 0)
		{
			return true;
		}
		NetworkManager networkManager = list[0];
		if (_persistence == PersistenceType.DestroyNewest)
		{
			InternalLog($"NetworkManager on object {base.gameObject.name} is being destroyed due to persistence type {_persistence}. A NetworkManager instance already exist on {networkManager.name}.");
			UnityEngine.Object.DestroyImmediate(base.gameObject);
			return false;
		}
		if (_persistence == PersistenceType.DestroyOldest)
		{
			InternalLog($"NetworkManager on object {networkManager.name} is being destroyed due to persistence type {_persistence}. A NetworkManager instance has been created on {base.gameObject.name}.");
			UnityEngine.Object.DestroyImmediate(networkManager.gameObject);
			return true;
		}
		InternalLog($"Persistance type of {_persistence} is unhandled on {base.gameObject.name}. Initialization will not proceed.");
		return false;
	}

	private bool ValidateSpawnablePrefabs(bool print)
	{
		if (SpawnablePrefabs == null && !string.IsNullOrEmpty(base.gameObject.scene.name))
		{
			if (print)
			{
				Debug.LogError("SpawnablePrefabs is null on " + base.gameObject.name + ". Select the NetworkManager in scene " + base.gameObject.scene.name + " and choose a prefabs file. Choosing DefaultPrefabObjects will automatically populate prefabs for you.");
			}
			return false;
		}
		return true;
	}

	private void SetDontDestroyOnLoad()
	{
		if (_dontDestroyOnLoad)
		{
			UnityEngine.Object.DontDestroyOnLoad(this);
		}
	}

	private void SetRunInBackground()
	{
		Application.runInBackground = _runInBackground;
	}

	private T GetOrCreateComponent<T>(T presetValue = null) where T : UnityEngine.Component
	{
		if (presetValue != null)
		{
			return presetValue;
		}
		if (base.gameObject.TryGetComponent<T>(out var component))
		{
			return component;
		}
		return base.gameObject.AddComponent<T>();
	}

	internal void ClearClientsCollection(Dictionary<int, NetworkConnection> clients, int transportIndex = -1)
	{
		bool flag = transportIndex < 0;
		List<int> list = CollectionCaches<int>.RetrieveList();
		foreach (KeyValuePair<int, NetworkConnection> client in clients)
		{
			NetworkConnection value = client.Value;
			if (!flag)
			{
				if (value.TransportIndex == transportIndex)
				{
					list.Add(client.Key);
					value.ResetState();
				}
			}
			else
			{
				value.ResetState();
			}
		}
		if (flag)
		{
			clients.Clear();
		}
		else
		{
			foreach (int item in list)
			{
				clients.Remove(item);
			}
		}
		CollectionCaches<int>.Store(list);
	}
}
