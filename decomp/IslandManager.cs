using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FishNet;
using FishNet.Managing.Client;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities.Types;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IslandManager : MonoBehaviour
{
	private static IslandManager _instance;

	private ClientManager _clientManager;

	[SerializeField]
	[Scene]
	private List<string> _miniIslands = new List<string>();

	[SerializeField]
	private float _nearPlayerRange = 15f;

	[SerializeField]
	private IslandInfo[] _islandInfos;

	private List<string> _scenes = new List<string>();

	private readonly Queue<byte> _pendingIslands = new Queue<byte>();

	private const int IslandBuildOffset = 1;

	public static int TotalIslands { get; private set; }

	public static float NearPlayerRange => _instance._nearPlayerRange;

	public static bool IsLoading { get; private set; }

	public static bool IsInitialized { get; private set; }

	public static event Action OnFirstIslandLoaded;

	private void Awake()
	{
		_instance = this;
		IsLoading = false;
		IsInitialized = false;
		TotalIslands = SceneManager.sceneCountInBuildSettings - 1;
	}

	private void Start()
	{
		_clientManager = InstanceFinder.ClientManager;
		if ((bool)_clientManager)
		{
			_clientManager.OnClientConnectionState += OnLocalClientConnectionState;
		}
		Init();
	}

	private void OnDestroy()
	{
		if ((bool)_clientManager)
		{
			_clientManager.OnClientConnectionState -= OnLocalClientConnectionState;
		}
		StopAllCoroutines();
		SceneManager.activeSceneChanged -= OnActiveSceneChange;
		IsLoading = false;
		IsInitialized = false;
	}

	private void OnLocalClientConnectionState(ClientConnectionStateArgs args)
	{
		LocalConnectionState connectionState = args.ConnectionState;
		if (connectionState == LocalConnectionState.Started || connectionState == LocalConnectionState.Stopped)
		{
			IsInitialized = false;
		}
	}

	private void Init()
	{
		SceneManager.activeSceneChanged += OnActiveSceneChange;
		for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
			_scenes.Add(fileNameWithoutExtension);
		}
		UnloadIslands();
	}

	private void OnActiveSceneChange(Scene arg0, Scene arg1)
	{
	}

	public static void LoadIsland(byte islandId)
	{
		if ((bool)_instance)
		{
			_instance.QueueRequest(islandId);
		}
	}

	public static void UnloadIslands()
	{
		if ((bool)_instance)
		{
			_instance.QueueRequest(byte.MaxValue);
		}
	}

	private void QueueRequest(byte islandId)
	{
		_pendingIslands.Clear();
		if (IsLoading)
		{
			_pendingIslands.Enqueue(islandId);
		}
		else
		{
			StartCoroutine(ProcessRequest(islandId));
		}
	}

	private IEnumerator ProcessRequest(byte islandId)
	{
		IsLoading = true;
		yield return UnloadAllIslandsRoutine();
		DecalManager.ClearDecals();
		if (_pendingIslands.Count > 0)
		{
			islandId = _pendingIslands.Dequeue();
		}
		if (islandId != byte.MaxValue)
		{
			yield return SceneManager.LoadSceneAsync(islandId + 1, LoadSceneMode.Additive);
		}
		if (!Application.isPlaying)
		{
			IsLoading = false;
			yield break;
		}
		yield return new WaitForFixedUpdate();
		yield return new WaitForFixedUpdate();
		if (_pendingIslands.Count > 0)
		{
			yield return ProcessRequest(_pendingIslands.Dequeue());
			yield break;
		}
		if (!Island.CurIsland)
		{
			IsInitialized = false;
			IsLoading = false;
			yield break;
		}
		bool num = !IsInitialized;
		IsInitialized = true;
		if (num)
		{
			IslandManager.OnFirstIslandLoaded?.Invoke();
		}
		if ((bool)Server.Instance && Server.Instance.IsServerInitialized && OnlineIslandManager.TeleportPlayers && (bool)Player.LocalPlayer)
		{
			Vector3 pos = ((!OnlineIslandManager.TeleportToMini) ? SpawnManager.MainSpawnPos : SpawnManager.MiniSpawnPos);
			float rot = ((!OnlineIslandManager.TeleportToMini) ? SpawnManager.MainSpawnRot : SpawnManager.MiniSpawnRot);
			foreach (Player alivePlayer in PlayerManager.AlivePlayers)
			{
				Server.Instance.TeleportPlayer(alivePlayer, pos, rot);
			}
		}
		if (OnlineIslandManager.TeleportPlayers && (bool)Player.LocalPlayer && (bool)BoatManager.Instance)
		{
			BoatManager.Instance.TryMoveBoat(SpawnManager.GetBoatSpawnPos(Player.LocalPlayer.Transform.position), SpawnManager.GetBoatSpawnRot(Player.LocalPlayer.Transform.position));
		}
		OnlineIslandManager.ToggleTeleportPlayers(to: false);
		if (_pendingIslands.Count > 0)
		{
			yield return ProcessRequest(_pendingIslands.Dequeue());
		}
		else
		{
			IsLoading = false;
		}
	}

	private IEnumerator UnloadAllIslandsRoutine()
	{
		Scene mainScene = base.gameObject.scene;
		for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
		{
			Scene sceneAt = SceneManager.GetSceneAt(i);
			if (!(sceneAt == mainScene))
			{
				AsyncOperation asyncOperation = SceneManager.UnloadSceneAsync(sceneAt);
				if (asyncOperation != null)
				{
					yield return asyncOperation;
				}
			}
		}
		if ((bool)Server.Instance && Server.Instance.IsServerInitialized)
		{
			ItemManager.OnIslandUnloaded();
		}
	}

	public static void OnIslandChange(byte index)
	{
		for (int i = 0; i < _instance._islandInfos.Length; i++)
		{
			_instance._islandInfos[i].Toggle(i != index);
		}
	}

	public static IslandInfo GetIslandInfo(int index)
	{
		return _instance._islandInfos[index];
	}

	public static bool IsNearMainIsland(Vector2 position, float radius, int excludedIsland = -1)
	{
		if (!_instance)
		{
			return false;
		}
		float num = radius * radius;
		for (int i = 0; i < _instance._islandInfos.Length; i++)
		{
			if (i != excludedIsland)
			{
				Vector3 islandPosition = _instance._islandInfos[i].IslandPosition;
				if ((position - new Vector2(islandPosition.x, islandPosition.z)).sqrMagnitude < num * 2f)
				{
					return true;
				}
			}
		}
		return false;
	}

	public static byte GetRandomMiniIsland(byte excludeIsland = byte.MaxValue)
	{
		if (!_instance || _instance._miniIslands.Count == 0)
		{
			return byte.MaxValue;
		}
		int num = UnityEngine.Random.Range(0, _instance._miniIslands.Count);
		for (int i = 0; i < _instance._miniIslands.Count; i++)
		{
			int index = (num + i) % _instance._miniIslands.Count;
			int buildIndexByScenePath = SceneUtility.GetBuildIndexByScenePath(_instance._miniIslands[index]);
			if (buildIndexByScenePath > 0 && buildIndexByScenePath - 1 != excludeIsland)
			{
				return (byte)buildIndexByScenePath;
			}
		}
		return byte.MaxValue;
	}

	public static bool IsMiniIsland(byte islandId)
	{
		if (!_instance)
		{
			return false;
		}
		int num = islandId + 1;
		foreach (string miniIsland in _instance._miniIslands)
		{
			if (SceneUtility.GetBuildIndexByScenePath(miniIsland) == num)
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsMainIsland(byte islandId)
	{
		if (islandId + 1 < SceneManager.sceneCountInBuildSettings)
		{
			return !IsMiniIsland(islandId);
		}
		return false;
	}

	public static Vector3 GetCurIslandMovedDiff()
	{
		int curIsland = OnlineIslandManager.CurIsland;
		IslandInfo islandInfo = _instance._islandInfos[curIsland];
		return islandInfo.IslandPosition - islandInfo.ArchivedIslandPosition;
	}
}
