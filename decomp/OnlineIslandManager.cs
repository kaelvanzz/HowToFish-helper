using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class OnlineIslandManager : NetworkBehaviour
{
	public static OnlineIslandManager Instance;

	public readonly SyncVar<byte> _curIsland = new SyncVar<byte>();

	public readonly SyncVar<byte> _maxIslandUnlocked = new SyncVar<byte>(2);

	public readonly SyncVar<byte> _activeIsland = new SyncVar<byte>();

	public readonly SyncVar<Vector2> _miniPos = new SyncVar<Vector2>();

	private bool _clientStarted;

	private byte _requestedIsland = byte.MaxValue;

	private float _nextMiniCheck;

	private readonly Dictionary<Player, Vector2> _lastPlayerPositions = new Dictionary<Player, Vector2>();

	private readonly HashSet<Player> _playersWhoApproachedMini = new HashSet<Player>();

	private const float MainDepartureDist = 250f;

	private const float MiniApproachDist = 150f;

	private const float MiniDepartureDist = 240f;

	private const float MiniLeadDist = 300f;

	private const float MainIslandClearance = 260f;

	private bool NetworkInitialize___EarlyOnlineIslandManagerAssembly_002DCSharp_002Edll_Excuted;

	private bool NetworkInitialize___LateOnlineIslandManagerAssembly_002DCSharp_002Edll_Excuted;

	public static bool TeleportPlayers { get; private set; }

	public static bool TeleportToMini { get; private set; }

	public static float TimeWhenSwappingIsland { get; private set; }

	public static byte MaxIslandUnlocked => Instance._maxIslandUnlocked.Value;

	public static byte CurIsland => Instance._curIsland.Value;

	public static byte ActiveIsland => Instance._activeIsland.Value;

	public static byte CurMini
	{
		get
		{
			if (!IslandManager.IsMiniIsland(Instance._activeIsland.Value))
			{
				return byte.MaxValue;
			}
			return Instance._activeIsland.Value;
		}
	}

	public static Vector2 MiniPosition => Instance._miniPos.Value;

	public virtual void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_OnlineIslandManager_Assembly_002DCSharp_002Edll();
		NetworkInitialize___Late();
	}

	public override void OnStartServer()
	{
		if (SaveManager.CurServerSave != null)
		{
			_maxIslandUnlocked.Value = (ClientSettings.CheatsEnabled ? byte.MaxValue : SaveManager.CurServerSave.MaxIsland);
			SaveManager.ServerSaveObject curServerSave = SaveManager.CurServerSave;
			byte b = curServerSave.SpawnedIsland;
			byte b2 = ((curServerSave.HasMiniIsland && IslandManager.IsMiniIsland(curServerSave.SpawnedMiniIsland)) ? curServerSave.SpawnedMiniIsland : byte.MaxValue);
			Vector2 value = curServerSave.MiniIslandPosition;
			if (IslandManager.IsMiniIsland(b))
			{
				b2 = b;
				b = 0;
				Vector3 islandPosition = IslandManager.GetIslandInfo(b).IslandPosition;
				value = new Vector2(islandPosition.x + 400f, islandPosition.z);
				Debug.LogWarning("Legacy mini-island save: using the first main island as the saved main island.");
			}
			_curIsland.Value = (byte)(IslandManager.IsMainIsland(b) ? b : 0);
			_miniPos.Value = value;
			_activeIsland.Value = ((b2 != byte.MaxValue) ? b2 : _curIsland.Value);
		}
		else if (ClientSettings.CheatsEnabled)
		{
			_maxIslandUnlocked.Value = byte.MaxValue;
			_curIsland.Value = 5;
			_activeIsland.Value = 5;
		}
	}

	public override void OnStartClient()
	{
		_clientStarted = true;
		OnActiveIslandChange(_activeIsland.Value, _activeIsland.Value, asServer: false);
	}

	public override void OnStopClient()
	{
		_clientStarted = false;
		_requestedIsland = byte.MaxValue;
		_lastPlayerPositions.Clear();
		_playersWhoApproachedMini.Clear();
		IslandManager.UnloadIslands();
	}

	private void Update()
	{
		if (!base.IsServerInitialized)
		{
			return;
		}
		if (!IslandManager.IsLoading && Time.time >= _nextMiniCheck)
		{
			_nextMiniCheck = Time.time + 1f;
			foreach (Player alivePlayer in PlayerManager.AlivePlayers)
			{
				if (TrySpawnMiniAhead(alivePlayer))
				{
					break;
				}
			}
		}
		if (ClientSettings.CheatsEnabled && (bool)Player.LocalPlayer && !Player.LocalPlayer.BlockInputs && !IslandManager.IsLoading && Input.GetKeyDown(KeyCode.O))
		{
			if (!Input.GetKey(KeyCode.LeftShift))
			{
				TpToNextIsland();
			}
			else
			{
				TpToRandomMiniIsland();
			}
		}
	}

	private void TpToRandomMiniIsland()
	{
		byte randomMiniIsland = IslandManager.GetRandomMiniIsland(CurMini);
		if (randomMiniIsland != byte.MaxValue)
		{
			ToggleTeleportPlayers(to: true, toMini: true);
			MonoBehaviour.print($"spawning mini: build index: {randomMiniIsland}");
			SpawnIsland((byte)(randomMiniIsland - 1), isMini: true, null, Vector2.zero);
		}
		else
		{
			MonoBehaviour.print("cant spawn mini, couldn't find mini scene in build");
		}
	}

	public static void ToggleTeleportPlayers(bool to, bool toMini = false)
	{
		if ((bool)Instance && Instance.IsServerInitialized)
		{
			TeleportPlayers = to;
			TeleportToMini = toMini;
		}
	}

	public void UnlockIsland(byte islandIndex)
	{
		if (islandIndex > _maxIslandUnlocked.Value)
		{
			_maxIslandUnlocked.Value = islandIndex;
		}
	}

	public static void TpToNextIsland(bool prev = false)
	{
		if ((bool)Instance)
		{
			ToggleTeleportPlayers(to: true);
			Instance.NextIslandInBuild(prev);
		}
	}

	public static void TpToSpecificIsland(byte to)
	{
		if ((bool)Instance)
		{
			ToggleTeleportPlayers(to: true);
			Instance.SpawnIsland(to);
		}
	}

	private void NextIslandInBuild(bool backwards = false)
	{
		int num = _curIsland.Value;
		for (int i = 0; i < IslandManager.TotalIslands; i++)
		{
			num += ((!backwards) ? 1 : (-1));
			if (num >= IslandManager.TotalIslands - 1)
			{
				num = 0;
			}
			else if (num < 0)
			{
				num = IslandManager.TotalIslands - 2;
			}
			if (IslandManager.IsMainIsland((byte)num))
			{
				SpawnIsland((byte)num);
				break;
			}
		}
	}

	public void SpawnIsland(byte islandIndex, bool isMini = false, Player player = null, Vector2? miniPosition = null)
	{
		if (!base.IsServerInitialized)
		{
			return;
		}
		if (isMini ? (!IslandManager.IsMiniIsland(islandIndex)) : (!IslandManager.IsMainIsland(islandIndex)))
		{
			Debug.LogWarning($"Rejected island {islandIndex}: scene does not match isMini={isMini}.");
			return;
		}
		if (isMini)
		{
			if (!miniPosition.HasValue && !player && PlayerManager.AlivePlayers.Count > 0)
			{
				player = PlayerManager.AlivePlayers[0];
			}
			if (!miniPosition.HasValue && (!player || !Island.CurIsland))
			{
				ToggleTeleportPlayers(to: false);
				return;
			}
			Vector2 vector = miniPosition ?? GetMiniPositionAhead(player.Transform.position);
			if (!miniPosition.HasValue && IslandManager.IsNearMainIsland(vector, 260f))
			{
				ToggleTeleportPlayers(to: false);
				return;
			}
			_miniPos.Value = vector;
		}
		else
		{
			_curIsland.Value = islandIndex;
		}
		_lastPlayerPositions.Clear();
		_playersWhoApproachedMini.Clear();
		_activeIsland.Value = islandIndex;
	}

	public bool TrySpawnMiniAhead(Player player)
	{
		if (!base.IsServerInitialized || !player || !Island.CurIsland || IslandManager.IsLoading || Island.CurIsland.gameObject.scene.buildIndex != _activeIsland.Value + 1 || TeleportPlayers)
		{
			return false;
		}
		Vector2 vector = new Vector2(player.Transform.position.x, player.Transform.position.z);
		Vector2 vector2 = new Vector2(Island.IslandPos.x, Island.IslandPos.z);
		bool flag = IslandManager.IsMiniIsland(_activeIsland.Value);
		float sqrMagnitude = (vector - vector2).sqrMagnitude;
		if (flag && sqrMagnitude <= 22500f)
		{
			_playersWhoApproachedMini.Add(player);
		}
		if (Time.time - TimeWhenSwappingIsland < 5f)
		{
			return false;
		}
		Vector2 value;
		bool num = _lastPlayerPositions.TryGetValue(player, out value);
		_lastPlayerPositions[player] = vector;
		Vector2 vector3 = vector - value;
		float num2 = (flag ? 240f : 250f);
		if (!num || vector3.sqrMagnitude < 0.01f || Vector2.Dot(vector3, vector - vector2) <= 0f || sqrMagnitude < num2 * num2 || (flag && !_playersWhoApproachedMini.Contains(player)))
		{
			return false;
		}
		Vector2 miniPositionAhead = GetMiniPositionAhead(player.Transform.position, vector3);
		if (IslandManager.IsNearMainIsland(vector, 260f, flag ? (-1) : _activeIsland.Value) || IslandManager.IsNearMainIsland(miniPositionAhead, 260f))
		{
			return false;
		}
		if (!DazedUtils.AllPlayersNearbyCheck(player.Transform.position, IslandManager.NearPlayerRange))
		{
			return false;
		}
		byte randomMiniIsland = IslandManager.GetRandomMiniIsland(CurMini);
		if (randomMiniIsland == byte.MaxValue)
		{
			return false;
		}
		SpawnIsland((byte)(randomMiniIsland - 1), isMini: true, player, miniPositionAhead);
		return true;
	}

	private static Vector2 GetMiniPositionAhead(Vector3 playerPosition, Vector2? travelDirection = null)
	{
		Vector2 vector = new Vector2(Island.IslandPos.x, Island.IslandPos.z);
		Vector2 vector2 = new Vector2(playerPosition.x, playerPosition.z);
		Vector2 normalized = (travelDirection ?? (vector2 - vector)).normalized;
		if (normalized == Vector2.zero && (bool)Player.LocalPlayer)
		{
			Vector3 forward = Player.LocalPlayer.Transform.forward;
			normalized = new Vector2(forward.x, forward.z).normalized;
		}
		Vector3 vector3 = Quaternion.AngleAxis(Random.Range(-30f, 30f), Vector3.up) * new Vector3(normalized.x, 0f, normalized.y);
		return vector2 + new Vector2(vector3.x, vector3.z) * 300f;
	}

	private void OnActiveIslandChange(byte prev, byte next, bool asServer)
	{
		if (!asServer && _clientStarted && _requestedIsland != next)
		{
			_requestedIsland = next;
			_lastPlayerPositions.Clear();
			_playersWhoApproachedMini.Clear();
			TimeWhenSwappingIsland = Time.time;
			IslandManager.LoadIsland(next);
			IslandManager.OnIslandChange(IslandManager.IsMiniIsland(next) ? byte.MaxValue : next);
		}
	}

	private void OnMiniPosChange(Vector2 prev, Vector2 next, bool asServer)
	{
		if (asServer || !base.IsServerInitialized)
		{
			Island.SetMiniPos(next);
		}
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyOnlineIslandManagerAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___EarlyOnlineIslandManagerAssembly_002DCSharp_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
			_miniPos.InitializeEarly(this, 3u, isSyncObject: false);
			_activeIsland.InitializeEarly(this, 2u, isSyncObject: false);
			_maxIslandUnlocked.InitializeEarly(this, 1u, isSyncObject: false);
			_curIsland.InitializeEarly(this, 0u, isSyncObject: false);
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateOnlineIslandManagerAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___LateOnlineIslandManagerAssembly_002DCSharp_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
			_miniPos.InitializeLate();
			_activeIsland.InitializeLate();
			_maxIslandUnlocked.InitializeLate();
			_curIsland.InitializeLate();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	private void Awake_UserLogic_OnlineIslandManager_Assembly_002DCSharp_002Edll()
	{
		Instance = this;
		_activeIsland.OnChange += OnActiveIslandChange;
		_miniPos.OnChange += OnMiniPosChange;
	}
}
