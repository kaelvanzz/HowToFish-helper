using UnityEngine;

namespace FishNet.Managing.Statistic;

[DisallowMultipleComponent]
[AddComponentMenu("FishNet/Manager/StatisticsManager")]
public class StatisticsManager : MonoBehaviour
{
	[Tooltip("True to operate while in release. This may cause allocations and impact performance.")]
	[SerializeField]
	private bool _runInRelease;

	[Tooltip("Statistics for NetworkTraffic.")]
	[SerializeField]
	private NetworkTrafficStatistics _networkTraffic;

	internal void InitializeOnce_Internal(NetworkManager manager)
	{
		if (!_runInRelease)
		{
			_networkTraffic = null;
			return;
		}
		InstantiateNetworkTrafficIfNeeded();
		_networkTraffic.InitializeOnce_Internal(manager);
	}

	public bool TryGetNetworkTrafficStatistics(out NetworkTrafficStatistics statistics)
	{
		InstantiateNetworkTrafficIfNeeded();
		if (_networkTraffic.IsEnabled())
		{
			statistics = _networkTraffic;
		}
		else
		{
			statistics = null;
		}
		return statistics != null;
	}

	private void InstantiateNetworkTrafficIfNeeded()
	{
		if (_networkTraffic == null)
		{
			_networkTraffic = new NetworkTrafficStatistics();
		}
	}
}
