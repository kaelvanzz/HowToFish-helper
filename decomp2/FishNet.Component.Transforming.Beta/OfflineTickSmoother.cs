using FishNet.Managing;
using FishNet.Managing.Timing;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace FishNet.Component.Transforming.Beta;

public class OfflineTickSmoother : MonoBehaviour
{
	[Tooltip("True to automatically initialize in Awake using InstanceFinder. When false you will need to manually call Initialize.")]
	[SerializeField]
	private bool _automaticallyInitialize = true;

	[Tooltip("Settings required to initialize the smoother.")]
	[SerializeField]
	private InitializationSettings _initializationSettings;

	[FormerlySerializedAs("_controllerMovementSettings")]
	[Tooltip("How smoothing occurs when the controller of the object.")]
	[SerializeField]
	private MovementSettings _movementSettings = new MovementSettings(unityReallyNeedsToSupportParameterlessInitializersOnStructsAlready: true);

	public TickSmootherController SmootherController { get; private set; }

	public bool IsInitialized { get; private set; }

	private void Awake()
	{
		RetrieveControllers();
		AutomaticallyInitialize();
	}

	private void OnDestroy()
	{
		if (SmootherController != null)
		{
			SmootherController.StopSmoother();
			SmootherController.OnDestroy();
		}
		StoreControllers();
		IsInitialized = false;
	}

	private void AutomaticallyInitialize()
	{
		if (_automaticallyInitialize)
		{
			TimeManager timeManager = InstanceFinder.TimeManager;
			if (timeManager == null)
			{
				NetworkManagerExtensions.LogWarning("Automatic initialization failed on " + base.gameObject.name + ". You must manually call Initialize.");
			}
			else
			{
				Initialize(timeManager);
			}
		}
	}

	public void Initialize(TimeManager timeManager)
	{
		if (timeManager == null)
		{
			NetworkManagerExtensions.LogError("TimeManager cannot be null when initializing.");
			return;
		}
		SmootherController.SetTimeManager(timeManager);
		_initializationSettings.SetOfflineRuntimeValues(timeManager, base.transform);
		SmootherController.Initialize(_initializationSettings, _movementSettings, default(MovementSettings));
		SmootherController.StartSmoother();
		IsInitialized = true;
	}

	public void SetTargetTransform(Transform value)
	{
		if (IsInitialized)
		{
			NetworkManagerExtensions.LogError("Target can only be set before Initialize is called.");
		}
		else
		{
			_initializationSettings.TargetTransform = value;
		}
	}

	private void StoreControllers()
	{
		if (SmootherController != null)
		{
			ResettableObjectCaches<TickSmootherController>.Store(SmootherController);
			SmootherController = null;
		}
	}

	private void RetrieveControllers()
	{
		StoreControllers();
		SmootherController = ResettableObjectCaches<TickSmootherController>.Retrieve();
	}
}
