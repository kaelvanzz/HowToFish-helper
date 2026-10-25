using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Observing;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[AddComponentMenu("FishNet/Component/NetworkObserver")]
public sealed class NetworkObserver : MonoBehaviour
{
	public enum ConditionOverrideType
	{
		AddMissing = 1,
		UseManager,
		IgnoreManager
	}

	internal bool ConditionsSetByObserverManager;

	[Tooltip("How ObserverManager conditions are used.")]
	[SerializeField]
	private ConditionOverrideType _overrideType = ConditionOverrideType.IgnoreManager;

	[Tooltip("True to update visibility for clientHost based on if they are an observer or not.")]
	[SerializeField]
	private bool _updateHostVisibility = true;

	[Tooltip("Conditions connections must met to be added as an observer. Multiple conditions may be used.")]
	[SerializeField]
	internal List<ObserverCondition> _observerConditions = new List<ObserverCondition>();

	private List<ObserverCondition> _timedConditions;

	private HashSet<NetworkConnection> _nonTimedMet;

	private NetworkObject _networkObject;

	private bool _registeredAsTimed;

	private bool _conditionsInitializedPreviously;

	private bool _initialized;

	private bool _lastParentVisible;

	private ServerManager _serverManager;

	private bool _hasNormalConditions;

	public ConditionOverrideType OverrideType
	{
		get
		{
			return _overrideType;
		}
		internal set
		{
			_overrideType = value;
		}
	}

	public bool UpdateHostVisibility
	{
		get
		{
			return _updateHostVisibility;
		}
		private set
		{
			_updateHostVisibility = value;
		}
	}

	public IReadOnlyList<ObserverCondition> ObserverConditions => _observerConditions;

	[APIExclude]
	internal List<ObserverCondition> ObserverConditionsInternal
	{
		get
		{
			return _observerConditions;
		}
		set
		{
			_observerConditions = value;
		}
	}

	internal void Deinitialize(bool destroyed)
	{
		_lastParentVisible = false;
		if (_nonTimedMet != null)
		{
			_nonTimedMet.Clear();
		}
		UnregisterTimedConditions();
		if (_serverManager != null)
		{
			_serverManager.OnRemoteConnectionState -= ServerManager_OnRemoteConnectionState;
		}
		if (_conditionsInitializedPreviously)
		{
			_hasNormalConditions = false;
			foreach (ObserverCondition observerCondition in _observerConditions)
			{
				observerCondition.Deinitialize(destroyed);
				if (destroyed && observerCondition.GetInstanceID() < 0)
				{
					UnityEngine.Object.Destroy(observerCondition);
				}
			}
			if (destroyed)
			{
				_observerConditions.Clear();
				CollectionCaches<ObserverCondition>.Store(_timedConditions);
				CollectionCaches<NetworkConnection>.Store(_nonTimedMet);
			}
		}
		_serverManager = null;
		_networkObject = null;
		_initialized = false;
	}

	internal void Initialize(NetworkObject networkObject)
	{
		if (_initialized)
		{
			return;
		}
		_networkObject = networkObject;
		_serverManager = _networkObject.ServerManager;
		_serverManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
		bool flag = _conditionsInitializedPreviously;
		if (!_conditionsInitializedPreviously)
		{
			_conditionsInitializedPreviously = true;
			if (OverrideType != ConditionOverrideType.IgnoreManager)
			{
				UpdateHostVisibility = networkObject.ObserverManager.UpdateHostVisibility;
			}
			_nonTimedMet = CollectionCaches<NetworkConnection>.RetrieveHashSet();
			List<ObserverCondition> list = CollectionCaches<ObserverCondition>.RetrieveList();
			List<ObserverCondition> list2 = CollectionCaches<ObserverCondition>.RetrieveList();
			foreach (ObserverCondition observerCondition2 in _observerConditions)
			{
				if (!(observerCondition2 == null))
				{
					flag = true;
					ObserverCondition observerCondition = UnityEngine.Object.Instantiate(observerCondition2);
					if (observerCondition.GetConditionType() == ObserverConditionType.Timed)
					{
						list2.AddOrdered(observerCondition);
						continue;
					}
					_hasNormalConditions = true;
					list.AddOrdered(observerCondition);
				}
			}
			_observerConditions.Clear();
			for (int i = 0; i < list.Count; i++)
			{
				_observerConditions.Add(list[i]);
			}
			_timedConditions = CollectionCaches<ObserverCondition>.RetrieveList();
			foreach (ObserverCondition item in list2)
			{
				_observerConditions.Add(item);
				_timedConditions.Add(item);
			}
			CollectionCaches<ObserverCondition>.Store(list);
			CollectionCaches<ObserverCondition>.Store(list2);
		}
		if (flag)
		{
			for (int j = 0; j < _observerConditions.Count; j++)
			{
				_observerConditions[j].Initialize(_networkObject);
			}
			RegisterTimedConditions();
		}
		_initialized = true;
	}

	public ObserverCondition GetObserverCondition<T>() where T : ObserverCondition
	{
		Type typeFromHandle = typeof(T);
		for (int i = 0; i < _observerConditions.Count; i++)
		{
			if (_observerConditions[i].GetType() == typeFromHandle)
			{
				return _observerConditions[i];
			}
		}
		return null;
	}

	internal ObserverStateChange RebuildObservers(NetworkConnection connection, bool timedOnly)
	{
		if (!_initialized)
		{
			string text = ((base.gameObject == null) ? "Empty" : base.gameObject.name);
			NetworkManagerExtensions.LogError(GetType().Name + " is not initialized on NetworkObject [" + text + "]. RebuildObservers should not be called. If you are able to reproduce this error consistently please report this issue.");
			return ObserverStateChange.Unchanged;
		}
		bool flag = _networkObject.Observers.Contains(connection);
		bool flag2 = true;
		if (connection != _networkObject.Owner)
		{
			bool flag3 = true;
			if (_networkObject.CurrentParentNetworkBehaviour != null)
			{
				flag3 = _networkObject.CurrentParentNetworkBehaviour.NetworkObject.Observers.Contains(connection);
			}
			if (flag3 && !_lastParentVisible)
			{
				timedOnly = false;
			}
			_lastParentVisible = flag3;
			if (!flag3)
			{
				flag2 = false;
			}
			else if (_observerConditions.Count > 0)
			{
				bool flag4 = !_hasNormalConditions || _nonTimedMet.Contains(connection);
				if (timedOnly && !flag4)
				{
					flag2 = false;
				}
				else
				{
					bool flag5 = true;
					List<ObserverCondition> list = (timedOnly ? _timedConditions : _observerConditions);
					for (int i = 0; i < list.Count; i++)
					{
						ObserverCondition observerCondition = list[i];
						bool notProcessed = false;
						bool flag6 = !observerCondition.GetIsEnabled() || observerCondition.ConditionMet(connection, flag, out notProcessed);
						if (notProcessed)
						{
							flag6 = flag;
						}
						if (!flag6)
						{
							flag2 = false;
							if (observerCondition.GetConditionType() != ObserverConditionType.Timed)
							{
								flag5 = false;
							}
							break;
						}
					}
					if (flag4 != flag5)
					{
						if (flag5)
						{
							_nonTimedMet.Add(connection);
						}
						else
						{
							_nonTimedMet.Remove(connection);
						}
					}
				}
			}
		}
		if (flag2)
		{
			return ReturnPassedConditions(flag);
		}
		return ReturnFailedCondition(flag);
	}

	private void RegisterTimedConditions()
	{
		if (_timedConditions != null && _timedConditions.Count != 0 && !_registeredAsTimed)
		{
			_registeredAsTimed = true;
			if (!(_serverManager == null))
			{
				_serverManager.Objects.AddTimedNetworkObserver(_networkObject);
			}
		}
	}

	private void UnregisterTimedConditions()
	{
		if (_timedConditions != null && _timedConditions.Count != 0 && _registeredAsTimed)
		{
			_registeredAsTimed = false;
			if (!(_serverManager == null))
			{
				_serverManager.Objects.RemoveTimedNetworkObserver(_networkObject);
			}
		}
	}

	private ObserverStateChange ReturnFailedCondition(bool currentlyAdded)
	{
		if (currentlyAdded)
		{
			return ObserverStateChange.Removed;
		}
		return ObserverStateChange.Unchanged;
	}

	private ObserverStateChange ReturnPassedConditions(bool currentlyAdded)
	{
		if (currentlyAdded)
		{
			return ObserverStateChange.Unchanged;
		}
		return ObserverStateChange.Added;
	}

	private void ServerManager_OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs arg2)
	{
		if (arg2.ConnectionState == RemoteConnectionState.Stopped)
		{
			_nonTimedMet.Remove(conn);
		}
	}

	public void SetUpdateHostVisibility(bool value)
	{
		if (value != UpdateHostVisibility)
		{
			UpdateHostVisibility = value;
		}
	}
}
