using UnityEngine;

public class SpawnManager : MonoBehaviour
{
	[SerializeField]
	private Island _connectedIsland;

	[SerializeField]
	private Transform _playerSpawnPoint;

	[Header("Boat")]
	[SerializeField]
	private Boat _boatPrefab;

	[SerializeField]
	private Transform _boatSpawnPoint;

	private static Transform _mainPlayerSpawnPoint;

	private static Transform _mainBoatSpawnPoint;

	private static Transform _miniPlayerSpawnPoint;

	private static Transform _miniBoatSpawnPoint;

	public static Vector3 MainSpawnPos
	{
		get
		{
			if (!_mainPlayerSpawnPoint)
			{
				return Vector3.zero;
			}
			return _mainPlayerSpawnPoint.position;
		}
	}

	public static float MainSpawnRot
	{
		get
		{
			if (!_mainPlayerSpawnPoint)
			{
				return 0f;
			}
			return _mainPlayerSpawnPoint.eulerAngles.y;
		}
	}

	private static Vector3 _boatSpawnPos
	{
		get
		{
			if (!_mainBoatSpawnPoint)
			{
				return Vector3.zero;
			}
			return _mainBoatSpawnPoint.position;
		}
	}

	private static Quaternion _boatSpawnRot
	{
		get
		{
			if (!_mainBoatSpawnPoint)
			{
				return Quaternion.identity;
			}
			return _mainBoatSpawnPoint.rotation;
		}
	}

	public static Vector3 MiniSpawnPos
	{
		get
		{
			if (!_miniPlayerSpawnPoint)
			{
				return Vector3.zero;
			}
			return _miniPlayerSpawnPoint.position;
		}
	}

	public static float MiniSpawnRot
	{
		get
		{
			if (!_miniPlayerSpawnPoint)
			{
				return 0f;
			}
			return _miniPlayerSpawnPoint.eulerAngles.y;
		}
	}

	private static Vector3 _miniBoatSpawnPos
	{
		get
		{
			if (!_miniBoatSpawnPoint)
			{
				return Vector3.zero;
			}
			return _miniBoatSpawnPoint.position;
		}
	}

	private static Quaternion _miniBoatSpawnRot
	{
		get
		{
			if (!_miniBoatSpawnPoint)
			{
				return Quaternion.identity;
			}
			return _miniBoatSpawnPoint.rotation;
		}
	}

	public static bool TryGetInitialPlayerSpawn(out Vector3 position, out float rotation)
	{
		position = default(Vector3);
		rotation = 0f;
		if (IslandManager.IsLoading || !IslandManager.IsInitialized || !OnlineIslandManager.Instance || !OnlineIslandManager.Instance.IsClientInitialized || !Island.CurIsland)
		{
			return false;
		}
		bool flag = OnlineIslandManager.CurMini != byte.MaxValue;
		byte b = (flag ? OnlineIslandManager.CurMini : OnlineIslandManager.CurIsland);
		if (Island.CurIsland.gameObject.scene.buildIndex != b + 1)
		{
			return false;
		}
		Transform transform = (flag ? _miniPlayerSpawnPoint : _mainPlayerSpawnPoint);
		if (flag)
		{
			if (!Island.CurIsland.HasAppliedMiniPosition)
			{
				return false;
			}
		}
		else if ((bool)Island.MiniIsland)
		{
			return false;
		}
		if (!transform || transform.gameObject.scene != Island.CurIsland.gameObject.scene)
		{
			return false;
		}
		position = transform.position;
		rotation = transform.eulerAngles.y;
		return true;
	}

	private void Awake()
	{
		SetSpawnPoints();
	}

	private void SetSpawnPoints()
	{
		if (!_connectedIsland || !_connectedIsland.IsMini)
		{
			_mainPlayerSpawnPoint = _playerSpawnPoint;
			_mainBoatSpawnPoint = _boatSpawnPoint;
		}
		else
		{
			_miniPlayerSpawnPoint = _playerSpawnPoint;
			_miniBoatSpawnPoint = _boatSpawnPoint;
		}
	}

	private void Start()
	{
		if ((bool)Server.Instance && Server.Instance.IsServerInitialized)
		{
			InitializeIsland();
		}
	}

	private void OnDestroy()
	{
		if (_mainPlayerSpawnPoint == _playerSpawnPoint)
		{
			_mainPlayerSpawnPoint = null;
		}
		if (_mainBoatSpawnPoint == _boatSpawnPoint)
		{
			_mainBoatSpawnPoint = null;
		}
		if (_miniPlayerSpawnPoint == _playerSpawnPoint)
		{
			_miniPlayerSpawnPoint = null;
		}
		if (_miniBoatSpawnPoint == _boatSpawnPoint)
		{
			_miniBoatSpawnPoint = null;
		}
	}

	private void InitializeIsland()
	{
		SaveManager.LoadWorldItems();
		if ((bool)BoatManager.Instance && (bool)_boatSpawnPoint && (bool)_boatPrefab)
		{
			BoatManager.Instance.TrySpawnBoat(_boatPrefab, _boatSpawnPoint.position, _boatSpawnPoint.rotation);
		}
	}

	public static Vector3 GetPlayerSpawnPos(Vector3 pos)
	{
		if (OnlineIslandManager.CurMini == byte.MaxValue)
		{
			return MainSpawnPos;
		}
		return MiniSpawnPos;
	}

	public static float GetPlayerSpawnRot(Vector3 pos)
	{
		if (OnlineIslandManager.CurMini == byte.MaxValue)
		{
			return MainSpawnRot;
		}
		return MiniSpawnRot;
	}

	public static Vector3 GetBoatSpawnPos(Vector3 pos)
	{
		if (OnlineIslandManager.CurMini == byte.MaxValue)
		{
			return _boatSpawnPos;
		}
		return _miniBoatSpawnPos;
	}

	public static Quaternion GetBoatSpawnRot(Vector3 pos)
	{
		if (OnlineIslandManager.CurMini == byte.MaxValue)
		{
			return _boatSpawnRot;
		}
		return _miniBoatSpawnRot;
	}
}
