using FishNet.Managing.Timing;
using UnityEngine;

namespace FishNet.Transporting;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-32768)]
internal class NetworkReaderLoop : MonoBehaviour
{
	private TimeManager _timeManager;

	private void Awake()
	{
		_timeManager = GetComponent<TimeManager>();
	}

	private void FixedUpdate()
	{
		_timeManager.TickFixedUpdate();
	}

	private void Update()
	{
		_timeManager.TickUpdate();
	}
}
