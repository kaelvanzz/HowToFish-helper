using System.IO;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using GameKit.Dependencies.Utilities.Types;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Component.Scenes;

[AddComponentMenu("FishNet/Component/DefaultScene")]
public class DefaultScene : MonoBehaviour
{
	[Tooltip("True to load the online scene as global, false to load it as connection.")]
	[SerializeField]
	private bool _enableGlobalScenes = true;

	[Tooltip("True to replace all scenes with the offline scene immediately.")]
	[SerializeField]
	private bool _startInOffline;

	[Tooltip("Scene to load when disconnected. Server and client will load this scene.")]
	[SerializeField]
	[Scene]
	private string _offlineScene;

	[Tooltip("Scene to load when connected. Server and client will load this scene.")]
	[SerializeField]
	[Scene]
	private string _onlineScene;

	[Tooltip("Which scenes to replace when loading into OnlineScene.")]
	[SerializeField]
	private ReplaceOption _replaceScenes;

	private NetworkManager _networkManager;

	public void SetOfflineScene(string sceneName)
	{
		_offlineScene = sceneName;
	}

	public string GetOfflineScene()
	{
		return _offlineScene;
	}

	public void SetOnlineScene(string sceneName)
	{
		_onlineScene = sceneName;
	}

	public string GetOnlineScene()
	{
		return _onlineScene;
	}

	private void OnEnable()
	{
		Initialize();
	}

	private void OnDestroy()
	{
		Deinitialize();
	}

	private void Initialize()
	{
		_networkManager = GetComponentInParent<NetworkManager>();
		if (_networkManager == null)
		{
			NetworkManagerExtensions.LogError("NetworkManager not found on " + base.gameObject.name + " or any parent objects. DefaultScene will not work.");
		}
		else
		{
			if (!_networkManager.Initialized)
			{
				return;
			}
			if (_onlineScene == string.Empty || _offlineScene == string.Empty)
			{
				NetworkManagerExtensions.LogWarning("Online or Offline scene is not specified. Default scenes will not load.");
				return;
			}
			_networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
			_networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
			_networkManager.SceneManager.OnLoadEnd += SceneManager_OnLoadEnd;
			_networkManager.ServerManager.OnAuthenticationResult += ServerManager_OnAuthenticationResult;
			if (_startInOffline)
			{
				LoadOfflineScene();
			}
		}
	}

	private void Deinitialize()
	{
		if (!ApplicationState.IsQuitting() && _networkManager != null && _networkManager.Initialized)
		{
			_networkManager.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
			_networkManager.ServerManager.OnServerConnectionState -= ServerManager_OnServerConnectionState;
			_networkManager.SceneManager.OnLoadEnd -= SceneManager_OnLoadEnd;
			_networkManager.ServerManager.OnAuthenticationResult -= ServerManager_OnAuthenticationResult;
		}
	}

	private void SceneManager_OnLoadEnd(SceneLoadEndEventArgs obj)
	{
		bool flag = false;
		Scene[] loadedScenes = obj.LoadedScenes;
		foreach (Scene scene in loadedScenes)
		{
			if (scene.name == GetSceneName(_onlineScene))
			{
				flag = true;
				break;
			}
		}
		if (flag)
		{
			UnloadOfflineScene();
		}
	}

	private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
	{
		if (obj.ConnectionState == LocalConnectionState.Started)
		{
			if (_networkManager.ServerManager.IsOnlyOneServerStarted())
			{
				SceneLoadData sceneLoadData = new SceneLoadData(GetSceneName(_onlineScene));
				sceneLoadData.ReplaceScenes = _replaceScenes;
				if (_enableGlobalScenes)
				{
					_networkManager.SceneManager.LoadGlobalScenes(sceneLoadData);
				}
				else
				{
					_networkManager.SceneManager.LoadConnectionScenes(sceneLoadData);
				}
			}
		}
		else if (obj.ConnectionState == LocalConnectionState.Stopped && !_networkManager.ServerManager.IsAnyServerStarted())
		{
			LoadOfflineScene();
		}
	}

	private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
	{
		if (obj.ConnectionState == LocalConnectionState.Stopped && !_networkManager.IsServerStarted)
		{
			LoadOfflineScene();
		}
	}

	private void ServerManager_OnAuthenticationResult(NetworkConnection arg1, bool authenticated)
	{
		if (!_enableGlobalScenes && authenticated)
		{
			SceneLoadData sceneLoadData = new SceneLoadData(GetSceneName(_onlineScene));
			_networkManager.SceneManager.LoadConnectionScenes(arg1, sceneLoadData);
		}
	}

	private void LoadOfflineScene()
	{
		if (!(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GetSceneName(_offlineScene)))
		{
			UnityEngine.SceneManagement.SceneManager.LoadScene(_offlineScene);
		}
	}

	private void UnloadOfflineScene()
	{
		Scene sceneByName = UnityEngine.SceneManagement.SceneManager.GetSceneByName(GetSceneName(_offlineScene));
		if (!string.IsNullOrEmpty(sceneByName.name))
		{
			UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sceneByName);
		}
	}

	private string GetSceneName(string fullPath)
	{
		return Path.GetFileNameWithoutExtension(fullPath);
	}
}
