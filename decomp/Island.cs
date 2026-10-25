using UnityEngine;
using UnityEngine.Rendering;

public class Island : MonoBehaviour
{
	[SerializeField]
	private float _islandSize = 55f;

	[SerializeField]
	private bool _isMiniIsland;

	public static float IslandSize = 55f;

	public static Vector3 IslandPos;

	public static Vector3 MiniPos;

	public static Island CurIsland;

	public static Island MiniIsland;

	public bool IsMini => _isMiniIsland;

	public bool HasAppliedMiniPosition { get; private set; }

	private void Awake()
	{
		if (_isMiniIsland)
		{
			base.transform.parent.position = MiniPos;
		}
	}

	private void Start()
	{
		CurIsland = this;
		IslandSize = _islandSize;
		IslandPos = (_isMiniIsland ? MiniPos : base.transform.position);
		if (_isMiniIsland)
		{
			MiniIsland = this;
			if ((bool)OnlineIslandManager.Instance)
			{
				SetMiniPos(OnlineIslandManager.MiniPosition);
			}
		}
		else
		{
			SetProbeWorldOffset(Vector3.zero);
		}
	}

	public static Vector3 GetClosestIslandPos(Vector3 pos)
	{
		return IslandPos;
	}

	private void OnDestroy()
	{
		if (_isMiniIsland && MiniIsland == this)
		{
			SetProbeWorldOffset(Vector3.zero);
			MiniIsland = null;
		}
		if (CurIsland == this)
		{
			CurIsland = null;
		}
	}

	public static void SetMiniPos(Vector2 xzPos)
	{
		Vector3 vector = (MiniPos = new Vector3(xzPos.x, 0f, xzPos.y));
		if ((bool)MiniIsland)
		{
			MiniIsland.transform.parent.position = vector;
			MiniIsland.HasAppliedMiniPosition = true;
			SetProbeWorldOffset(vector);
			if (CurIsland == MiniIsland)
			{
				IslandPos = vector;
			}
		}
	}

	private static void SetProbeWorldOffset(Vector3 offset)
	{
		Volume globalVolume = GameInfo.GlobalVolume;
		if ((bool)globalVolume && globalVolume.profile.TryGet<ProbeVolumesOptions>(out var component))
		{
			component.worldOffset.overrideState = true;
			component.worldOffset.value = offset;
		}
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.cyan;
		Gizmos.DrawWireSphere(base.transform.position, _islandSize);
	}
}
