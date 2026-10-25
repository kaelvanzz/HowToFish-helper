using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet.Connection;
using FishNet.Managing.Client;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using GameKit.Dependencies.Utilities.Types;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace FishNet.Managing.Scened;

[DisallowMultipleComponent]
[AddComponentMenu("FishNet/Manager/SceneManager")]
public sealed class SceneManager : MonoBehaviour
{
	internal enum LightProbeUpdateType
	{
		Asynchronous,
		BlockThread,
		Off
	}

	[Tooltip("Script to handle addressables loading and unloading. This field may be blank if addressables are not being used.")]
	[SerializeField]
	private SceneProcessorBase _sceneProcessor;

	[Tooltip("How to update light probes after loading or unloading scenes.")]
	[SerializeField]
	private LightProbeUpdateType _lightProbeUpdating;

	[FormerlySerializedAs("_moveClientHostObjects")]
	[Tooltip("True to move spawned objects visible to the client that are within an unloading scene. This ensures the objects are despawned on the client side rather than when the scene is destroyed.")]
	[SerializeField]
	private bool _moveClientObjects = true;

	[Tooltip("True to automatically set active scenes when loading and unloading scenes.")]
	[SerializeField]
	private bool _setActiveScene = true;

	private string[] _globalScenes = new string[0];

	private SceneLoadData _globalSceneLoadData = new SceneLoadData();

	private List<object> _queuedOperations = new List<object>();

	private HashSet<Scene> _manualUnloadScenes = new HashSet<Scene>();

	private Scene _movedObjectsScene;

	private Scene _delayedDestroyScene;

	private Scene _fallbackActiveScene;

	private bool _sceneQueueStartInvoked;

	private List<GameObject> _movingObjects = new List<GameObject>();

	private Dictionary<NetworkConnection, int> _pendingClientSceneChanges = new Dictionary<NetworkConnection, int>();

	private HashSet<string> _serverGlobalScenesLoading = new HashSet<string>();

	private const string INVALID_SCENELOADDATA = "One or more datas in SceneLoadData are invalid.This generally occurs when calling this method without specifying any scenes or when data fields are null.";

	private const string INVALID_SCENEUNLOADDATA = "One or more datas in SceneLoadData are invalid.This generally occurs when calling this method without specifying any scenes or when data fields are null.";

	public Dictionary<Scene, HashSet<NetworkConnection>> SceneConnections { get; private set; } = new Dictionary<Scene, HashSet<NetworkConnection>>();


	public NetworkManager NetworkManager { get; private set; }

	internal bool IteratingQueue { get; private set; }

	internal float QueueCompleteTime { get; private set; }

	private ServerManager _serverManager => NetworkManager.ServerManager;

	private ClientManager _clientManager => NetworkManager.ClientManager;

	public event Action<bool> OnActiveSceneSet;

	public event Action<NetworkConnection, bool> OnClientLoadedStartScenes;

	public event Action OnQueueStart;

	public event Action OnQueueEnd;

	public event Action<SceneLoadStartEventArgs> OnLoadStart;

	public event Action<SceneLoadPercentEventArgs> OnLoadPercentChange;

	public event Action<SceneLoadEndEventArgs> OnLoadEnd;

	public event Action<SceneUnloadStartEventArgs> OnUnloadStart;

	public event Action<SceneUnloadEndEventArgs> OnUnloadEnd;

	public event Action<ClientPresenceChangeEventArgs> OnClientPresenceChangeStart;

	public event Action<ClientPresenceChangeEventArgs> OnClientPresenceChangeEnd;

	internal event Action<bool> OnActiveSceneSetInternal;

	public SceneProcessorBase GetSceneProcessor()
	{
		return _sceneProcessor;
	}

	public void SetSceneProcessor(SceneProcessorBase value)
	{
		_sceneProcessor = value;
	}

	public void SetMoveClientObjects(bool value)
	{
		_moveClientObjects = value;
	}

	private void Awake()
	{
		UnityEngine.SceneManagement.SceneManager.sceneUnloaded += SceneManager_SceneUnloaded;
		if (_sceneProcessor == null)
		{
			_sceneProcessor = base.gameObject.AddComponent<DefaultSceneProcessor>();
		}
		_sceneProcessor.Initialize(this);
	}

	private void OnDestroy()
	{
		UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= SceneManager_SceneUnloaded;
	}

	private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
	{
		if (!NetworkManager.ServerManager.IsAnyServerStarted())
		{
			ResetValues();
		}
	}

	private void ResetValues()
	{
		SceneConnections.Clear();
		_globalScenes = new string[0];
		_globalSceneLoadData = new SceneLoadData();
		_queuedOperations.Clear();
		_manualUnloadScenes.Clear();
		_sceneQueueStartInvoked = false;
		_movingObjects.Clear();
	}

	private void ServerManager_OnRemoteConnectionState(NetworkConnection arg1, RemoteConnectionStateArgs arg2)
	{
		if (arg2.ConnectionState == RemoteConnectionState.Stopped)
		{
			ClientDisconnected(arg1);
		}
	}

	internal void InitializeOnce_Internal(NetworkManager manager)
	{
		NetworkManager = manager;
		NetworkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
		NetworkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
		_clientManager.RegisterBroadcast<LoadScenesBroadcast>(OnServerLoadedScenes);
		_clientManager.RegisterBroadcast<UnloadScenesBroadcast>(OnServerUnloadedScenes);
		_serverManager.RegisterBroadcast<ClientScenesLoadedBroadcast>(OnClientLoadedScenes);
		_serverManager.RegisterBroadcast<EmptyStartScenesBroadcast>(OnClientSentEmptyStartScenes);
		_clientManager.RegisterBroadcast<EmptyStartScenesBroadcast>(OnServerSentEmptyStartScenes);
	}

	private void SceneManager_SceneUnloaded(Scene scene)
	{
		if (NetworkManager.IsServerStarted)
		{
			SceneConnections.Remove(scene);
			_manualUnloadScenes.Remove(scene);
			RemoveFromGlobalScenes(scene);
		}
	}

	private void TryInvokeLoadedStartScenes(NetworkConnection connection, bool asServer)
	{
		if (connection.SetLoadedStartScenes(asServer))
		{
			this.OnClientLoadedStartScenes?.Invoke(connection, asServer);
		}
	}

	internal void OnClientAuthenticated(NetworkConnection connection)
	{
		AddPendingLoad(connection);
		if (_globalScenes.Length == 0)
		{
			connection.Broadcast(default(EmptyStartScenesBroadcast));
			return;
		}
		string[] array = GlobalScenesExcludingLoading();
		if (array != null)
		{
			SceneLoadData sceneLoadData = new SceneLoadData(array);
			sceneLoadData.Params = _globalSceneLoadData.Params;
			sceneLoadData.Options = _globalSceneLoadData.Options;
			sceneLoadData.ReplaceScenes = _globalSceneLoadData.ReplaceScenes;
			sceneLoadData.PreferredActiveScene = _globalSceneLoadData.PreferredActiveScene;
			LoadQueueData queueData = new LoadQueueData(SceneScopeType.Global, Array.Empty<NetworkConnection>(), sceneLoadData, _globalScenes, asServer: false);
			LoadScenesBroadcast loadScenesBroadcast = default(LoadScenesBroadcast);
			loadScenesBroadcast.QueueData = queueData;
			LoadScenesBroadcast message = loadScenesBroadcast;
			connection.Broadcast(message);
		}
	}

	private void OnServerSentEmptyStartScenes(EmptyStartScenesBroadcast msg, Channel channel)
	{
		TryInvokeLoadedStartScenes(_clientManager.Connection, asServer: false);
		_clientManager.Broadcast(msg);
	}

	private void OnClientSentEmptyStartScenes(NetworkConnection conn, EmptyStartScenesBroadcast msg, Channel channel)
	{
		if (conn.IsActive)
		{
			if (conn.LoadedStartScenes(asServer: true))
			{
				conn.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Received multiple EmptyStartSceneBroadcast from connectionId {conn.ClientId}. Connection will be kicked immediately.");
			}
			else
			{
				OnClientLoadedScenes(conn, default(ClientScenesLoadedBroadcast), channel);
			}
		}
	}

	private void ClientDisconnected(NetworkConnection conn)
	{
		_pendingClientSceneChanges.Remove(conn);
		List<Scene> list = new List<Scene>();
		Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
		foreach (KeyValuePair<Scene, HashSet<NetworkConnection>> sceneConnection in SceneConnections)
		{
			Scene key = sceneConnection.Key;
			HashSet<NetworkConnection> value = sceneConnection.Value;
			if (value.Remove(conn) && value.Count == 0 && !IsGlobalScene(key) && !_manualUnloadScenes.Contains(key) && key != activeScene)
			{
				list.Add(key);
			}
		}
		if (list.Count <= 0)
		{
			return;
		}
		foreach (Scene item in list)
		{
			SceneConnections.Remove(item);
		}
		SceneUnloadData sceneUnloadData = new SceneUnloadData(SceneLookupData.CreateData(list));
		UnloadConnectionScenes(Array.Empty<NetworkConnection>(), sceneUnloadData);
	}

	private void OnClientLoadedScenes(NetworkConnection conn, ClientScenesLoadedBroadcast msg, Channel channel)
	{
		if (!conn.IsActive)
		{
			return;
		}
		_pendingClientSceneChanges.TryGetValueIL2CPP(conn, out var value);
		if (value == 0)
		{
			conn.Kick(KickReason.ExploitAttempt, LoggingType.Common, $"Received excessive ClientScenesLoadedBroadcast from connectionId {conn.ClientId}. Connection will be kicked immediately.");
			return;
		}
		value--;
		if (value == 0)
		{
			_pendingClientSceneChanges.Remove(conn);
		}
		else
		{
			_pendingClientSceneChanges[conn] = value;
		}
		if (!Comparers.IsDefault(msg))
		{
			SceneLookupData[] sceneLookupDatas = msg.SceneLookupDatas;
			for (int i = 0; i < sceneLookupDatas.Length; i++)
			{
				bool foundByHandle;
				Scene scene = sceneLookupDatas[i].GetScene(out foundByHandle);
				if (scene.IsValid())
				{
					AddConnectionToScene(conn, scene);
				}
			}
		}
		TryInvokeLoadedStartScenes(conn, asServer: true);
	}

	private void TryInvokeOnQueueStart()
	{
		if (!_sceneQueueStartInvoked)
		{
			_sceneQueueStartInvoked = true;
			IteratingQueue = true;
			this.OnQueueStart?.Invoke();
		}
	}

	private void TryInvokeOnQueueEnd()
	{
		if (_sceneQueueStartInvoked)
		{
			_sceneQueueStartInvoked = false;
			IteratingQueue = false;
			QueueCompleteTime = Time.unscaledTime;
			this.OnQueueEnd?.Invoke();
		}
	}

	private void InvokeOnSceneLoadStart(LoadQueueData qd)
	{
		TryInvokeOnQueueStart();
		this.OnLoadStart?.Invoke(new SceneLoadStartEventArgs(qd));
	}

	private void InvokeOnSceneLoadEnd(LoadQueueData qd, List<string> requestedLoadScenes, List<Scene> loadedScenes, string[] unloadedSceneNames)
	{
		List<string> list = requestedLoadScenes.ToList();
		for (int i = 0; i < loadedScenes.Count; i++)
		{
			list.Remove(loadedScenes[i].name);
		}
		SceneLoadEndEventArgs obj = new SceneLoadEndEventArgs(qd, list.ToArray(), loadedScenes.ToArray(), unloadedSceneNames);
		this.OnLoadEnd?.Invoke(obj);
	}

	private void InvokeOnSceneUnloadStart(UnloadQueueData sqd)
	{
		TryInvokeOnQueueStart();
		this.OnUnloadStart?.Invoke(new SceneUnloadStartEventArgs(sqd));
	}

	private void InvokeOnSceneUnloadEnd(UnloadQueueData sqd, List<Scene> unloadedScenes, List<UnloadedScene> newUnloadedScenes)
	{
		SceneUnloadEndEventArgs obj = new SceneUnloadEndEventArgs(sqd, unloadedScenes, newUnloadedScenes);
		this.OnUnloadEnd?.Invoke(obj);
	}

	private void InvokeOnScenePercentChange(LoadQueueData qd, float value)
	{
		value = Mathf.Clamp(value, 0f, 1f);
		SceneLoadPercentEventArgs obj = new SceneLoadPercentEventArgs(qd, value);
		this.OnLoadPercentChange?.Invoke(obj);
	}

	private void QueueOperation(object data)
	{
		_queuedOperations.Add(data);
		if (_queuedOperations.Count == 1 && base.gameObject.activeInHierarchy)
		{
			StartCoroutine(__ProcessSceneQueue());
		}
	}

	private IEnumerator __ProcessSceneQueue()
	{
		while (_queuedOperations.Count > 0)
		{
			if (_queuedOperations[0] is LoadQueueData)
			{
				yield return StartCoroutine(__LoadScenes());
			}
			else if (_queuedOperations[0] is UnloadQueueData)
			{
				yield return StartCoroutine(__UnloadScenes());
			}
			if (_queuedOperations.Count > 0)
			{
				_queuedOperations.RemoveAt(0);
			}
		}
		TryInvokeOnQueueEnd();
	}

	private string[] GlobalScenesExcludingLoading()
	{
		HashSet<string> hashSet = null;
		string[] globalScenes = _globalScenes;
		foreach (string item in globalScenes)
		{
			if (_serverGlobalScenesLoading.Contains(item))
			{
				if (hashSet == null)
				{
					hashSet = new HashSet<string>();
				}
				hashSet.Add(item);
			}
		}
		if (hashSet != null)
		{
			if (_globalScenes.Length - hashSet.Count <= 0)
			{
				return null;
			}
			List<string> list = new List<string>();
			globalScenes = _globalScenes;
			foreach (string item2 in globalScenes)
			{
				if (!hashSet.Contains(item2))
				{
					list.Add(item2);
				}
			}
			return list.ToArray();
		}
		return _globalScenes;
	}

	public void LoadGlobalScenes(SceneLoadData sceneLoadData)
	{
		LoadGlobalScenes_Internal(sceneLoadData, _globalScenes, asServer: true);
	}

	private void LoadGlobalScenes_Internal(SceneLoadData sceneLoadData, string[] globalScenes, bool asServer)
	{
		if (CanExecute(asServer, warn: true) && !SceneDataInvalid(sceneLoadData, error: true))
		{
			if (sceneLoadData.Options.AllowStacking)
			{
				NetworkManager.LogError("Stacking scenes is not allowed with Global scenes.");
				return;
			}
			LoadQueueData data = new LoadQueueData(SceneScopeType.Global, Array.Empty<NetworkConnection>(), sceneLoadData, globalScenes, asServer);
			QueueOperation(data);
		}
	}

	public void LoadConnectionScenes(NetworkConnection conn, SceneLoadData sceneLoadData)
	{
		LoadConnectionScenes(new NetworkConnection[1] { conn }, sceneLoadData);
	}

	public void LoadConnectionScenes(NetworkConnection[] conns, SceneLoadData sceneLoadData)
	{
		LoadConnectionScenes_Internal(conns, sceneLoadData, _globalScenes, asServer: true);
	}

	public void LoadConnectionScenes(SceneLoadData sceneLoadData)
	{
		LoadConnectionScenes_Internal(Array.Empty<NetworkConnection>(), sceneLoadData, _globalScenes, asServer: true);
	}

	private void LoadConnectionScenes_Internal(NetworkConnection[] conns, SceneLoadData sceneLoadData, string[] globalScenes, bool asServer)
	{
		if (CanExecute(asServer, warn: true) && !SceneDataInvalid(sceneLoadData, error: true))
		{
			LoadQueueData data = new LoadQueueData(SceneScopeType.Connections, conns, sceneLoadData, globalScenes, asServer);
			QueueOperation(data);
		}
	}

	private bool CanMoveNetworkObject(NetworkObject nob, bool warn)
	{
		if (nob == null)
		{
			return WarnAndReturnFalse("NetworkObject is null.");
		}
		if (!nob.GetIsNetworked())
		{
			return WarnAndReturnFalse("NetworkObject " + nob.name + " cannot be moved as it is not networked.");
		}
		if (!nob.IsSpawned)
		{
			return WarnAndReturnFalse("NetworkObject " + nob.name + " canot be moved as it is not spawned.");
		}
		if (nob.IsSceneObject)
		{
			return WarnAndReturnFalse("NetworkObject " + nob.name + " cannot be moved as it is a scene object.");
		}
		if (nob.transform.parent != null)
		{
			return WarnAndReturnFalse("NetworkObject " + nob.name + " cannot be moved because it is not the root object. Unity can only move root objects between scenes.");
		}
		if (nob.IsGlobal && nob.gameObject.scene.name == DDOL.GetDDOL().gameObject.scene.name)
		{
			return WarnAndReturnFalse("NetworkObject " + nob.name + " cannot be moved because it is global. Global objects must remain in the DontDestroyOnLoad scene.");
		}
		return true;
		bool WarnAndReturnFalse(string msg)
		{
			if (warn)
			{
				NetworkManager.LogWarning(msg);
			}
			return false;
		}
	}

	private IEnumerator __LoadScenes()
	{
		try
		{
			LoadQueueData data = _queuedOperations[0] as LoadQueueData;
			SceneLoadData sceneLoadData = data.SceneLoadData;
			bool asServer = data.AsServer;
			bool asHost = !asServer && NetworkManager.IsServerStarted;
			if (!ConnectionActive(asServer))
			{
				yield break;
			}
			if (sceneLoadData.SceneLookupDatas.Length == 0)
			{
				NetworkManager.LogWarning("No scenes specified to load.");
				yield break;
			}
			ReplaceOption replaceScenes = sceneLoadData.ReplaceScenes;
			NetworkConnection localConnection = NetworkManager.ClientManager.Connection;
			if (!asServer)
			{
				if (!asHost)
				{
					_globalScenes = data.GlobalScenes;
				}
			}
			else if (asServer && data.ScopeType == SceneScopeType.Global)
			{
				_globalSceneLoadData = sceneLoadData;
				string[] names = sceneLoadData.SceneLookupDatas.GetNames();
				string[] array = names;
				foreach (string item in array)
				{
					_serverGlobalScenesLoading.Add(item);
				}
				if (replaceScenes != ReplaceOption.None)
				{
					_globalScenes = names;
				}
				else
				{
					int destinationIndex = _globalScenes.Length;
					Array.Resize(ref _globalScenes, _globalScenes.Length + names.Length);
					Array.Copy(names, 0, _globalScenes, destinationIndex, names.Length);
				}
				CheckForDuplicateGlobalSceneNames();
				data.GlobalScenes = _globalScenes;
			}
			List<string> requestedLoadSceneNames = new List<string>();
			List<int> list = new List<int>();
			SceneLookupData[] broadcastLookupDatas = new SceneLookupData[sceneLoadData.SceneLookupDatas.Length];
			List<SceneLookupData> loadableScenes = new List<SceneLookupData>();
			for (int l = 0; l < sceneLoadData.SceneLookupDatas.Length; l++)
			{
				SceneLookupData sceneLookupData = sceneLoadData.SceneLookupDatas[l];
				bool foundByHandle;
				Scene scene2 = sceneLookupData.GetScene(out foundByHandle);
				if (scene2.IsValid())
				{
					requestedLoadSceneNames.Add(scene2.name);
					if (foundByHandle)
					{
						list.Add(scene2.handle);
					}
				}
				if (CanLoadScene(data, sceneLookupData))
				{
					if (!asHost)
					{
						loadableScenes.Add(sceneLookupData);
					}
				}
				else if (asServer)
				{
					broadcastLookupDatas[l] = new SceneLookupData(scene2);
				}
			}
			if (!asHost)
			{
				Scene movedObjectsScene = GetMovedObjectsScene();
				NetworkObject[] movedNetworkObjects = sceneLoadData.MovedNetworkObjects;
				foreach (NetworkObject networkObject in movedNetworkObjects)
				{
					if (networkObject != null && CanMoveNetworkObject(networkObject, warn: true))
					{
						UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(networkObject.gameObject, movedObjectsScene);
					}
				}
			}
			List<int> list2 = new List<int>();
			if (replaceScenes != ReplaceOption.None)
			{
				if (asServer)
				{
					Scene[] array2 = SceneConnections.Keys.ToArray();
					for (int m = 0; m < array2.Length; m++)
					{
						list2.Add(array2[m].handle);
					}
					if (data.ScopeType == SceneScopeType.Global)
					{
						Scene[] array3 = array2;
						foreach (Scene scene3 in array3)
						{
							RemoveAllConnectionsFromScene(scene3);
						}
					}
					else if (data.ScopeType == SceneScopeType.Connections)
					{
						RemoveConnectionsFromNonGlobalScenes(data.Connections);
					}
				}
				else
				{
					foreach (Scene scene7 in NetworkManager.ClientManager.Connection.Scenes)
					{
						list2.Add(scene7.handle);
					}
				}
			}
			List<Scene> unloadableScenes = new List<Scene>();
			if (replaceScenes != ReplaceOption.None && !asHost)
			{
				for (int n = 0; n < UnityEngine.SceneManagement.SceneManager.sceneCount; n++)
				{
					Scene sceneAt = UnityEngine.SceneManagement.SceneManager.GetSceneAt(n);
					if (sceneAt == GetMovedObjectsScene() || requestedLoadSceneNames.Contains(sceneAt.name) || list.Contains(sceneAt.handle) || IsGlobalScene(sceneAt) || _manualUnloadScenes.Contains(sceneAt))
					{
						continue;
					}
					bool num = list2.Contains(sceneAt.handle);
					bool flag = SceneConnections.ContainsKey(sceneAt);
					if (!num || flag)
					{
						if (SceneConnections.TryGetValueIL2CPP(sceneAt, out var value))
						{
							if (value != null && value.Count > 0)
							{
								continue;
							}
						}
						else if (replaceScenes != 0)
						{
							continue;
						}
					}
					unloadableScenes.Add(sceneAt);
				}
			}
			InvokeOnSceneLoadStart(data);
			if (unloadableScenes.Count > 0 || loadableScenes.Count > 0)
			{
				_sceneProcessor.LoadStart(data);
			}
			string[] unloadedNames = new string[unloadableScenes.Count];
			for (int num2 = 0; num2 < unloadableScenes.Count; num2++)
			{
				unloadedNames[num2] = unloadableScenes[num2].name;
			}
			if (!data.AsServer && !asHost && replaceScenes != ReplaceOption.None && _moveClientObjects)
			{
				Scene movedObjectsScene2 = GetMovedObjectsScene();
				foreach (NetworkObject value2 in NetworkManager.ClientManager.Objects.Spawned.Values)
				{
					if (CanMoveNetworkObject(value2, warn: false))
					{
						UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(value2.gameObject, movedObjectsScene2);
					}
				}
			}
			_sceneProcessor.UnloadStart(data);
			for (int j = 0; j < unloadableScenes.Count; j++)
			{
				MoveClientHostObjects(unloadableScenes[j], asServer);
				_sceneProcessor.BeginUnloadAsync(unloadableScenes[j]);
				while (!_sceneProcessor.IsPercentComplete())
				{
					yield return null;
				}
			}
			_sceneProcessor.UnloadEnd(data);
			List<Scene> loadedScenes = new List<Scene>();
			for (int j = 0; j < loadableScenes.Count; j++)
			{
				LoadSceneParameters loadSceneParameters = default(LoadSceneParameters);
				loadSceneParameters.loadSceneMode = LoadSceneMode.Additive;
				loadSceneParameters.localPhysicsMode = sceneLoadData.Options.LocalPhysics;
				LoadSceneParameters parameters = loadSceneParameters;
				float maximumIndexWorth = 1f / (float)loadableScenes.Count;
				_sceneProcessor.BeginLoadAsync(loadableScenes[j].Name, parameters);
				while (!_sceneProcessor.IsPercentComplete())
				{
					float percentComplete = _sceneProcessor.GetPercentComplete();
					InvokePercentageChange(j, maximumIndexWorth, percentComplete);
					yield return null;
				}
				Scene scene4 = _sceneProcessor.GetLastLoadedScene();
				if (scene4 == default(Scene))
				{
					scene4 = UnityEngine.SceneManagement.SceneManager.GetSceneAt(UnityEngine.SceneManagement.SceneManager.sceneCount - 1);
				}
				loadedScenes.Add(scene4);
				_sceneProcessor.AddLoadedScene(scene4);
			}
			InvokeOnScenePercentChange(data, 1f);
			if (data.AsServer && !sceneLoadData.Options.AutomaticallyUnload)
			{
				foreach (Scene item2 in loadedScenes)
				{
					_manualUnloadScenes.Add(item2);
				}
			}
			if (!asHost)
			{
				Scene scene5 = default(Scene);
				if (sceneLoadData.Options.AllowStacking)
				{
					Scene firstLookupScene = sceneLoadData.GetFirstLookupScene();
					if (sceneLoadData.SceneLookupDatas[0].Handle != 0 && !string.IsNullOrEmpty(firstLookupScene.name))
					{
						scene5 = firstLookupScene;
					}
					else
					{
						Scene scene6 = default(Scene);
						for (int num3 = 0; num3 < UnityEngine.SceneManagement.SceneManager.sceneCount; num3++)
						{
							Scene sceneAt2 = UnityEngine.SceneManagement.SceneManager.GetSceneAt(num3);
							if (sceneAt2.name == firstLookupScene.name)
							{
								scene6 = sceneAt2;
							}
						}
						if (string.IsNullOrEmpty(scene6.name))
						{
							NetworkManager.LogError("Scene " + sceneLoadData.SceneLookupDatas[0].Name + " could not be found in loaded scenes.");
						}
						else
						{
							scene5 = scene6;
						}
					}
				}
				else
				{
					scene5 = sceneLoadData.GetFirstLookupScene();
					if (string.IsNullOrEmpty(scene5.name))
					{
						scene5 = GetFirstLoadedScene();
					}
				}
				if (string.IsNullOrEmpty(scene5.name))
				{
					NetworkManager.LogError("Unable to move objects to a new scene because new scene lookup has failed.");
				}
				else
				{
					GetMovedObjectsScene().GetRootGameObjects(_movingObjects);
					foreach (GameObject movingObject in _movingObjects)
					{
						UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(movingObject, scene5);
					}
				}
			}
			_sceneProcessor.ActivateLoadedScenes();
			yield return _sceneProcessor.AsyncsIsDone();
			_sceneProcessor.LoadEnd(data);
			bool allScenesLoaded;
			do
			{
				allScenesLoaded = true;
				foreach (Scene item3 in loadedScenes)
				{
					if (!item3.isLoaded)
					{
						allScenesLoaded = false;
						break;
					}
				}
				yield return null;
			}
			while (!allScenesLoaded);
			SetActiveScene_Local();
			if (asServer)
			{
				foreach (Scene item4 in loadedScenes)
				{
					SetInFirstNullIndex(item4);
				}
			}
			if (data.AsServer && NetworkManager.IsServerStarted)
			{
				LoadScenesBroadcast loadScenesBroadcast = default(LoadScenesBroadcast);
				loadScenesBroadcast.QueueData = data;
				LoadScenesBroadcast message = loadScenesBroadcast;
				message.QueueData.SceneLoadData.SceneLookupDatas = broadcastLookupDatas;
				if (data.ScopeType == SceneScopeType.Global)
				{
					NetworkConnection[] array4 = _serverManager.Clients.Values.ToArray();
					AddPendingLoad(array4, array4.Length);
					_serverManager.Broadcast(message);
				}
				else if (data.ScopeType == SceneScopeType.Connections)
				{
					AddPendingLoad(data.Connections, data.Connections.Length);
					for (int num4 = 0; num4 < data.Connections.Length; num4++)
					{
						NetworkConnection networkConnection = data.Connections[num4];
						if (networkConnection.IsValid() && networkConnection.IsAuthenticated)
						{
							data.Connections[num4].Broadcast(message);
						}
					}
				}
			}
			else if (!data.AsServer && NetworkManager.IsClientStarted)
			{
				foreach (Scene item5 in unloadableScenes)
				{
					if (item5.IsValid())
					{
						localConnection.RemoveFromScene(item5);
					}
				}
				foreach (Scene item6 in loadedScenes)
				{
					localConnection.AddToScene(item6);
				}
				TryInvokeLoadedStartScenes(_clientManager.Connection, asServer: false);
				ClientScenesLoadedBroadcast clientScenesLoadedBroadcast = default(ClientScenesLoadedBroadcast);
				clientScenesLoadedBroadcast.SceneLookupDatas = sceneLoadData.SceneLookupDatas;
				ClientScenesLoadedBroadcast message2 = clientScenesLoadedBroadcast;
				_clientManager.Broadcast(message2);
			}
			InvokeOnSceneLoadEnd(data, requestedLoadSceneNames, loadedScenes, unloadedNames);
			Scene GetFirstLoadedScene()
			{
				if (loadedScenes.Count > 0)
				{
					return loadedScenes[0];
				}
				return default(Scene);
			}
			void InvokePercentageChange(int index, float maximumWorth, float currentScenePercent)
			{
				float num6 = (float)index * maximumWorth;
				num6 += Mathf.Lerp(0f, maximumWorth, currentScenePercent);
				InvokeOnScenePercentChange(data, num6);
			}
			void SetActiveScene_Local()
			{
				bool byUser;
				Scene preferredScene = GetUserPreferredActiveScene(sceneLoadData.PreferredActiveScene, asServer, out byUser);
				if (!preferredScene.IsValid())
				{
					bool flag2 = false;
					if (sceneLoadData.ReplaceScenes != ReplaceOption.None)
					{
						flag2 |= data.ScopeType == SceneScopeType.Connections && !NetworkManager.IsServerStarted;
						Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
						flag2 |= activeScene == GetMovedObjectsScene();
					}
					if (flag2)
					{
						preferredScene = sceneLoadData.GetFirstLookupScene();
					}
				}
				SetActiveScene(preferredScene, asServer, byUser);
			}
			void SetInFirstNullIndex(Scene scene)
			{
				for (int num5 = 0; num5 < broadcastLookupDatas.Length; num5++)
				{
					if (broadcastLookupDatas[num5] == null)
					{
						broadcastLookupDatas[num5] = new SceneLookupData(scene);
						return;
					}
				}
				NetworkManager.LogError("Cannot add scene to broadcastLookupDatas, collection is full.");
			}
		}
		finally
		{
			_serverGlobalScenesLoading.Clear();
		}
	}

	private void OnServerLoadedScenes(LoadScenesBroadcast msg, Channel channel)
	{
		if (msg.QueueData == null)
		{
			TryInvokeLoadedStartScenes(_clientManager.Connection, asServer: false);
			return;
		}
		LoadQueueData queueData = msg.QueueData;
		if (queueData.ScopeType == SceneScopeType.Global)
		{
			LoadGlobalScenes_Internal(queueData.SceneLoadData, queueData.GlobalScenes, asServer: false);
		}
		else
		{
			LoadConnectionScenes_Internal(Array.Empty<NetworkConnection>(), queueData.SceneLoadData, queueData.GlobalScenes, asServer: false);
		}
	}

	public void UnloadGlobalScenes(SceneUnloadData sceneUnloadData)
	{
		if (CanExecute(asServer: true, warn: true))
		{
			UnloadGlobalScenes_Internal(sceneUnloadData, _globalScenes, asServer: true);
		}
	}

	private void UnloadGlobalScenes_Internal(SceneUnloadData sceneUnloadData, string[] globalScenes, bool asServer)
	{
		UnloadQueueData data = new UnloadQueueData(SceneScopeType.Global, Array.Empty<NetworkConnection>(), sceneUnloadData, globalScenes, asServer);
		QueueOperation(data);
	}

	public void UnloadConnectionScenes(NetworkConnection connection, SceneUnloadData sceneUnloadData)
	{
		UnloadConnectionScenes(new NetworkConnection[1] { connection }, sceneUnloadData);
	}

	public void UnloadConnectionScenes(NetworkConnection[] connections, SceneUnloadData sceneUnloadData)
	{
		UnloadConnectionScenes_Internal(connections, sceneUnloadData, _globalScenes, asServer: true);
	}

	public void UnloadConnectionScenes(SceneUnloadData sceneUnloadData)
	{
		UnloadConnectionScenes_Internal(Array.Empty<NetworkConnection>(), sceneUnloadData, _globalScenes, asServer: true);
	}

	private void UnloadConnectionScenes_Internal(NetworkConnection[] connections, SceneUnloadData sceneUnloadData, string[] globalScenes, bool asServer)
	{
		if (CanExecute(asServer, warn: true) && !SceneDataInvalid(sceneUnloadData, error: true))
		{
			UnloadQueueData data = new UnloadQueueData(SceneScopeType.Connections, connections, sceneUnloadData, globalScenes, asServer);
			QueueOperation(data);
		}
	}

	private IEnumerator __UnloadScenes()
	{
		UnloadQueueData data = _queuedOperations[0] as UnloadQueueData;
		SceneUnloadData sceneUnloadData = data.SceneUnloadData;
		if (!ConnectionActive(data.AsServer))
		{
			yield break;
		}
		bool flag = !data.AsServer && NetworkManager.IsServerStarted;
		bool asServer = data.AsServer;
		Scene[] scenes = GetScenes(sceneUnloadData.SceneLookupDatas);
		if (scenes.Length == 0 && !flag)
		{
			NetworkManager.LogWarning($"Scene lookup data of length {sceneUnloadData.SceneLookupDatas.Length} could not find any scenes to unload. This may occur when trying to unload a scene only by handle. Consider using the scene reference or handle and name while creating SceneLookupData.");
			yield break;
		}
		if (asServer && data.ScopeType == SceneScopeType.Global)
		{
			RemoveFromGlobalScenes(sceneUnloadData.SceneLookupDatas);
			data.GlobalScenes = _globalScenes;
		}
		if (asServer)
		{
			Scene[] array = scenes;
			foreach (Scene scene in array)
			{
				if (data.ScopeType == SceneScopeType.Global)
				{
					RemoveAllConnectionsFromScene(scene);
				}
				else if (data.ScopeType == SceneScopeType.Connections)
				{
					RemoveConnectionsFromScene(data.Connections, scene);
				}
			}
		}
		List<Scene> unloadableScenes = scenes.ToList();
		List<UnloadedScene> unloadedScenes = new List<UnloadedScene>();
		if ((asServer || flag) && sceneUnloadData.Options.Mode == UnloadOptions.ServerUnloadMode.KeepUnused)
		{
			unloadableScenes.Clear();
		}
		else if (!asServer && !flag)
		{
			sceneUnloadData.Options.Mode = UnloadOptions.ServerUnloadMode.UnloadUnused;
		}
		if (data.ScopeType == SceneScopeType.Connections)
		{
			RemoveGlobalScenes(unloadableScenes);
		}
		if (sceneUnloadData.Options.Mode == UnloadOptions.ServerUnloadMode.UnloadUnused)
		{
			RemoveOccupiedScenes(unloadableScenes);
		}
		if (unloadableScenes.Count > 0)
		{
			InvokeOnSceneUnloadStart(data);
			_sceneProcessor.UnloadStart(data);
			foreach (Scene item in unloadableScenes)
			{
				if (!item.IsValid())
				{
					NetworkManager.LogWarning("A scene was expected to be unloaded but could not due to it's referening going missing. This usually occurs when the same scene has been queued for unloading multiple times.");
					continue;
				}
				unloadedScenes.Add(new UnloadedScene(item));
				MoveClientHostObjects(item, asServer);
				_manualUnloadScenes.Remove(item);
				_sceneProcessor.BeginUnloadAsync(item);
				while (!_sceneProcessor.IsPercentComplete())
				{
					yield return null;
				}
			}
			_sceneProcessor.UnloadEnd(data);
		}
		yield return null;
		bool byUser;
		Scene userPreferredActiveScene = GetUserPreferredActiveScene(sceneUnloadData.PreferredActiveScene, asServer, out byUser);
		SetActiveScene(userPreferredActiveScene, asServer, byUser);
		if (asServer && ConnectionActive(asServer: true))
		{
			UnloadScenesBroadcast unloadScenesBroadcast = default(UnloadScenesBroadcast);
			unloadScenesBroadcast.QueueData = data;
			UnloadScenesBroadcast message = unloadScenesBroadcast;
			if (data.ScopeType == SceneScopeType.Global)
			{
				_serverManager.Broadcast(message);
			}
			else if (data.ScopeType == SceneScopeType.Connections && data.Connections != null)
			{
				for (int j = 0; j < data.Connections.Length; j++)
				{
					if (data.Connections[j].IsValid())
					{
						data.Connections[j].Broadcast(message);
					}
				}
			}
		}
		else if (!asServer)
		{
			NetworkConnection connection = NetworkManager.ClientManager.Connection;
			foreach (Scene item2 in unloadableScenes)
			{
				if (item2.IsValid())
				{
					connection.RemoveFromScene(item2);
				}
			}
		}
		InvokeOnSceneUnloadEnd(data, unloadableScenes, unloadedScenes);
	}

	private void OnServerUnloadedScenes(UnloadScenesBroadcast msg, Channel channel)
	{
		UnloadQueueData queueData = msg.QueueData;
		if (queueData.ScopeType == SceneScopeType.Global)
		{
			UnloadGlobalScenes_Internal(queueData.SceneUnloadData, queueData.GlobalScenes, asServer: false);
		}
		else
		{
			UnloadConnectionScenes_Internal(Array.Empty<NetworkConnection>(), queueData.SceneUnloadData, queueData.GlobalScenes, asServer: false);
		}
	}

	private void MoveClientHostObjects(Scene scene, bool asServer)
	{
		if (!_moveClientObjects || !asServer || !NetworkManager.IsClientStarted)
		{
			return;
		}
		NetworkConnection connection = NetworkManager.ClientManager.Connection;
		List<NetworkObject> list = CollectionCaches<NetworkObject>.RetrieveList();
		foreach (NetworkObject value in NetworkManager.ServerManager.Objects.Spawned.Values)
		{
			if (!(value.gameObject.scene != scene) && value.Observers.Contains(connection) && !(value.transform.root != null))
			{
				list.Add(value);
			}
		}
		int count = list.Count;
		if (count > 0)
		{
			Scene delayedDestroyScene = GetDelayedDestroyScene();
			for (int i = 0; i < count; i++)
			{
				NetworkObject networkObject = list[i];
				networkObject.ClearRuntimeSceneObject();
				if (!networkObject.IsDeinitializing)
				{
					networkObject.Despawn();
				}
				else
				{
					networkObject.gameObject.SetActive(value: false);
				}
				UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(networkObject.gameObject, delayedDestroyScene);
			}
		}
		CollectionCaches<NetworkObject>.Store(list);
	}

	internal bool InSceneConnections(NetworkConnection conn, Scene scene)
	{
		if (!SceneConnections.TryGetValueIL2CPP(scene, out var value))
		{
			return false;
		}
		return value.Contains(conn);
	}

	public void AddOwnerToDefaultScene(NetworkObject nob)
	{
		if (!nob.Owner.IsValid)
		{
			NetworkManager.LogWarning("NetworkObject " + nob.name + " does not have an owner.");
		}
		else if (_globalScenes.Length == 0)
		{
			AddConnectionToScene(nob.Owner, nob.gameObject.scene);
		}
	}

	public void AddConnectionToScene(NetworkConnection conn, Scene scene)
	{
		if (!conn.IsValid())
		{
			return;
		}
		if (!scene.isLoaded || !scene.IsValid())
		{
			NetworkManager.LogError("Only valid, loaded scenes may be used.");
			return;
		}
		HashSet<NetworkConnection> value;
		bool flag = SceneConnections.TryGetValueIL2CPP(scene, out value);
		if (!flag)
		{
			value = new HashSet<NetworkConnection>();
		}
		if (value.Add(conn))
		{
			conn.AddToScene(scene);
			if (!flag)
			{
				SceneConnections[scene] = value;
			}
			NetworkConnection[] array = new NetworkConnection[1] { conn };
			InvokeClientPresenceChange(scene, array, added: true, start: true);
			RebuildObservers(array);
			InvokeClientPresenceChange(scene, array, added: true, start: false);
			RebuildObservers(conn.Objects.ToArray());
		}
	}

	public void RemoveConnectionsFromNonGlobalScenes(NetworkConnection[] conns)
	{
		List<Scene> list = new List<Scene>();
		NetworkConnection[] array;
		foreach (KeyValuePair<Scene, HashSet<NetworkConnection>> sceneConnection in SceneConnections)
		{
			Scene key = sceneConnection.Key;
			if (IsGlobalScene(key))
			{
				continue;
			}
			HashSet<NetworkConnection> value = sceneConnection.Value;
			List<NetworkConnection> list2 = new List<NetworkConnection>();
			array = conns;
			foreach (NetworkConnection networkConnection in array)
			{
				if (networkConnection.IsValid() && value.Remove(networkConnection))
				{
					networkConnection.RemoveFromScene(key);
					list2.Add(networkConnection);
				}
			}
			if (value.Count == 0)
			{
				list.Add(key);
			}
			if (list2.Count > 0)
			{
				InvokeClientPresenceChange(key, list2, added: false, start: true);
				RebuildObservers(list2);
				InvokeClientPresenceChange(key, list2, added: false, start: false);
			}
		}
		foreach (Scene item in list)
		{
			SceneConnections.Remove(item);
		}
		array = conns;
		foreach (NetworkConnection networkConnection2 in array)
		{
			RebuildObservers(networkConnection2.Objects.ToArray());
		}
	}

	public void RemoveConnectionsFromScene(NetworkConnection[] conns, Scene scene)
	{
		if (!SceneConnections.TryGetValueIL2CPP(scene, out var value))
		{
			return;
		}
		List<NetworkConnection> list = new List<NetworkConnection>();
		NetworkConnection[] array = conns;
		foreach (NetworkConnection networkConnection in array)
		{
			if (networkConnection.IsValid() && value.Remove(networkConnection))
			{
				networkConnection.RemoveFromScene(scene);
				list.Add(networkConnection);
			}
		}
		if (value.Count == 0)
		{
			SceneConnections.Remove(scene);
		}
		if (list.Count > 0)
		{
			NetworkConnection[] array2 = list.ToArray();
			InvokeClientPresenceChange(scene, array2, added: false, start: true);
			RebuildObservers(array2);
			InvokeClientPresenceChange(scene, array2, added: false, start: false);
		}
		array = conns;
		foreach (NetworkConnection networkConnection2 in array)
		{
			RebuildObservers(networkConnection2.Objects.ToArray());
		}
	}

	public void RemoveAllConnectionsFromScene(Scene scene)
	{
		if (!SceneConnections.TryGetValueIL2CPP(scene, out var value))
		{
			return;
		}
		foreach (NetworkConnection item in value)
		{
			item.RemoveFromScene(scene);
		}
		NetworkConnection[] array = value.ToArray();
		value.Clear();
		SceneConnections.Remove(scene);
		if (array.Length != 0)
		{
			InvokeClientPresenceChange(scene, array, added: false, start: true);
			RebuildObservers(array);
			InvokeClientPresenceChange(scene, array, added: false, start: false);
		}
		NetworkConnection[] array2 = array;
		foreach (NetworkConnection networkConnection in array2)
		{
			RebuildObservers(networkConnection.Objects.ToArray());
		}
	}

	private bool CanLoadScene(LoadQueueData qd, SceneLookupData sld)
	{
		bool foundByHandle;
		bool flag = !string.IsNullOrEmpty(sld.GetScene(out foundByHandle).name);
		if (flag)
		{
			if (!qd.AsServer)
			{
				return false;
			}
			if (!qd.SceneLoadData.Options.AllowStacking)
			{
				return false;
			}
			if (flag && foundByHandle)
			{
				return false;
			}
		}
		return true;
	}

	private void RebuildObservers(IList<NetworkObject> networkObjects)
	{
		int count = networkObjects.Count;
		for (int i = 0; i < count; i++)
		{
			NetworkObject networkObject = networkObjects[i];
			if (networkObject != null && networkObject.IsSpawned)
			{
				_serverManager.Objects.RebuildObservers(networkObject);
			}
		}
	}

	internal void RebuildObservers(NetworkConnection connection)
	{
		List<NetworkConnection> list = CollectionCaches<NetworkConnection>.RetrieveList(connection);
		RebuildObservers(list);
		CollectionCaches<NetworkConnection>.Store(list);
	}

	internal void RebuildObservers(IList<NetworkConnection> connections)
	{
		int count = connections.Count;
		for (int i = 0; i < count; i++)
		{
			_serverManager.Objects.RebuildObservers(connections[i]);
		}
	}

	private void InvokeClientPresenceChange(Scene scene, IList<NetworkConnection> conns, bool added, bool start)
	{
		int count = conns.Count;
		for (int i = 0; i < count; i++)
		{
			NetworkConnection conn = conns[i];
			ClientPresenceChangeEventArgs obj = new ClientPresenceChangeEventArgs(scene, conn, added);
			if (start)
			{
				this.OnClientPresenceChangeStart?.Invoke(obj);
			}
			else
			{
				this.OnClientPresenceChangeEnd?.Invoke(obj);
			}
		}
	}

	private Scene[] GetScenes(SceneLookupData[] datas)
	{
		List<Scene> list = new List<Scene>();
		for (int i = 0; i < datas.Length; i++)
		{
			bool foundByHandle;
			Scene scene = datas[i].GetScene(out foundByHandle);
			if (!string.IsNullOrEmpty(scene.name))
			{
				list.Add(scene);
			}
		}
		return list.ToArray();
	}

	public static Scene GetScene(string sceneName, NetworkManager nm = null, bool warnIfDuplicates = true)
	{
		Scene result = default(Scene);
		sceneName = sceneName.ToLower();
		int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
		for (int i = 0; i < sceneCount; i++)
		{
			Scene sceneAt = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
			if (!(sceneAt.name.ToLower() == sceneName))
			{
				continue;
			}
			if (result.IsValid())
			{
				if (warnIfDuplicates)
				{
					string message = "Scene name " + sceneAt.name + " is loaded multiple times. The first scene found will be returned. If you wish to unload multiple instances of a scene with the same name create SceneLookupData using scene handles instead of name.";
					nm.LogWarning(message);
					break;
				}
			}
			else
			{
				result = sceneAt;
			}
		}
		return result;
	}

	public static Scene GetScene(int sceneHandle)
	{
		int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
		for (int i = 0; i < sceneCount; i++)
		{
			Scene sceneAt = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
			if (sceneAt.handle == sceneHandle)
			{
				return sceneAt;
			}
		}
		return default(Scene);
	}

	private bool IsGlobalScene(Scene scene)
	{
		string[] globalScenes = _globalScenes;
		foreach (string obj in globalScenes)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(obj);
			if (obj == scene.name || fileNameWithoutExtension == scene.name)
			{
				return true;
			}
		}
		return false;
	}

	private void CheckForDuplicateGlobalSceneNames()
	{
		HashSet<string> hashSet = CollectionCaches<string>.RetrieveHashSet();
		string[] globalScenes = _globalScenes;
		for (int i = 0; i < globalScenes.Length; i++)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(globalScenes[i]);
			if (hashSet.Contains(fileNameWithoutExtension))
			{
				NetworkManager.LogWarning("There are multiple global scenes loaded with the same NameOnly. This occurs when a global scene has the same name as another but resides in a different folder path. Each global scene name must be unique.");
				break;
			}
			hashSet.Add(fileNameWithoutExtension);
		}
	}

	private void RemoveFromGlobalScenes(Scene scene)
	{
		RemoveFromGlobalScenes(new SceneLookupData[1] { SceneLookupData.CreateData(scene) });
	}

	private void RemoveFromGlobalScenes(SceneLookupData[] datas)
	{
		List<string> list = _globalScenes.ToList();
		int count = list.Count;
		for (int i = 0; i < datas.Length; i++)
		{
			list.Remove(datas[i].Name);
		}
		if (count != list.Count)
		{
			_globalScenes = list.ToArray();
		}
	}

	private void RemoveGlobalScenes(List<Scene> scenes)
	{
		for (int i = 0; i < scenes.Count; i++)
		{
			string[] globalScenes = _globalScenes;
			for (int j = 0; j < globalScenes.Length; j++)
			{
				if (globalScenes[j] == scenes[i].name)
				{
					scenes.RemoveAt(i);
					i--;
				}
			}
		}
	}

	private void RemoveOccupiedScenes(List<Scene> scenes)
	{
		for (int i = 0; i < scenes.Count; i++)
		{
			if (SceneConnections.TryGetValueIL2CPP(scenes[i], out var _))
			{
				scenes.RemoveAt(i);
				i--;
			}
		}
	}

	private void AddPendingLoad(NetworkConnection conn)
	{
		NetworkConnection[] array = CollectionCaches<NetworkConnection>.RetrieveArray();
		if (array.Length == 0)
		{
			array = new NetworkConnection[1];
		}
		array[0] = conn;
		AddPendingLoad(array, 1);
		CollectionCaches<NetworkConnection>.Store(array, 1);
	}

	private void AddPendingLoad(NetworkConnection[] conns, int count)
	{
		foreach (NetworkConnection networkConnection in conns)
		{
			if (networkConnection.IsActive && networkConnection.IsAuthenticated)
			{
				if (_pendingClientSceneChanges.TryGetValue(networkConnection, out var value))
				{
					_pendingClientSceneChanges[networkConnection] = value + 1;
				}
				else
				{
					_pendingClientSceneChanges[networkConnection] = 1;
				}
			}
		}
	}

	private void SetActiveScene(Scene preferredScene, bool asServer, bool byUser)
	{
		if (byUser && preferredScene.IsValid())
		{
			CompleteSetActive(preferredScene);
			return;
		}
		if (!_setActiveScene)
		{
			Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
			CompleteSetActive(activeScene);
			return;
		}
		Scene scene2 = default(Scene);
		if (_globalScenes.Length != 0)
		{
			scene2 = GetScene(_globalScenes[0], NetworkManager, warnIfDuplicates: false);
		}
		else if (preferredScene.IsValid())
		{
			scene2 = preferredScene;
		}
		if (string.IsNullOrEmpty(scene2.name) && UnityEngine.SceneManagement.SceneManager.GetActiveScene() == _movedObjectsScene)
		{
			scene2 = GetFallbackActiveScene();
		}
		CompleteSetActive(scene2);
		void CompleteSetActive(Scene scene)
		{
			bool num = scene.IsValid();
			if (num)
			{
				UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
			}
			this.OnActiveSceneSet?.Invoke(byUser);
			this.OnActiveSceneSetInternal?.Invoke(asServer);
			if (num)
			{
				if (_lightProbeUpdating == LightProbeUpdateType.Asynchronous)
				{
					LightProbes.TetrahedralizeAsync();
				}
				else if (_lightProbeUpdating == LightProbeUpdateType.BlockThread)
				{
					LightProbes.Tetrahedralize();
				}
			}
		}
	}

	private Scene GetFallbackActiveScene()
	{
		return _sceneProcessor.GetFallbackActiveScene();
	}

	private Scene GetMovedObjectsScene()
	{
		return _sceneProcessor.GetMovedObjectsScene();
	}

	private Scene GetDelayedDestroyScene()
	{
		return _sceneProcessor.GetDelayedDestroyScene();
	}

	private Scene GetUserPreferredActiveScene(PreferredScene ps, bool asServer, out bool byUser)
	{
		byUser = false;
		SceneLookupData sceneLookupData = (asServer ? ps.Server : ps.Client);
		if (sceneLookupData == null)
		{
			return default(Scene);
		}
		bool foundByHandle;
		Scene scene = sceneLookupData.GetScene(out foundByHandle);
		if (scene.IsValid())
		{
			byUser = true;
		}
		return scene;
	}

	internal bool IsIteratingQueue(float completionTimeRequirement = 0f)
	{
		if (!IteratingQueue)
		{
			return Time.unscaledTime - QueueCompleteTime < completionTimeRequirement;
		}
		return true;
	}

	private bool SceneDataInvalid(SceneLoadData data, bool error)
	{
		bool num = data.DataInvalid();
		if (num && error)
		{
			NetworkManager.LogError("One or more datas in SceneLoadData are invalid.This generally occurs when calling this method without specifying any scenes or when data fields are null.");
		}
		return num;
	}

	private bool SceneDataInvalid(SceneUnloadData data, bool error)
	{
		bool num = data.DataInvalid();
		if (num && error)
		{
			NetworkManager.LogError("One or more datas in SceneLoadData are invalid.This generally occurs when calling this method without specifying any scenes or when data fields are null.");
		}
		return num;
	}

	private bool ConnectionActive(bool asServer)
	{
		if (!asServer)
		{
			return NetworkManager.IsClientStarted;
		}
		return NetworkManager.IsServerStarted;
	}

	private bool CanExecute(bool asServer, bool warn)
	{
		bool flag;
		if (asServer)
		{
			flag = NetworkManager.IsServerStarted;
			if (!flag && warn)
			{
				NetworkManager.LogWarning("Method cannot be called as the server is not active.");
			}
		}
		else
		{
			flag = NetworkManager.IsClientStarted;
			if (!flag && warn)
			{
				NetworkManager.LogWarning("Method cannot be called as the client is not active.");
			}
		}
		return flag;
	}
}
