using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.Observing;

public class HashGrid : MonoBehaviour
{
	public enum GridAxes : byte
	{
		XY,
		YZ,
		XZ
	}

	internal static Vector2Int UnsetGridPosition = Vector2Int.one * int.MaxValue;

	internal static GridEntry EmptyGridEntry = new GridEntry(new HashSet<GridEntry>());

	[Tooltip("Axes of world space to base the grid on.")]
	[SerializeField]
	private GridAxes _gridAxes;

	[Tooltip("Accuracy of the grid. Objects will be considered nearby if they are within this number of units. Lower values may be more expensive.")]
	[Range(1f, 65535f)]
	[SerializeField]
	private ushort _accuracy = 10;

	private int _halfAccuracy;

	private Stack<HashSet<GridEntry>> _gridEntryHashSetCache = new Stack<HashSet<GridEntry>>();

	private Stack<GridEntry> _gridEntryCache = new Stack<GridEntry>();

	private Dictionary<Vector2Int, GridEntry> _gridEntries = new Dictionary<Vector2Int, GridEntry>();

	private NetworkManager _networkManager;

	private void Awake()
	{
		_networkManager = GetComponentInParent<NetworkManager>();
		if (_networkManager == null)
		{
			_networkManager.LogError("NetworkManager not found on object or within parent of " + base.gameObject.name + ". The " + GetType().Name + " must be placed on or beneath a NetworkManager.");
		}
		else if (!_networkManager.HasInstance<HashGrid>())
		{
			_halfAccuracy = Mathf.CeilToInt((float)(int)_accuracy / 2f);
			_networkManager.RegisterInstance(this);
		}
		else
		{
			UnityEngine.Object.Destroy(this);
		}
	}

	private void OutputNewGridCollections(out GridEntry gridEntry, out HashSet<GridEntry> gridEntries)
	{
		if (!_gridEntryHashSetCache.TryPop(out gridEntries))
		{
			BuildGridEntryHashSetCache();
			gridEntries = new HashSet<GridEntry>();
		}
		if (!_gridEntryCache.TryPop(out gridEntry))
		{
			BuildGridEntryCache();
			gridEntry = new GridEntry();
		}
		void BuildGridEntryCache()
		{
			for (int i = 0; i < 100; i++)
			{
				_gridEntryCache.Push(new GridEntry());
			}
		}
		void BuildGridEntryHashSetCache()
		{
			for (int j = 0; j < 100; j++)
			{
				_gridEntryHashSetCache.Push(new HashSet<GridEntry>());
			}
		}
	}

	private GridEntry CreateGridEntry(Vector2Int position)
	{
		OutputNewGridCollections(out var gridEntry, out var gridEntries);
		gridEntry.SetValues(position, gridEntries);
		_gridEntries[position] = gridEntry;
		int num = position.x + 1;
		int num2 = position.y + 1;
		int num3 = 0;
		for (int i = position.x - 1; i <= num; i++)
		{
			for (int j = position.y - 1; j <= num2; j++)
			{
				num3++;
				if (_gridEntries.TryGetValue(new Vector2Int(i, j), out var value))
				{
					gridEntries.Add(value);
					value.NearbyEntries.Add(gridEntry);
				}
			}
		}
		return gridEntry;
	}

	internal void GetNearbyHashGridPositions(NetworkObject nob, ref HashSet<Vector2Int> collection)
	{
		Vector2Int hashGridPosition = GetHashGridPosition(nob);
		int num = hashGridPosition.x + 1;
		int num2 = hashGridPosition.y + 1;
		for (int i = hashGridPosition.x - 1; i < num; i++)
		{
			for (int j = hashGridPosition.y - 1; j < num2; j++)
			{
				collection.Add(new Vector2Int(i, j));
			}
		}
	}

	internal Vector2Int GetHashGridPosition(NetworkObject nob)
	{
		Vector3 position = nob.transform.position;
		float num;
		float num2;
		if (_gridAxes == GridAxes.XY)
		{
			num = position.x;
			num2 = position.y;
		}
		else if (_gridAxes == GridAxes.XZ)
		{
			num = position.x;
			num2 = position.z;
		}
		else
		{
			if (_gridAxes != GridAxes.YZ)
			{
				_networkManager.LogError("GridAxes of " + _gridAxes.ToString() + " is not handled.");
				return default(Vector2Int);
			}
			num = position.y;
			num2 = position.z;
		}
		return new Vector2Int((int)num / _halfAccuracy, (int)num2 / _halfAccuracy);
	}

	internal GridEntry GetGridEntry(NetworkObject nob)
	{
		Vector2Int hashGridPosition = GetHashGridPosition(nob);
		return GetGridEntry(hashGridPosition);
	}

	internal GridEntry GetGridEntry(Vector2Int position)
	{
		if (!_gridEntries.TryGetValue(position, out var value))
		{
			return CreateGridEntry(position);
		}
		return value;
	}
}
