using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace FishNet.Component.Spawning;

[AddComponentMenu("FishNet/Component/ServerSpawner")]
public class ServerSpawner : MonoBehaviour
{
	[Tooltip("True to spawn the objects as soon as the server starts. False if you wish to call Spawn manually.")]
	[SerializeField]
	private bool _automaticallySpawn = true;

	[Tooltip("NetworkObjects to spawn when the server starts.")]
	[SerializeField]
	private List<NetworkObject> _networkObjects = new List<NetworkObject>();

	private ServerManager _serverManager;

	private void Awake()
	{
		InitializeOnce();
	}

	private void OnDestroy()
	{
		if (!(_serverManager == null))
		{
			_serverManager.OnServerConnectionState -= ServerManager_OnServerConnectionState;
		}
	}

	private void InitializeOnce()
	{
		_serverManager = GetComponentInParent<ServerManager>();
		if (_serverManager == null)
		{
			_serverManager = InstanceFinder.ServerManager;
		}
		if (_serverManager == null)
		{
			NetworkManagerExtensions.LogWarning("ServerSpawner on " + base.gameObject.name + " cannot work as NetworkManager wasn't found on this object or within parent objects.");
		}
		else if (_automaticallySpawn)
		{
			_serverManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
		}
	}

	private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs args)
	{
		if (args.ConnectionState == LocalConnectionState.Started && _serverManager.IsOnlyOneServerStarted())
		{
			Spawn_Internally();
		}
	}

	private void Spawn_Internally()
	{
		if (_serverManager == null)
		{
			return;
		}
		foreach (NetworkObject networkObject in _networkObjects)
		{
			NetworkObject pooledInstantiated = _serverManager.NetworkManager.GetPooledInstantiated(networkObject, asServer: true);
			_serverManager.Spawn(pooledInstantiated);
		}
	}

	public void Spawn()
	{
		Spawn_Internally();
	}
}
