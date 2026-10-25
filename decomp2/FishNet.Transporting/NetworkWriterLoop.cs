using FishNet.Managing.Timing;
using UnityEngine;

namespace FishNet.Transporting;

[DisallowMultipleComponent]
[DefaultExecutionOrder(32767)]
internal class NetworkWriterLoop : MonoBehaviour
{
	private TimeManager _timeManager;

	private void Awake()
	{
		_timeManager = GetComponent<TimeManager>();
	}

	private void LateUpdate()
	{
		Iterate();
	}

	private void Iterate()
	{
		_timeManager.TickLateUpdate();
	}
}
