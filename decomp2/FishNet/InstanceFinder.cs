using System;
using System.Linq;
using FishNet.Component.ColliderRollback;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Predicting;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
using FishNet.Managing.Statistic;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet;

public static class InstanceFinder
{
	private static NetworkManager _networkManager;

	public static NetworkManager NetworkManager
	{
		get
		{
			if (_networkManager == null)
			{
				int count = NetworkManager.Instances.Count;
				if (count > 0)
				{
					_networkManager = NetworkManager.Instances.First();
					if (count > 1)
					{
						_networkManager.LogWarning("Multiple NetworkManagers found, the first result will be returned. If you only wish to have one NetworkManager then uncheck 'Allow Multiple' within your NetworkManagers.");
					}
				}
				else
				{
					if (ApplicationState.IsQuitting())
					{
						return null;
					}
					Debug.Log("NetworkManager not found in any open scenes.");
				}
			}
			return _networkManager;
		}
	}

	public static ServerManager ServerManager
	{
		get
		{
			NetworkManager networkManager = NetworkManager;
			if (!(networkManager == null))
			{
				return networkManager.ServerManager;
			}
			return null;
		}
	}

	public static ClientManager ClientManager
	{
		get
		{
			NetworkManager networkManager = NetworkManager;
			if (!(networkManager == null))
			{
				return networkManager.ClientManager;
			}
			return null;
		}
	}

	public static TransportManager TransportManager
	{
		get
		{
			NetworkManager networkManager = NetworkManager;
			if (!(networkManager == null))
			{
				return networkManager.TransportManager;
			}
			return null;
		}
	}

	public static TimeManager TimeManager
	{
		get
		{
			NetworkManager networkManager = NetworkManager;
			if (!(networkManager == null))
			{
				return networkManager.TimeManager;
			}
			return null;
		}
	}

	public static SceneManager SceneManager
	{
		get
		{
			NetworkManager networkManager = NetworkManager;
			if (!(networkManager == null))
			{
				return networkManager.SceneManager;
			}
			return null;
		}
	}

	public static RollbackManager RollbackManager
	{
		get
		{
			NetworkManager networkManager = NetworkManager;
			if (!(networkManager == null))
			{
				return networkManager.RollbackManager;
			}
			return null;
		}
	}

	public static PredictionManager PredictionManager
	{
		get
		{
			NetworkManager networkManager = NetworkManager;
			if (!(networkManager == null))
			{
				return networkManager.PredictionManager;
			}
			return null;
		}
	}

	public static StatisticsManager StatisticsManager
	{
		get
		{
			NetworkManager networkManager = NetworkManager;
			if (!(networkManager == null))
			{
				return networkManager.StatisticsManager;
			}
			return null;
		}
	}

	[Obsolete("Use IsClientOnlyStarted. Note the difference between IsClientOnlyInitialized and IsClientOnlyStarted.")]
	public static bool IsClientOnly => IsClientOnlyStarted;

	[Obsolete("Use IsServerOnlyStarted. Note the difference between IsServerOnlyInitialized and IsServerOnlyStarted.")]
	public static bool IsServerOnly => IsServerOnlyStarted;

	[Obsolete("Use IsHostStarted. Note the difference between IsHostInitialized and IsHostStarted.")]
	public static bool IsHost => IsHostStarted;

	[Obsolete("Use IsClientStarted. Note the difference between IsClientInitialized and IsClientStarted.")]
	public static bool IsClient => IsClientStarted;

	[Obsolete("Use IsServerStarted. Note the difference between IsServerInitialized and IsServerStarted.")]
	public static bool IsServer => IsServerStarted;

	public static bool IsServerStarted
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

	public static bool IsServerOnlyStarted
	{
		get
		{
			if (!(NetworkManager == null))
			{
				return NetworkManager.IsServerOnlyStarted;
			}
			return false;
		}
	}

	public static bool IsClientStarted
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

	public static bool IsClientOnlyStarted
	{
		get
		{
			if (!(NetworkManager == null))
			{
				return NetworkManager.IsClientOnlyStarted;
			}
			return false;
		}
	}

	public static bool IsHostStarted
	{
		get
		{
			if (!(NetworkManager == null))
			{
				return NetworkManager.IsHostStarted;
			}
			return false;
		}
	}

	public static bool IsOffline
	{
		get
		{
			if (!(NetworkManager == null))
			{
				return NetworkManager.IsOffline;
			}
			return true;
		}
	}

	public static void RegisterInvokeOnInstance<T>(Action<UnityEngine.Component> handler) where T : UnityEngine.Component
	{
		if (NetworkManager != null)
		{
			NetworkManager.RegisterInvokeOnInstance<T>(handler);
		}
	}

	public static void UnregisterInvokeOnInstance<T>(Action<UnityEngine.Component> handler) where T : UnityEngine.Component
	{
		if (NetworkManager != null)
		{
			NetworkManager.UnregisterInvokeOnInstance<T>(handler);
		}
	}

	public static T GetInstance<T>() where T : UnityEngine.Component
	{
		if (!(NetworkManager == null))
		{
			return NetworkManager.GetInstance<T>();
		}
		return null;
	}

	public static bool HasInstance<T>() where T : UnityEngine.Component
	{
		if (!(NetworkManager == null))
		{
			return NetworkManager.HasInstance<T>();
		}
		return false;
	}

	public static void RegisterInstance<T>(T component, bool replace = true) where T : UnityEngine.Component
	{
		if (NetworkManager != null)
		{
			NetworkManager.RegisterInstance(component, replace);
		}
	}

	public static bool TryRegisterInstance<T>(T component) where T : UnityEngine.Component
	{
		if (!(NetworkManager == null))
		{
			return NetworkManager.TryRegisterInstance(component);
		}
		return false;
	}

	public static bool TryGetInstance<T>(out T component) where T : UnityEngine.Component
	{
		if (NetworkManager == null)
		{
			component = null;
			return false;
		}
		return NetworkManager.TryGetInstance<T>(out component);
	}

	public static void UnregisterInstance<T>() where T : UnityEngine.Component
	{
		if (NetworkManager != null)
		{
			NetworkManager.UnregisterInstance<T>();
		}
	}
}
