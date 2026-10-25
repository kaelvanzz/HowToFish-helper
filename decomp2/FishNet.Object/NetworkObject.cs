using System;
using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.CodeAnalysis.Annotations;
using FishNet.Component.ColliderRollback;
using FishNet.Component.Observing;
using FishNet.Component.Ownership;
using FishNet.Component.Prediction;
using FishNet.Component.Transforming;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Observing;
using FishNet.Managing.Predicting;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
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

namespace FishNet.Object;

[DefaultExecutionOrder(-32767)]
[DisallowMultipleComponent]
public class NetworkObject : MonoBehaviour, IOrderable
{
	public delegate void HostVisibilityUpdatedDelegate(bool prevVisible, bool nextVisible);

	[Serializable]
	internal enum PredictionType : byte
	{
		Other,
		Rigidbody,
		Rigidbody2D
	}

	private bool _onStartServerCalled;

	private bool _onStartClientCalled;

	[HideInInspector]
	public NetworkObserver NetworkObserver;

	[HideInInspector]
	public HashSet<NetworkConnection> Observers = new HashSet<NetworkConnection>();

	internal GridEntry HashGridEntry;

	internal uint ObserverAddedTick;

	private bool _networkObserverInitiliazed;

	[NonSerialized]
	private List<Renderer> _renderers;

	private bool _renderersPopulated;

	private bool _lastClientHostVisibility;

	private HashGrid _hashGrid;

	private float _nextHashGridUpdateTime;

	private bool _isStatic;

	private Vector2Int _hashGridPosition = HashGrid.UnsetGridPosition;

	private RigidbodyPauser _rigidbodyPauser;

	[Tooltip("True if this object uses prediction methods.")]
	[SerializeField]
	private bool _enablePrediction;

	[Tooltip("What type of component is being used for prediction? If not using rigidbodies set to other.")]
	[SerializeField]
	private PredictionType _predictionType;

	[Tooltip("Object containing graphics when using prediction. This should be child of the predicted root.")]
	[SerializeField]
	private Transform _graphicalObject;

	[Tooltip("True to detach and re-attach the graphical object at runtime when the client initializes/deinitializes the item. This can resolve camera jitter or be helpful objects child of the graphical which do not handle reconiliation well, such as certain animation rigs. Transform is detached after OnStartClient, and reattached before OnStopClient.")]
	[SerializeField]
	private bool _detachGraphicalObject;

	[Tooltip("True to forward replicate and reconcile states to all clients. This is ideal with games where you want all clients and server to run the same inputs. False to only use prediction on the owner, and synchronize to spectators using other means such as a NetworkTransform.")]
	[SerializeField]
	private bool _enableStateForwarding = true;

	[Tooltip("NetworkTransform to configure for prediction. Specifying this is optional.")]
	[SerializeField]
	private NetworkTransform _networkTransform;

	[Tooltip("How many ticks to interpolate graphics on objects owned by the client. Typically low as 1 can be used to smooth over the frames between ticks.")]
	[Range(1f, 255f)]
	[SerializeField]
	private byte _ownerInterpolation = 1;

	[SerializeField]
	private TransformPropertiesFlag _ownerSmoothedProperties = (TransformPropertiesFlag)255u;

	[Tooltip("Interpolation amount of adaptive interpolation to use on non-owned objects. Higher levels result in more interpolation. When off spectatorInterpolation is used; when on interpolation based on strength and local client latency is used.")]
	[SerializeField]
	private AdaptiveInterpolationType _adaptiveInterpolation = AdaptiveInterpolationType.Low;

	[SerializeField]
	private TransformPropertiesFlag _spectatorSmoothedProperties = (TransformPropertiesFlag)255u;

	[Tooltip("How many ticks to interpolate graphics on objects when not owned by the client.")]
	[Range(1f, 255f)]
	[SerializeField]
	private byte _spectatorInterpolation = 2;

	[Tooltip("True to enable teleport threshhold.")]
	[SerializeField]
	private bool _enableTeleport;

	[Tooltip("Distance the graphical object must move between ticks to teleport the transform properties.")]
	[Range(0.001f, 65535f)]
	[SerializeField]
	private float _teleportThreshold = 1f;

	private List<NetworkBehaviour> _predictionBehaviours = new List<NetworkBehaviour>();

	private NetworkConnection _owner;

	private List<ushort> _rpcLinkIndexes;

	[SerializeField]
	[HideInInspector]
	internal TransformProperties SerializedTransformProperties;

	[NonSerialized]
	private static double _lastSceneIdAutomaticRebuildTime;

	[SerializeField]
	[HideInInspector]
	internal bool WasActiveDuringEdit;

	[SerializeField]
	[HideInInspector]
	internal bool WasActiveDuringEdit_Set1;

	[HideInInspector]
	public List<NetworkBehaviour> NetworkBehaviours;

	[HideInInspector]
	public NetworkBehaviour InitializedParentNetworkBehaviour;

	[HideInInspector]
	public List<NetworkObject> InitializedNestedNetworkObjects = new List<NetworkObject>();

	[HideInInspector]
	public NetworkBehaviour RuntimeParentNetworkBehaviour;

	[HideInInspector]
	public List<NetworkBehaviour> RuntimeChildNetworkBehaviours;

	[NonSerialized]
	internal NetworkObjectState State;

	[Tooltip("True if the object will always initialize as a networked object. When false the object will not automatically initialize over the network. Using Spawn() on an object will always set that instance as networked.")]
	[SerializeField]
	private bool _isNetworked = true;

	[Tooltip("True if the object can be spawned at runtime; this is generally false for scene prefabs you do not spawn.")]
	[SerializeField]
	private bool _isSpawnable = true;

	[Tooltip("True to make this object global, and added to the DontDestroyOnLoad scene. This value may only be set for instantiated objects, and can be changed if done immediately after instantiating.")]
	[SerializeField]
	private bool _isGlobal;

	[Tooltip("Order to initialize this object's callbacks when spawned with other NetworkObjects in the same tick. Default value is 0, negative values will execute callbacks first.")]
	[Range(-128f, 127f)]
	[SerializeField]
	private sbyte _initializeOrder;

	[Tooltip("True to keep this object spawned when the owner disconnects.")]
	[SerializeField]
	private bool _preventDespawnOnDisconnect;

	[SerializeField]
	[Tooltip("How to handle this object when it despawns. Scene objects are never destroyed when despawning.")]
	private DespawnType _defaultDespawnType;

	private bool _disabledNetworkBehavioursInitialized;

	private bool _initializedValusSet;

	public const int UNSET_SCENEID_VALUE = 0;

	public const int UNSET_OBJECTID_VALUE = 65535;

	public const int UNSET_PREFABID_VALUE = 65535;

	public bool IsObjectReconciling { get; internal set; }

	[Obsolete("This field will be removed in v5. Instead reference NetworkTickSmoother on each graphical object used.")]
	public TransformTickSmoother PredictionSmoother { get; private set; }

	public RigidbodyPauser RigidbodyPauser => _rigidbodyPauser;

	public bool EnablePrediction => _enablePrediction;

	public bool EnableStateForwarding
	{
		get
		{
			if (_enablePrediction)
			{
				return _enableStateForwarding;
			}
			return false;
		}
	}

	[Obsolete("Use IsClientOnlyInitialized. Note the difference between IsClientOnlyInitialized and IsClientOnlyStarted.")]
	public bool IsClientOnly => IsClientOnlyInitialized;

	[Obsolete("Use IsServerOnlyInitialized. Note the difference between IsServerOnlyInitialized and IsServerOnlyStarted.")]
	public bool IsServerOnly => IsServerOnlyInitialized;

	[Obsolete("Use IsHostInitialized. Note the difference between IsHostInitialized and IsHostStarted.")]
	public bool IsHost => IsHostInitialized;

	[Obsolete("Use IsClientInitialized. Note the difference between IsClientInitialized and IsClientStarted.")]
	public bool IsClient => IsClientInitialized;

	[Obsolete("Use IsServerInitialized. Note the difference between IsServerInitialized and IsServerStarted.")]
	public bool IsServer => IsServerInitialized;

	public bool IsDestroying { get; private set; }

	internal bool AllowPredictedSpawning
	{
		get
		{
			if (!(PredictedSpawn == null))
			{
				return PredictedSpawn.GetAllowSpawning();
			}
			return false;
		}
	}

	internal bool AllowPredictedDespawning
	{
		get
		{
			if (!(PredictedSpawn == null))
			{
				return PredictedSpawn.GetAllowDespawning();
			}
			return false;
		}
	}

	public bool IsClientInitialized { get; private set; }

	public bool IsClientStarted
	{
		get
		{
			if (!(NetworkManager == null))
			{
				return NetworkManager.IsClientStarted;
			}
			return false;
		}
	}

	public bool IsClientOnlyInitialized
	{
		get
		{
			if (!IsServerInitialized)
			{
				return IsClientInitialized;
			}
			return false;
		}
	}

	public bool IsClientOnlyStarted
	{
		get
		{
			if (IsClientStarted)
			{
				return !IsServerStarted;
			}
			return false;
		}
	}

	public bool IsServerInitialized { get; private set; }

	public bool IsServerStarted
	{
		get
		{
			if (!(NetworkManager == null))
			{
				return NetworkManager.IsServerStarted;
			}
			return false;
		}
	}

	public bool IsServerOnlyInitialized
	{
		get
		{
			if (IsServerInitialized)
			{
				return !IsClientInitialized;
			}
			return false;
		}
	}

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

	public bool IsHostStarted
	{
		get
		{
			if (IsClientStarted)
			{
				return IsServerStarted;
			}
			return false;
		}
	}

	public bool IsHostInitialized
	{
		get
		{
			if (IsClientInitialized)
			{
				return IsServerInitialized;
			}
			return false;
		}
	}

	public bool IsOffline
	{
		get
		{
			if (!IsClientStarted)
			{
				return !IsServerStarted;
			}
			return false;
		}
	}

	public bool IsManagerReconciling => PredictionManager.IsReconciling;

	public bool IsTakingOwnership
	{
		get
		{
			if (PredictedOwner != null)
			{
				return PredictedOwner.TakingOwnership;
			}
			return false;
		}
	}

	[PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "OnStartServer", "")]
	[PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "OnStartNetwork", " Use base.Owner.IsLocalClient instead.")]
	[PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "Awake", "")]
	[PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "Start", "")]
	public bool IsOwner
	{
		get
		{
			if (!IsClientInitialized)
			{
				return false;
			}
			return Owner.IsLocalClient;
		}
	}

	[PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "OnStartServer", "")]
	[PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "OnStartNetwork", " Use (base.Owner.IsLocalClient || (base.IsServerInitialized && !Owner.Isvalid) instead.")]
	[PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "Awake", "")]
	[PreventUsageInside("global::FishNet.Object.NetworkBehaviour", "Start", "")]
	public bool IsController
	{
		get
		{
			if (!IsOwner)
			{
				if (IsServerInitialized)
				{
					return !Owner.IsValid;
				}
				return false;
			}
			return true;
		}
	}

	[Obsolete("Use IsController.")]
	public bool HasAuthority => IsController;

	public NetworkConnection Owner
	{
		get
		{
			if (_owner == null)
			{
				return NetworkManager.EmptyConnection;
			}
			return _owner;
		}
		private set
		{
			_owner = value;
		}
	}

	public int OwnerId
	{
		get
		{
			if (Owner.IsValid)
			{
				return Owner.ClientId;
			}
			return -1;
		}
	}

	public bool IsSpawned
	{
		get
		{
			if (!IsDeinitializing)
			{
				return ObjectId != 65535;
			}
			return false;
		}
	}

	public NetworkConnection LocalConnection
	{
		get
		{
			if (!(NetworkManager == null))
			{
				return NetworkManager.ClientManager.Connection;
			}
			return new NetworkConnection();
		}
	}

	public NetworkManager NetworkManager { get; private set; }

	public ServerManager ServerManager { get; private set; }

	public ClientManager ClientManager { get; private set; }

	public ObserverManager ObserverManager { get; private set; }

	public TransportManager TransportManager { get; private set; }

	public TimeManager TimeManager { get; private set; }

	public FishNet.Managing.Scened.SceneManager SceneManager { get; private set; }

	public PredictionManager PredictionManager { get; private set; }

	public RollbackManager RollbackManager { get; private set; }

	[field: SerializeField]
	[field: HideInInspector]
	public ushort PrefabId { get; internal set; } = ushort.MaxValue;


	[field: SerializeField]
	[field: HideInInspector]
	public ushort SpawnableCollectionId { get; internal set; }

	[field: SerializeField]
	[field: HideInInspector]
	public ulong AssetPathHash { get; private set; }

	[field: SerializeField]
	[field: HideInInspector]
	internal ulong SceneId { get; private set; }

	[field: SerializeField]
	[field: HideInInspector]
	public bool IsNested { get; private set; }

	public bool IsInitializedNested => InitializedParentNetworkBehaviour != null;

	public NetworkConnection PredictedSpawner { get; private set; } = NetworkManager.EmptyConnection;


	public bool IsSceneObject => SceneId != 0;

	[field: SerializeField]
	[field: HideInInspector]
	public byte ComponentIndex { get; private set; }

	public int ObjectId { get; private set; } = 65535;


	internal bool IsDeinitializing { get; private set; } = true;


	[field: SerializeField]
	[field: HideInInspector]
	public PredictedSpawn PredictedSpawn { get; private set; }

	[field: SerializeField]
	[field: HideInInspector]
	public PredictedOwner PredictedOwner { get; private set; }

	internal NetworkBehaviour CurrentParentNetworkBehaviour
	{
		get
		{
			if (RuntimeParentNetworkBehaviour != null)
			{
				return RuntimeParentNetworkBehaviour;
			}
			if (InitializedParentNetworkBehaviour != null)
			{
				return InitializedParentNetworkBehaviour;
			}
			return null;
		}
	}

	[Obsolete("Use Get/SetIsNetworked.")]
	public bool IsNetworked
	{
		get
		{
			return GetIsNetworked();
		}
		private set
		{
			SetIsNetworked(value);
		}
	}

	[Obsolete("Use GetIsSpawnable.")]
	public bool IsSpawnable => _isSpawnable;

	public bool IsGlobal
	{
		get
		{
			return _isGlobal;
		}
		private set
		{
			_isGlobal = value;
		}
	}

	public int Order
	{
		get
		{
			return _initializeOrder + GetOrderModifier();
			int GetOrderModifier()
			{
				int num = 1;
				NetworkBehaviour currentParentNetworkBehaviour = CurrentParentNetworkBehaviour;
				while (currentParentNetworkBehaviour != null)
				{
					num++;
					currentParentNetworkBehaviour = currentParentNetworkBehaviour.NetworkObject.CurrentParentNetworkBehaviour;
				}
				return num * 128;
			}
		}
	}

	internal bool PreventDespawnOnDisconnect => _preventDespawnOnDisconnect;

	public event HostVisibilityUpdatedDelegate OnHostVisibilityUpdated;

	public event Action<NetworkObject> OnObserversActive;

	public void Broadcast<T>(T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable) where T : struct, IBroadcast
	{
		if (NetworkManager == null)
		{
			NetworkManager.LogWarning("Cannot send broadcast from " + base.gameObject.name + ", NetworkManager reference is null. This may occur if the object is not spawned or initialized.");
		}
		else
		{
			NetworkManager.ServerManager.Broadcast(Observers, message, requireAuthenticated, channel);
		}
	}

	private void InvokeStartCallbacks(bool asServer, bool invokeSyncTypeCallbacks)
	{
		if (asServer || IsServerOnlyStarted || IsClientOnlyInitialized)
		{
			for (int i = 0; i < NetworkBehaviours.Count; i++)
			{
				NetworkBehaviours[i].InvokeOnNetwork_Internal(start: true);
			}
		}
		if (asServer)
		{
			for (int j = 0; j < NetworkBehaviours.Count; j++)
			{
				NetworkBehaviours[j].OnStartServer_Internal();
			}
			_onStartServerCalled = true;
			for (int k = 0; k < NetworkBehaviours.Count; k++)
			{
				NetworkBehaviours[k].OnOwnershipServer_Internal(NetworkManager.EmptyConnection);
			}
		}
		else
		{
			for (int l = 0; l < NetworkBehaviours.Count; l++)
			{
				NetworkBehaviours[l].OnStartClient_Internal();
			}
			_onStartClientCalled = true;
			for (int m = 0; m < NetworkBehaviours.Count; m++)
			{
				NetworkBehaviours[m].OnOwnershipClient_Internal(NetworkManager.EmptyConnection);
			}
		}
		if (invokeSyncTypeCallbacks)
		{
			InvokeOnStartSyncTypeCallbacks(asServer: true);
		}
		InvokeStartCallbacks_Prediction(asServer);
	}

	internal void InvokeOnStartSyncTypeCallbacks(bool asServer)
	{
		for (int i = 0; i < NetworkBehaviours.Count; i++)
		{
			NetworkBehaviours[i].InvokeSyncTypeOnStartCallbacks(asServer);
		}
	}

	internal void InvokeOnStopSyncTypeCallbacks(bool asServer)
	{
		for (int i = 0; i < NetworkBehaviours.Count; i++)
		{
			NetworkBehaviours[i].InvokeSyncTypeOnStopCallbacks(asServer);
		}
	}

	internal void OnSpawnServer(NetworkConnection conn)
	{
		for (int i = 0; i < NetworkBehaviours.Count; i++)
		{
			NetworkBehaviours[i].SendBufferedRpcs(conn);
		}
		for (int j = 0; j < NetworkBehaviours.Count; j++)
		{
			NetworkBehaviours[j].OnSpawnServer(conn);
		}
	}

	internal void InvokeOnServerDespawn(NetworkConnection conn)
	{
		for (int i = 0; i < NetworkBehaviours.Count; i++)
		{
			NetworkBehaviours[i].OnDespawnServer(conn);
		}
	}

	internal void InvokeStopCallbacks(bool asServer, bool invokeSyncTypeCallbacks)
	{
		InvokeStopCallbacks_Prediction(asServer);
		if (invokeSyncTypeCallbacks)
		{
			InvokeOnStopSyncTypeCallbacks(asServer);
		}
		if (asServer && _onStartServerCalled)
		{
			for (int i = 0; i < NetworkBehaviours.Count; i++)
			{
				NetworkBehaviours[i].OnStopServer_Internal();
			}
			if (!_onStartClientCalled)
			{
				InvokeOnNetwork();
			}
			_onStartServerCalled = false;
		}
		else if (!asServer && _onStartClientCalled)
		{
			for (int j = 0; j < NetworkBehaviours.Count; j++)
			{
				NetworkBehaviours[j].OnStopClient_Internal();
			}
			if (!_onStartServerCalled)
			{
				InvokeOnNetwork();
			}
			_onStartClientCalled = false;
		}
		void InvokeOnNetwork()
		{
			for (int k = 0; k < NetworkBehaviours.Count; k++)
			{
				NetworkBehaviours[k].InvokeOnNetwork_Internal(start: false);
			}
		}
	}

	private void InvokeManualOwnershipChange(NetworkConnection prevOwner, bool asServer)
	{
		if (asServer)
		{
			for (int i = 0; i < NetworkBehaviours.Count; i++)
			{
				NetworkBehaviours[i].OnOwnershipServer_Internal(prevOwner);
			}
			WriteSyncTypesForManualOwnershipChange(prevOwner);
		}
		else if (!IsOwner || IsServerStarted || !(prevOwner == Owner))
		{
			for (int j = 0; j < NetworkBehaviours.Count; j++)
			{
				NetworkBehaviours[j].OnOwnershipClient_Internal(prevOwner);
			}
		}
	}

	internal void UpdateForNetworkObject(bool force)
	{
		if (_hashGrid == null || _isStatic)
		{
			return;
		}
		float unscaledTime = Time.unscaledTime;
		if (force || !(unscaledTime < _nextHashGridUpdateTime))
		{
			_nextHashGridUpdateTime = unscaledTime + 1f;
			Vector2Int hashGridPosition = _hashGrid.GetHashGridPosition(this);
			if (hashGridPosition != _hashGridPosition)
			{
				_hashGridPosition = hashGridPosition;
				HashGridEntry = _hashGrid.GetGridEntry(hashGridPosition);
			}
		}
	}

	public void UpdateRenderers(bool updateVisibility = true)
	{
		InitializeRendererCollection(force: true, updateVisibility);
	}

	public void SetRenderersVisible(bool visible, bool force = false)
	{
		if (force || NetworkObserver.UpdateHostVisibility)
		{
			UpdateRenderVisibility(visible);
		}
	}

	private void UpdateRenderVisibility(bool visible)
	{
		InitializeRendererCollection(force: false, updateVisibility: false);
		List<Renderer> renderers = _renderers;
		for (int i = 0; i < renderers.Count; i++)
		{
			Renderer renderer = renderers[i];
			if (renderer == null)
			{
				_renderers.RemoveAt(i);
				i--;
			}
			else
			{
				renderer.enabled = visible;
			}
		}
		if (this.OnHostVisibilityUpdated != null)
		{
			this.OnHostVisibilityUpdated(_lastClientHostVisibility, visible);
		}
		_lastClientHostVisibility = visible;
	}

	private void InitializeRendererCollection(bool force, bool updateVisibility)
	{
		if (!force && _renderersPopulated)
		{
			return;
		}
		List<Renderer> list = CollectionCaches<Renderer>.RetrieveList();
		GetComponentsInChildren(includeInactive: true, list);
		_renderers = new List<Renderer>();
		foreach (Renderer item in list)
		{
			if (item.enabled)
			{
				_renderers.Add(item);
			}
		}
		CollectionCaches<Renderer>.Store(list);
		_renderersPopulated = true;
		if (updateVisibility)
		{
			UpdateRenderVisibility(_lastClientHostVisibility);
		}
	}

	private void AddDefaultNetworkObserverConditions()
	{
		if (!_networkObserverInitiliazed)
		{
			NetworkObserver = NetworkManager.ObserverManager.AddDefaultConditions(this);
		}
	}

	internal bool RemoveObserver(NetworkConnection connection)
	{
		int count = Observers.Count;
		bool num = Observers.Remove(connection);
		if (num)
		{
			TryInvokeOnObserversActive(count);
		}
		return num;
	}

	internal ObserverStateChange RebuildObservers(NetworkConnection connection, bool timedOnly)
	{
		if (!connection.IsValid)
		{
			NetworkManager.LogWarning("An invalid connection was used when rebuilding observers.");
			return ObserverStateChange.Unchanged;
		}
		if (!connection.IsActive)
		{
			Observers.Remove(connection);
			return ObserverStateChange.Unchanged;
		}
		if (IsDeinitializing)
		{
			return ObserverStateChange.Unchanged;
		}
		UpdateForNetworkObject(!timedOnly);
		int count = Observers.Count;
		ObserverStateChange observerStateChange = NetworkObserver.RebuildObservers(connection, timedOnly);
		switch (observerStateChange)
		{
		case ObserverStateChange.Added:
			Observers.Add(connection);
			break;
		case ObserverStateChange.Removed:
			Observers.Remove(connection);
			break;
		}
		if (observerStateChange != 0)
		{
			TryInvokeOnObserversActive(count);
		}
		return observerStateChange;
	}

	private void TryInvokeOnObserversActive(int startCount)
	{
		if (TimeManager != null)
		{
			ObserverAddedTick = TimeManager.LocalTick;
		}
		if (this.OnObserversActive != null && ((Observers.Count > 0 && startCount == 0) || (Observers.Count == 0 && startCount > 0)))
		{
			this.OnObserversActive(this);
		}
	}

	private void ResetState_Observers(bool asServer)
	{
		ObserverAddedTick = 0u;
	}

	public Transform GetGraphicalObject()
	{
		return _graphicalObject;
	}

	public void SetGraphicalObject(Transform t)
	{
		_graphicalObject = t;
		InitializeTickSmoother();
	}

	private void TimeManager_OnUpdate_Prediction()
	{
		if (_enablePrediction && PredictionSmoother != null)
		{
			PredictionSmoother.OnUpdate();
		}
	}

	private void InitializePredictionEarly(NetworkManager manager, bool asServer)
	{
		if (!_enablePrediction)
		{
			return;
		}
		if (!_enableStateForwarding && _networkTransform != null)
		{
			_networkTransform.ConfigureForPrediction(_predictionType);
		}
		if (asServer)
		{
			return;
		}
		InitializeSmoothers();
		if (_predictionBehaviours.Count <= 0)
		{
			return;
		}
		ChangePredictionSubscriptions(subscribe: true, manager);
		foreach (NetworkBehaviour predictionBehaviour in _predictionBehaviours)
		{
			predictionBehaviour.Preinitialize_Prediction(asServer);
		}
	}

	private void Deinitialize_Prediction(bool asServer)
	{
		if (!_enablePrediction)
		{
			return;
		}
		DeinitializeSmoothers();
		if (_predictionBehaviours.Count <= 0)
		{
			return;
		}
		ChangePredictionSubscriptions(subscribe: false, NetworkManager);
		foreach (NetworkBehaviour predictionBehaviour in _predictionBehaviours)
		{
			predictionBehaviour.Deinitialize_Prediction(asServer);
		}
	}

	private void ChangePredictionSubscriptions(bool subscribe, NetworkManager manager)
	{
		if (!(manager == null))
		{
			if (subscribe)
			{
				manager.PredictionManager.OnPreReconcile += PredictionManager_OnPreReconcile;
				manager.PredictionManager.OnReconcile += PredictionManager_OnReconcile;
				manager.PredictionManager.OnReplicateReplay += PredictionManager_OnReplicateReplay;
				manager.PredictionManager.OnPostReplicateReplay += PredictionManager_OnPostReplicateReplay;
				manager.PredictionManager.OnPostReconcile += PredictionManager_OnPostReconcile;
				manager.TimeManager.OnPreTick += TimeManager_OnPreTick;
				manager.TimeManager.OnPostTick += TimeManager_OnPostTick;
			}
			else
			{
				manager.PredictionManager.OnPreReconcile -= PredictionManager_OnPreReconcile;
				manager.PredictionManager.OnReconcile -= PredictionManager_OnReconcile;
				manager.PredictionManager.OnReplicateReplay -= PredictionManager_OnReplicateReplay;
				manager.PredictionManager.OnPostReplicateReplay -= PredictionManager_OnPostReplicateReplay;
				manager.PredictionManager.OnPostReconcile -= PredictionManager_OnPostReconcile;
				manager.TimeManager.OnPreTick -= TimeManager_OnPreTick;
				manager.TimeManager.OnPostTick -= TimeManager_OnPostTick;
			}
		}
	}

	private void InitializeSmoothers()
	{
		bool flag = _predictionType == PredictionType.Rigidbody;
		bool flag2 = _predictionType == PredictionType.Rigidbody2D;
		if (flag || flag2)
		{
			_rigidbodyPauser = ResettableObjectCaches<RigidbodyPauser>.Retrieve();
			RigidbodyType rbType = ((!flag) ? RigidbodyType.Rigidbody2D : RigidbodyType.Rigidbody);
			_rigidbodyPauser.UpdateRigidbodies(base.transform, rbType, getInChildren: true);
		}
		if (_graphicalObject == null)
		{
			NetworkManagerExtensions.Log("GraphicalObject is null on " + base.gameObject.name + ". This may be intentional, and acceptable, if you are smoothing between ticks yourself. Otherwise consider assigning the GraphicalObject field.");
			return;
		}
		if (PredictionSmoother == null)
		{
			PredictionSmoother = ResettableObjectCaches<TransformTickSmoother>.Retrieve();
		}
		InitializeTickSmoother();
	}

	private void InitializeTickSmoother()
	{
		if (PredictionSmoother != null)
		{
			float teleportDistance = (_enableTeleport ? _teleportThreshold : float.NegativeInfinity);
			PredictionSmoother.InitializeNetworked(this, _graphicalObject, _detachGraphicalObject, teleportDistance, (float)TimeManager.TickDelta, _ownerInterpolation, _ownerSmoothedProperties, _spectatorInterpolation, _spectatorSmoothedProperties, _adaptiveInterpolation);
		}
	}

	private void DeinitializeSmoothers()
	{
		if (PredictionSmoother != null)
		{
			PredictionSmoother.Deinitialize();
			ResettableObjectCaches<TransformTickSmoother>.Store(PredictionSmoother);
			PredictionSmoother = null;
			ResettableObjectCaches<RigidbodyPauser>.StoreAndDefault(ref _rigidbodyPauser);
		}
	}

	private void InvokeStartCallbacks_Prediction(bool asServer)
	{
		if (_predictionBehaviours.Count != 0 && !asServer)
		{
			TimeManager.OnUpdate += TimeManager_Update;
			if (PredictionSmoother != null)
			{
				PredictionSmoother.OnStartClient();
			}
		}
	}

	private void InvokeStopCallbacks_Prediction(bool asServer)
	{
		if (_predictionBehaviours.Count != 0 && !asServer)
		{
			if (TimeManager != null)
			{
				TimeManager.OnUpdate -= TimeManager_Update;
			}
			if (PredictionSmoother != null)
			{
				PredictionSmoother.OnStopClient();
			}
		}
	}

	private void TimeManager_OnPreTick()
	{
		if (PredictionSmoother != null)
		{
			PredictionSmoother.OnPreTick();
		}
	}

	private void PredictionManager_OnPostReplicateReplay(uint clientTick, uint serverTick)
	{
		if (PredictionSmoother != null)
		{
			PredictionSmoother.OnPostReplicateReplay(clientTick);
		}
	}

	private void TimeManager_OnPostTick()
	{
		if (PredictionSmoother != null)
		{
			PredictionSmoother.OnPostTick(NetworkManager.TimeManager.LocalTick);
		}
	}

	private void PredictionManager_OnPreReconcile(uint clientTick, uint serverTick)
	{
		if (PredictionSmoother != null)
		{
			PredictionSmoother.OnPreReconcile();
		}
	}

	private void PredictionManager_OnReconcile(uint clientReconcileTick, uint serverReconcileTick)
	{
		for (int i = 0; i < _predictionBehaviours.Count; i++)
		{
			_predictionBehaviours[i].Reconcile_Client_Start();
		}
		if (!IsObjectReconciling && _rigidbodyPauser != null)
		{
			_rigidbodyPauser.Pause();
		}
	}

	private void PredictionManager_OnPostReconcile(uint clientReconcileTick, uint serverReconcileTick)
	{
		for (int i = 0; i < _predictionBehaviours.Count; i++)
		{
			_predictionBehaviours[i].Reconcile_Client_End();
		}
		if (_rigidbodyPauser != null)
		{
			_rigidbodyPauser.Unpause();
		}
		IsObjectReconciling = false;
	}

	private void PredictionManager_OnReplicateReplay(uint clientTick, uint serverTick)
	{
		uint replayTick = (IsOwner ? clientTick : serverTick);
		for (int i = 0; i < _predictionBehaviours.Count; i++)
		{
			_predictionBehaviours[i].Replicate_Replay_Start(replayTick);
		}
	}

	internal void RegisterPredictionBehaviourOnce(NetworkBehaviour nb)
	{
		_predictionBehaviours.Add(nb);
	}

	internal void EmptyReplicatesQueueIntoHistory()
	{
		for (int i = 0; i < _predictionBehaviours.Count; i++)
		{
			_predictionBehaviours[i].EmptyReplicatesQueueIntoHistory_Start();
		}
	}

	internal void SetReplicateTick(uint value, bool createdReplicate)
	{
		if (createdReplicate && Owner.IsValid)
		{
			Owner.ReplicateTick.Update(NetworkManager.TimeManager, value);
		}
	}

	private void ResetState_Prediction(bool asServer)
	{
	}

	internal void SetIsDestroying(DespawnType? despawnType = null)
	{
		if (despawnType.HasValue)
		{
			if (despawnType.Value == DespawnType.Destroy)
			{
				IsDestroying = true;
			}
		}
		else if (GetDefaultDespawnType() == DespawnType.Destroy)
		{
			IsDestroying = true;
		}
	}

	public NetworkBehaviour GetNetworkBehaviour(byte componentIndex, bool error)
	{
		if (componentIndex >= NetworkBehaviours.Count && error)
		{
			string message = $"ComponentIndex of {componentIndex} is out of bounds on {base.gameObject.name} [id {ObjectId}]. This may occur if you have modified your gameObject/prefab without saving it, or the scene.";
			NetworkManager.LogError(message);
		}
		return NetworkBehaviours[componentIndex];
	}

	public void Despawn(GameObject go, DespawnType? despawnType = null)
	{
		if (NetworkManager != null)
		{
			NetworkManager.ServerManager.Despawn(go, despawnType);
		}
	}

	public void Despawn(NetworkObject nob, DespawnType? despawnType = null)
	{
		if (NetworkManager != null)
		{
			NetworkManager.ServerManager.Despawn(nob, despawnType);
		}
	}

	public void Despawn(DespawnType? despawnType = null)
	{
		if (NetworkManager != null)
		{
			NetworkManager.ServerManager.Despawn(this, despawnType);
		}
	}

	public void Spawn(GameObject go, NetworkConnection ownerConnection = null, Scene scene = default(Scene))
	{
		if (NetworkManager != null)
		{
			NetworkManager.ServerManager.Spawn(go, ownerConnection, scene);
		}
	}

	public void Spawn(NetworkObject nob, NetworkConnection ownerConnection = null, Scene scene = default(Scene))
	{
		if (NetworkManager != null)
		{
			NetworkManager.ServerManager.Spawn(nob, ownerConnection, scene);
		}
	}

	[Obsolete("Use SetLocalOwnership(NetworkConnection, bool).")]
	public void SetLocalOwnership(NetworkConnection caller)
	{
		SetLocalOwnership(caller, recursive: false);
	}

	public void SetLocalOwnership(NetworkConnection caller, bool recursive)
	{
		NetworkConnection owner = Owner;
		SetOwner(caller);
		int count = NetworkBehaviours.Count;
		for (int i = 0; i < count; i++)
		{
			NetworkBehaviours[i].OnOwnershipClient_Internal(owner);
		}
		if (!recursive)
		{
			return;
		}
		List<NetworkObject> networkObjects = GetNetworkObjects(GetNetworkObjectOption.AllNestedRecursive);
		foreach (NetworkObject item in networkObjects)
		{
			item.SetLocalOwnership(caller, recursive: true);
		}
		CollectionCaches<NetworkObject>.Store(networkObjects);
	}

	public void RegisterInvokeOnInstance<T>(Action<UnityEngine.Component> handler) where T : UnityEngine.Component
	{
		NetworkManager.RegisterInvokeOnInstance<T>(handler);
	}

	public void UnregisterInvokeOnInstance<T>(Action<UnityEngine.Component> handler) where T : UnityEngine.Component
	{
		NetworkManager.UnregisterInvokeOnInstance<T>(handler);
	}

	public bool HasInstance<T>() where T : UnityEngine.Component
	{
		return NetworkManager.HasInstance<T>();
	}

	public T GetInstance<T>() where T : UnityEngine.Component
	{
		return NetworkManager.GetInstance<T>();
	}

	public void RegisterInstance<T>(T component, bool replace = true) where T : UnityEngine.Component
	{
		NetworkManager.RegisterInstance(component, replace);
	}

	public bool TryRegisterInstance<T>(T component) where T : UnityEngine.Component
	{
		return NetworkManager.TryRegisterInstance(component);
	}

	public bool TryGetInstance<T>(out T component) where T : UnityEngine.Component
	{
		return NetworkManager.TryGetInstance<T>(out component);
	}

	public void UnregisterInstance<T>() where T : UnityEngine.Component
	{
		NetworkManager.UnregisterInstance<T>();
	}

	internal void SetRpcLinkIndexes(List<ushort> values)
	{
		_rpcLinkIndexes = values;
	}

	internal void RemoveClientRpcLinkIndexes()
	{
		NetworkManager.ClientManager.Objects.RemoveLinkIndexes(_rpcLinkIndexes);
	}

	public void SetSceneId(ulong sceneId)
	{
		SceneId = sceneId;
	}

	public void SetAssetPathHash(ulong value)
	{
		AssetPathHash = value;
	}

	internal void ClearRuntimeSceneObject()
	{
		if (!Application.isPlaying)
		{
			NetworkManagerExtensions.LogError("ClearRuntimeSceneObject may only be called at runtime.");
		}
		else
		{
			SceneId = 0uL;
		}
	}

	private void WriteSyncTypesForManualOwnershipChange(NetworkConnection prevOwner)
	{
		if (prevOwner.IsActive)
		{
			WriteForConnection(prevOwner, ReadPermission.ExcludeOwner);
		}
		if (Owner.IsActive)
		{
			WriteForConnection(Owner, ReadPermission.OwnerOnly);
		}
		void WriteForConnection(NetworkConnection conn, ReadPermission permission)
		{
			for (int i = 0; i < NetworkBehaviours.Count; i++)
			{
				NetworkBehaviours[i].WriteSyncTypesForConnection(conn, permission);
			}
		}
	}

	public bool GetIsNetworked()
	{
		return _isNetworked;
	}

	public void SetIsNetworked(bool value)
	{
		_isNetworked = value;
	}

	public bool GetIsSpawnable()
	{
		return _isSpawnable;
	}

	public void SetIsSpawnable(bool value)
	{
		_isSpawnable = value;
	}

	public void SetIsGlobal(bool value)
	{
		if (IsNested && !CurrentParentNetworkBehaviour.NetworkObject.IsGlobal)
		{
			NetworkManager.LogWarning("Object " + base.gameObject.name + " cannot change IsGlobal because it is nested and the parent NetorkObject is not global.");
			return;
		}
		if (!IsDeinitializing)
		{
			NetworkManager.LogWarning("Object " + base.gameObject.name + " cannot change IsGlobal as it's already initialized. IsGlobal may only be changed immediately after instantiating.");
			return;
		}
		if (IsSceneObject)
		{
			NetworkManager.LogWarning("Object " + base.gameObject.name + " cannot have be global because it is a scene object. Only instantiated objects may be global.");
			return;
		}
		_networkObserverInitiliazed = false;
		IsGlobal = value;
	}

	public int GetInitializeOrder()
	{
		return Order;
	}

	public DespawnType GetDefaultDespawnType()
	{
		return _defaultDespawnType;
	}

	public void SetDefaultDespawnType(DespawnType despawnType)
	{
		_defaultDespawnType = despawnType;
	}

	internal void UnsetInitializedValuesSet()
	{
		_initializedValusSet = false;
	}

	public override string ToString()
	{
		string text = ((base.gameObject == null) ? $"NetworkObject HashCode [{GetHashCode()}]" : $"GameObject HashCode [{base.gameObject.GetHashCode()}]");
		return $"Name [{base.gameObject.name}] ObjectId [{ObjectId}] OwnerId [{OwnerId}] {text}";
	}

	protected virtual void Awake()
	{
		_isStatic = base.gameObject.isStatic;
		if (!_initializedValusSet)
		{
			bool flag = false;
			Transform parent = base.transform.parent;
			while (parent != null)
			{
				if (parent.TryGetComponent<NetworkObject>(out var _))
				{
					flag = true;
					break;
				}
				parent = parent.parent;
			}
			if (!flag)
			{
				SetInitializedValues(null);
			}
		}
		SetChildDespawnedState();
	}

	protected virtual void Start()
	{
		TryStartDeactivation();
	}

	private void OnDisable()
	{
		if (IsDeinitializing && Owner.IsValid)
		{
			Owner.RemoveObject(this);
		}
		else if (!IsServerStarted && !IsClientStarted && IsSceneObject)
		{
			ResetState(asServer: true);
			ResetState(asServer: false);
		}
	}

	private void OnDestroy()
	{
		SetIsDestroying(DespawnType.Destroy);
		if (!_initializedValusSet)
		{
			return;
		}
		if (NetworkObserver != null)
		{
			NetworkObserver.Deinitialize(destroyed: true);
		}
		if (NetworkManager != null)
		{
			Deinitialize_Prediction(asServer: true);
			NetworkManager.ServerManager.Objects.NetworkObjectDestroyed(this, asServer: true);
			InvokeStopCallbacks(asServer: true, invokeSyncTypeCallbacks: true);
			Deinitialize_Prediction(asServer: false);
			NetworkManager.ClientManager.Objects.NetworkObjectDestroyed(this, asServer: false);
			InvokeStopCallbacks(asServer: false, invokeSyncTypeCallbacks: true);
		}
		if (Owner.IsValid)
		{
			Owner.RemoveObject(this);
		}
		Observers.Clear();
		if (NetworkBehaviours.Count > 0)
		{
			NetworkBehaviour item = NetworkBehaviours[0];
			if (RuntimeParentNetworkBehaviour != null && RuntimeParentNetworkBehaviour.NetworkObject.RuntimeChildNetworkBehaviours != null)
			{
				RuntimeParentNetworkBehaviour.NetworkObject.RuntimeChildNetworkBehaviours.Remove(item);
			}
		}
		IsDeinitializing = true;
		SetDeinitializedStatus();
		NetworkBehaviour_OnDestroy();
		ResetState(asServer: true);
		ResetState(asServer: false);
		StoreCollections();
		void NetworkBehaviour_OnDestroy()
		{
			foreach (NetworkBehaviour networkBehaviour in NetworkBehaviours)
			{
				networkBehaviour.NetworkBehaviour_OnDestroy();
			}
		}
	}

	private void InitializeNetworkBehavioursIfDisabled()
	{
		if (!_disabledNetworkBehavioursInitialized)
		{
			_disabledNetworkBehavioursInitialized = true;
			for (int i = 0; i < NetworkBehaviours.Count; i++)
			{
				NetworkBehaviours[i].InitializeIfDisabled();
			}
		}
	}

	internal List<NetworkObject> GetNetworkObjects(GetNetworkObjectOption option)
	{
		List<NetworkObject> list = CollectionCaches<NetworkObject>.RetrieveList();
		if (option.FastContains(GetNetworkObjectOption.Self))
		{
			list.Add(this);
		}
		bool flag = false;
		if (option.FastContains(GetNetworkObjectOption.RuntimeNested))
		{
			foreach (NetworkBehaviour runtimeChildNetworkBehaviour in RuntimeChildNetworkBehaviours)
			{
				list.Add(runtimeChildNetworkBehaviour.NetworkObject);
			}
			flag = true;
		}
		if (option.FastContains(GetNetworkObjectOption.InitializedNested))
		{
			list.AddRangeUnique(InitializedNestedNetworkObjects);
			flag = true;
		}
		if (flag && option.FastContains(GetNetworkObjectOption.Recursive))
		{
			option &= ~GetNetworkObjectOption.Self;
			int count = list.Count;
			for (int i = 0; i < count; i++)
			{
				List<NetworkObject> networkObjects = list[i].GetNetworkObjects(option);
				list.AddRangeUnique(networkObjects);
				CollectionCaches<NetworkObject>.Store(networkObjects);
			}
		}
		return list;
	}

	private void SetChildGlobalState()
	{
		if (IsGlobal)
		{
			for (int i = 0; i < InitializedNestedNetworkObjects.Count; i++)
			{
				InitializedNestedNetworkObjects[i].SetIsGlobal(value: true);
			}
		}
	}

	private void SetChildDespawnedState()
	{
		for (int i = 0; i < InitializedNestedNetworkObjects.Count; i++)
		{
			NetworkObject networkObject = InitializedNestedNetworkObjects[i];
			if (!networkObject.gameObject.activeSelf)
			{
				networkObject.State = NetworkObjectState.Despawned;
			}
		}
	}

	internal void TryStartDeactivation()
	{
		if (!GetIsNetworked())
		{
			return;
		}
		if (IsGlobal && !IsSceneObject && !IsNested)
		{
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		}
		if (NetworkManager == null || (!NetworkManager.IsClientStarted && !NetworkManager.IsServerStarted))
		{
			if (IsSceneObject)
			{
				WasActiveDuringEdit = true;
			}
			base.gameObject.SetActive(value: false);
		}
	}

	internal void SetInitializedStatus(bool isInitialized, bool asServer)
	{
		if (asServer)
		{
			IsServerInitialized = isInitialized;
		}
		else
		{
			IsClientInitialized = isInitialized;
		}
	}

	private void SetDeinitializedStatus()
	{
		IsServerInitialized = false;
		IsClientInitialized = false;
	}

	internal void InitializeEarly(NetworkManager networkManager, int objectId, NetworkConnection owner, bool asServer)
	{
		if (!networkManager.DoubleLogic(asServer))
		{
			State = NetworkObjectState.Spawned;
			InitializeNetworkBehavioursIfDisabled();
			IsDeinitializing = false;
			NetworkManager = networkManager;
			ServerManager = networkManager.ServerManager;
			ClientManager = networkManager.ClientManager;
			ObserverManager = networkManager.ObserverManager;
			TransportManager = networkManager.TransportManager;
			TimeManager = networkManager.TimeManager;
			SceneManager = networkManager.SceneManager;
			PredictionManager = networkManager.PredictionManager;
			RollbackManager = networkManager.RollbackManager;
			SetOwner(owner);
			if (ObjectId != 65535)
			{
				NetworkManager.LogError("Object was initialized twice without being reset. Object " + ToString());
			}
			ObjectId = objectId;
			AddDefaultNetworkObserverConditions();
		}
		for (int i = 0; i < NetworkBehaviours.Count; i++)
		{
			NetworkBehaviours[i].InitializeEarly(this, asServer);
		}
		if (asServer)
		{
			if (networkManager.TryGetInstance<HashGrid>(out _hashGrid))
			{
				_hashGridPosition = _hashGrid.GetHashGridPosition(this);
				HashGridEntry = _hashGrid.GetGridEntry(this);
			}
			NetworkObserver.Initialize(this);
		}
		_networkObserverInitiliazed = true;
		InitializePredictionEarly(networkManager, asServer);
		if (owner != null)
		{
			owner.AddObject(this);
		}
	}

	private void TimeManager_Update()
	{
		TimeManager_OnUpdate_Prediction();
	}

	public void SetParent(NetworkBehaviour nb)
	{
		if (CanChangeParent(logFailure: true) && !IsInvalidParent(nb))
		{
			UpdateParent(nb);
		}
	}

	public void SetParent(NetworkObject nob)
	{
		if (CanChangeParent(logFailure: true))
		{
			if (nob == null)
			{
				UnsetParent();
				return;
			}
			if (nob.NetworkBehaviours.Count == 0)
			{
				NetworkManager.LogWarning(nob.name + " is not a valid parent because it does not have any NetworkBehaviours. Consider adding EmptyNetworkBehaviour to " + nob.name + " to resolve this problem.");
				return;
			}
			NetworkBehaviour newParent = nob.NetworkBehaviours[0];
			UpdateParent(newParent);
		}
	}

	public void UnsetParent()
	{
		UpdateParent(null);
	}

	private void UpdateParent(NetworkBehaviour newParent)
	{
		if (NetworkBehaviours.Count == 0)
		{
			NetworkManager.LogWarning(base.gameObject.name + " cannot have it's parent updated because it does not have any NetworkBehaviours. Consider adding EmptyNetworkBehaviour to " + base.gameObject.name + " to resolve this problem.");
			return;
		}
		NetworkBehaviour item = NetworkBehaviours[0];
		if (RuntimeParentNetworkBehaviour != null)
		{
			RuntimeParentNetworkBehaviour.NetworkObject.RuntimeChildNetworkBehaviours.Remove(item);
		}
		if (newParent == null)
		{
			RuntimeParentNetworkBehaviour = null;
			base.transform.SetParent(null);
		}
		else
		{
			RuntimeParentNetworkBehaviour = newParent;
			newParent.NetworkObject.RuntimeChildNetworkBehaviours.Add(item);
			base.transform.SetParent(newParent.transform);
		}
		if (NetworkManager != null)
		{
			NetworkManager.ServerManager.Objects.RebuildObservers(this);
		}
	}

	private bool CanChangeParent(bool logFailure)
	{
		if (IsSceneObject)
		{
			return true;
		}
		if (InitializedParentNetworkBehaviour == null)
		{
			return true;
		}
		if (logFailure)
		{
			NetworkManager.LogWarning(ToString() + " cannot have it's parent changed because it's nested. Only nested scene objects may change their parent runtime.");
		}
		return false;
	}

	private bool IsInvalidParent(NetworkBehaviour nb)
	{
		if (IsSceneObject)
		{
			return false;
		}
		if (nb == RuntimeParentNetworkBehaviour)
		{
			return true;
		}
		if (nb.NetworkObject.IsGlobal && !IsGlobal)
		{
			NetworkManager.LogWarning(nb.NetworkObject.name + " is a global NetworkObject but " + base.gameObject.name + " is not. Only global NetworkObjects can be set as a child of another global NetworkObject.");
			return true;
		}
		if (nb.NetworkObject == this)
		{
			NetworkManager.LogWarning(base.gameObject.name + " cannot be set as a child of itself.");
			return true;
		}
		return false;
	}

	internal T AddAndSerialize<T>() where T : NetworkBehaviour
	{
		int count = NetworkBehaviours.Count;
		T val = base.gameObject.AddComponent<T>();
		NetworkBehaviours.Add(val);
		val.SerializeComponents(this, (byte)count);
		return val;
	}

	internal void SetInitializedValues(NetworkObject parentNob, bool force = false)
	{
		byte componentId = 0;
		SetInitializedValues(parentNob, ref componentId, force);
	}

	internal void SetInitializedValues(NetworkObject parentNob, ref byte componentId, bool force = false)
	{
		if (!ApplicationState.IsPlaying())
		{
			NetworkManager.LogError("Method SetInitializedValues should only be called at runtime.");
			return;
		}
		if (force || !_initializedValusSet)
		{
			StoreCollections();
			RetrieveCollections();
			_initializedValusSet = true;
		}
		SerializeTransformProperties();
		SetIsNestedThroughTraversal();
		if (componentId == 0)
		{
			if (IsNested)
			{
				return;
			}
			if (GetComponentsInChildren<NetworkObject>(includeInactive: true).Length > 254)
			{
				NetworkManagerExtensions.LogError($"The number of child NetworkObjects on {base.gameObject.name} exceeds the maximum of {(byte)254}.");
				return;
			}
		}
		NetworkBehaviours.Clear();
		if (TryGetComponent<PredictedSpawn>(out var component))
		{
			PredictedSpawn = component;
		}
		if (TryGetComponent<PredictedOwner>(out var component2))
		{
			PredictedOwner = component2;
		}
		ComponentIndex = componentId;
		if (parentNob != null)
		{
			AddEmptyNetworkBehaviour(parentNob, base.transform.parent, addToNetworkBehaviours: true);
			if (!base.transform.parent.TryGetComponent<NetworkBehaviour>(out var component3))
			{
				NetworkManagerExtensions.LogError("A NetworkBehaviour is expected to exist on " + parentNob.name + " but does not.");
			}
			else
			{
				InitializedParentNetworkBehaviour = component3;
			}
		}
		List<Transform> list = CollectionCaches<Transform>.RetrieveList();
		InitializedNestedNetworkObjects.Clear();
		list.Add(base.transform);
		for (int i = 0; i < list.Count; i++)
		{
			Transform transform = list[i];
			for (int j = 0; j < transform.childCount; j++)
			{
				Transform child = transform.GetChild(j);
				if (child.TryGetComponent<NetworkObject>(out var component4))
				{
					if (IsSceneObject == component4.IsSceneObject)
					{
						InitializedNestedNetworkObjects.Add(component4);
					}
				}
				else
				{
					list.Add(child);
				}
			}
		}
		List<NetworkBehaviour> list2 = CollectionCaches<NetworkBehaviour>.RetrieveList();
		List<NetworkBehaviour> results = CollectionCaches<NetworkBehaviour>.RetrieveList();
		for (int k = 0; k < list.Count; k++)
		{
			results.Clear();
			list[k].GetNetworkBehavioursNonAlloc(ref results);
			list2.AddRange(results);
		}
		if (list2.Count == 0)
		{
			NetworkBehaviour networkBehaviour = AddEmptyNetworkBehaviour(this, base.transform, addToNetworkBehaviours: false);
			if (networkBehaviour != null)
			{
				list2.Add(networkBehaviour);
			}
		}
		int count = list2.Count;
		for (int l = 0; l < count; l++)
		{
			NetworkBehaviour networkBehaviour2 = list2[l];
			NetworkBehaviours.Add(networkBehaviour2);
			networkBehaviour2.SerializeComponents(this, (byte)l);
		}
		CollectionCaches<Transform>.Store(list);
		CollectionCaches<NetworkBehaviour>.Store(list2);
		CollectionCaches<NetworkBehaviour>.Store(results);
		foreach (NetworkObject initializedNestedNetworkObject in InitializedNestedNetworkObjects)
		{
			componentId++;
			initializedNestedNetworkObject.SetInitializedValues(this, ref componentId, force);
		}
		SetChildGlobalState();
	}

	private NetworkBehaviour AddEmptyNetworkBehaviour(NetworkObject nob, Transform target, bool addToNetworkBehaviours)
	{
		if (!target.TryGetComponent<NetworkBehaviour>(out var component))
		{
			if (nob.NetworkBehaviours.Count == 254)
			{
				NetworkManager.LogError(string.Format("NetworkObject {0} already has a maximum of {1}. {2} cannot be added. Nested spawning will likely fail for this object.", ToString(), (byte)254, "EmptyNetworkBehaviour"));
				return null;
			}
			component = target.gameObject.AddComponent<EmptyNetworkBehaviour>();
			if (addToNetworkBehaviours)
			{
				nob.NetworkBehaviours.Add(component);
				component.SerializeComponents(nob, (byte)(nob.NetworkBehaviours.Count - 1));
			}
		}
		return component;
	}

	internal void Initialize(bool asServer, bool invokeSyncTypeCallbacks)
	{
		SetInitializedStatus(isInitialized: true, asServer);
		InvokeStartCallbacks(asServer, invokeSyncTypeCallbacks);
	}

	internal bool CanDeinitialize(bool asServer)
	{
		if (NetworkManager == null)
		{
			return false;
		}
		if (asServer && !IsServerInitialized)
		{
			return false;
		}
		if (!asServer && !IsClientInitialized)
		{
			return false;
		}
		return true;
	}

	internal void Deinitialize(bool asServer)
	{
		if (!CanDeinitialize(asServer))
		{
			return;
		}
		Deinitialize_Prediction(asServer);
		InvokeStopCallbacks(asServer, invokeSyncTypeCallbacks: true);
		for (int i = 0; i < NetworkBehaviours.Count; i++)
		{
			NetworkBehaviours[i].Deinitialize(asServer);
		}
		bool flag = asServer && !IsClientInitialized;
		if (asServer)
		{
			if (NetworkObserver != null)
			{
				NetworkObserver.Deinitialize(destroyed: false);
			}
			IsDeinitializing = true;
		}
		else
		{
			if (!NetworkManager.IsServerStarted)
			{
				IsDeinitializing = true;
			}
			RemoveClientRpcLinkIndexes();
		}
		if (!asServer || flag)
		{
			PredictedSpawner = NetworkManager.EmptyConnection;
		}
		SetInitializedStatus(isInitialized: false, asServer);
		if (asServer)
		{
			Observers.Clear();
		}
	}

	public void ResetState(bool asServer)
	{
		int count = NetworkBehaviours.Count;
		for (int i = 0; i < count; i++)
		{
			NetworkBehaviours[i].ResetState(asServer);
		}
		ResetState_Prediction(asServer);
		ResetState_Observers(asServer);
		if (!IsNested || State == NetworkObjectState.Despawned)
		{
			State = NetworkObjectState.Unset;
		}
		SetOwner(NetworkManager.EmptyConnection);
		if (NetworkObserver != null)
		{
			NetworkObserver.Deinitialize(destroyed: false);
		}
		ObjectId = 65535;
	}

	public void RemoveOwnership(bool includeNested = false)
	{
		GiveOwnership(null, asServer: true, includeNested);
	}

	public void GiveOwnership(NetworkConnection newOwner)
	{
		GiveOwnership(newOwner, asServer: true, recursive: false);
	}

	public void GiveOwnership(NetworkConnection newOwner, bool asServer)
	{
		GiveOwnership(newOwner, asServer, recursive: false);
	}

	internal void GiveOwnership(NetworkConnection newOwner, bool asServer, bool recursive = false)
	{
		if (asServer)
		{
			if (!NetworkManager.IsServerStarted)
			{
				NetworkManager.LogWarning("Ownership cannot be given for object " + base.gameObject.name + ". Only server may give ownership.");
				return;
			}
			if (newOwner == Owner)
			{
				return;
			}
			if (newOwner != null && newOwner.IsActive && !newOwner.LoadedStartScenes(asServer: true))
			{
				NetworkManager.LogWarning($"Ownership has been transfered to ConnectionId {newOwner.ClientId} but this is not recommended until after they have loaded start scenes. You can be notified when a connection loads start scenes by using connection.OnLoadedStartScenes on the connection, or SceneManager.OnClientLoadStartScenes.");
			}
		}
		bool flag = newOwner != null && newOwner.IsActive;
		NetworkConnection networkConnection = Owner;
		if (networkConnection == null)
		{
			networkConnection = NetworkManager.EmptyConnection;
		}
		SetOwner(newOwner);
		if (asServer || !NetworkManager.IsHostStarted)
		{
			if (flag)
			{
				newOwner.AddObject(this);
			}
			if (networkConnection != newOwner)
			{
				networkConnection.RemoveObject(this);
			}
		}
		InvokeManualOwnershipChange(networkConnection, asServer);
		if (asServer)
		{
			if (flag)
			{
				ServerManager.Objects.RebuildObservers(this, newOwner);
			}
			PooledWriter pooledWriter = WriterPool.Retrieve();
			pooledWriter.WritePacketIdUnpacked(PacketId.OwnershipChange);
			pooledWriter.WriteNetworkObject(this);
			pooledWriter.WriteNetworkConnection(Owner);
			if (NetworkManager.ServerManager.ShareIds)
			{
				NetworkManager.TransportManager.SendToClients(0, pooledWriter.GetArraySegment(), this);
			}
			else
			{
				if (networkConnection.IsActive)
				{
					NetworkManager.TransportManager.SendToClient(0, pooledWriter.GetArraySegment(), networkConnection);
				}
				if (flag)
				{
					NetworkManager.TransportManager.SendToClient(0, pooledWriter.GetArraySegment(), newOwner);
				}
			}
			pooledWriter.Store();
			if (networkConnection.IsActive)
			{
				ServerManager.Objects.RebuildObservers(networkConnection);
			}
		}
		if (!recursive)
		{
			return;
		}
		List<NetworkObject> networkObjects = GetNetworkObjects(GetNetworkObjectOption.AllNestedRecursive);
		foreach (NetworkObject item in networkObjects)
		{
			item.GiveOwnership(newOwner, asServer, recursive: true);
		}
		CollectionCaches<NetworkObject>.Store(networkObjects);
	}

	internal void InitializePredictedObject_Server(NetworkConnection predictedSpawner)
	{
		PredictedSpawner = predictedSpawner;
	}

	internal void InitializePredictedObject_Client(NetworkManager manager, int objectId, NetworkConnection owner, NetworkConnection predictedSpawner)
	{
		PredictedSpawner = predictedSpawner;
		InitializeEarly(manager, objectId, owner, asServer: false);
	}

	private void SetOwner(NetworkConnection owner)
	{
		Owner = owner;
	}

	internal TransformPropertiesFlag GetTransformChanges(TransformProperties stp)
	{
		Transform t = base.transform;
		return GetTransformChanges(t, stp.Position, stp.Rotation, stp.Scale);
	}

	internal TransformPropertiesFlag GetTransformChanges(GameObject prefab)
	{
		Transform transform = prefab.transform;
		return GetTransformChanges(base.transform, transform.localPosition, transform.localRotation, transform.localScale);
	}

	private TransformPropertiesFlag GetTransformChanges(Transform t, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
	{
		TransformPropertiesFlag transformPropertiesFlag = TransformPropertiesFlag.Unset;
		if (t.localPosition != localPosition)
		{
			transformPropertiesFlag |= TransformPropertiesFlag.Position;
		}
		if (t.localRotation != localRotation)
		{
			transformPropertiesFlag |= TransformPropertiesFlag.Rotation;
		}
		if (t.localScale != localScale)
		{
			transformPropertiesFlag |= TransformPropertiesFlag.Scale;
		}
		return transformPropertiesFlag;
	}

	internal bool SetIsNestedThroughTraversal()
	{
		Transform parent = base.transform.parent;
		while (parent != null && parent != base.transform)
		{
			if (parent.TryGetComponent<NetworkObject>(out var _))
			{
				IsNested = true;
				return IsNested;
			}
			parent = parent.parent;
		}
		IsNested = false;
		return IsNested;
	}

	internal void SerializeTransformProperties()
	{
		SerializedTransformProperties = new TransformProperties(base.transform.localPosition, base.transform.localRotation, base.transform.localScale);
	}

	private void StoreCollections()
	{
		CollectionCaches<NetworkBehaviour>.StoreAndDefault(ref NetworkBehaviours);
		CollectionCaches<NetworkObject>.StoreAndDefault(ref InitializedNestedNetworkObjects);
		CollectionCaches<NetworkBehaviour>.StoreAndDefault(ref RuntimeChildNetworkBehaviours);
	}

	private void RetrieveCollections()
	{
		NetworkBehaviours = CollectionCaches<NetworkBehaviour>.RetrieveList();
		InitializedNestedNetworkObjects = CollectionCaches<NetworkObject>.RetrieveList();
		RuntimeChildNetworkBehaviours = CollectionCaches<NetworkBehaviour>.RetrieveList();
	}
}
