using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Observing;
using UnityEngine;

namespace FishNet.Managing.Observing;

[DisallowMultipleComponent]
[AddComponentMenu("FishNet/Manager/ObserverManager")]
public sealed class ObserverManager : MonoBehaviour
{
	[Tooltip("True to update visibility for clientHost based on if they are an observer or not.")]
	[SerializeField]
	private bool _updateHostVisibility = true;

	[Tooltip("Maximum duration the server will take to update timed observer conditions as server load increases. Lower values will result in timed conditions being checked quicker at the cost of performance.")]
	[SerializeField]
	[Range(0.1f, 20f)]
	private float _maximumTimedObserversDuration = 10f;

	[Tooltip("Default observer conditions for networked objects.")]
	[SerializeField]
	private List<ObserverCondition> _defaultConditions = new List<ObserverCondition>();

	private NetworkManager _networkManager;

	private const float MINIMUM_TIMED_OBSERVERS_DURATION = 0.1f;

	private const float MAXIMUM_TIMED_OBSERVERS_DURATION = 20f;

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

	public float MaximumTimedObserversDuration
	{
		get
		{
			return _maximumTimedObserversDuration;
		}
		private set
		{
			_maximumTimedObserversDuration = value;
		}
	}

	public void SetMaximumTimedObserversDuration(float value)
	{
		MaximumTimedObserversDuration = Math.Clamp(value, 0.1f, 20f);
	}

	internal void InitializeOnce_Internal(NetworkManager manager)
	{
		_networkManager = manager;
		SetMaximumTimedObserversDuration(MaximumTimedObserversDuration);
	}

	public void SetUpdateHostVisibility(bool value, HostVisibilityUpdateTypes updateType)
	{
		if (value == UpdateHostVisibility)
		{
			return;
		}
		if (HostVisibilityUpdateContains(updateType, HostVisibilityUpdateTypes.Manager))
		{
			UpdateHostVisibility = value;
		}
		if (!_networkManager.IsServerStarted || !HostVisibilityUpdateContains(updateType, HostVisibilityUpdateTypes.Spawned))
		{
			return;
		}
		NetworkConnection connection = _networkManager.ClientManager.Connection;
		foreach (NetworkObject value2 in _networkManager.ServerManager.Objects.Spawned.Values)
		{
			value2.NetworkObserver.SetUpdateHostVisibility(value);
			if (connection.IsActive)
			{
				value2.SetRenderersVisible(value2.Observers.Contains(connection), force: true);
			}
		}
		static bool HostVisibilityUpdateContains(HostVisibilityUpdateTypes whole, HostVisibilityUpdateTypes part)
		{
			return (whole & part) == part;
		}
	}

	internal NetworkObserver AddDefaultConditions(NetworkObject nob)
	{
		bool flag = nob.IsGlobal && !nob.IsSceneObject;
		bool flag2;
		if (!nob.TryGetComponent<NetworkObserver>(out var component))
		{
			flag2 = true;
			component = nob.gameObject.AddComponent<NetworkObserver>();
		}
		else
		{
			if (component.ConditionsSetByObserverManager)
			{
				return component;
			}
			flag2 = false;
		}
		if (!flag2 && _defaultConditions.Count == 0)
		{
			return component;
		}
		if (flag2)
		{
			if (flag)
			{
				return component;
			}
			if (_defaultConditions.Count == 0)
			{
				return component;
			}
			component.OverrideType = NetworkObserver.ConditionOverrideType.UseManager;
		}
		else if (flag)
		{
			component.ObserverConditionsInternal.Clear();
			component.OverrideType = NetworkObserver.ConditionOverrideType.IgnoreManager;
		}
		if (component.OverrideType != NetworkObserver.ConditionOverrideType.IgnoreManager)
		{
			if (component.OverrideType == NetworkObserver.ConditionOverrideType.UseManager)
			{
				component.ObserverConditionsInternal.Clear();
				AddMissing(component);
			}
			else if (component.OverrideType == NetworkObserver.ConditionOverrideType.AddMissing)
			{
				AddMissing(component);
			}
		}
		component.ConditionsSetByObserverManager = true;
		return component;
		void AddMissing(NetworkObserver networkObserver)
		{
			int count = _defaultConditions.Count;
			for (int i = 0; i < count; i++)
			{
				ObserverCondition item = _defaultConditions[i];
				if (!networkObserver.ObserverConditionsInternal.Contains(item))
				{
					networkObserver.ObserverConditionsInternal.Add(item);
				}
			}
		}
	}
}
