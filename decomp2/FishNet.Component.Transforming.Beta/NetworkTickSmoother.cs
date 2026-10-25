using FishNet.Object;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Component.Transforming.Beta;

public class NetworkTickSmoother : NetworkBehaviour
{
	[Tooltip("Settings required to initialize the smoother.")]
	[SerializeField]
	private InitializationSettings _initializationSettings;

	[Tooltip("How smoothing occurs when the controller of the object.")]
	[SerializeField]
	private MovementSettings _controllerMovementSettings = new MovementSettings(unityReallyNeedsToSupportParameterlessInitializersOnStructsAlready: true);

	[Tooltip("How smoothing occurs when spectating the object.")]
	[SerializeField]
	private MovementSettings _spectatorMovementSettings = new MovementSettings(unityReallyNeedsToSupportParameterlessInitializersOnStructsAlready: true);

	private bool NetworkInitialize___EarlyFishNet_002EComponent_002ETransforming_002EBeta_002ENetworkTickSmootherFishNet_002ERuntime_002Edll_Excuted;

	private bool NetworkInitialize___LateFishNet_002EComponent_002ETransforming_002EBeta_002ENetworkTickSmootherFishNet_002ERuntime_002Edll_Excuted;

	public TickSmootherController SmootherController { get; private set; }

	private void OnDestroy()
	{
		if (SmootherController != null)
		{
			SmootherController.OnDestroy();
		}
		StoreControllers();
	}

	public override void OnStartClient()
	{
		RetrieveControllers();
		_initializationSettings.SetNetworkedRuntimeValues(this, base.transform);
		SmootherController.Initialize(_initializationSettings, _controllerMovementSettings, _spectatorMovementSettings);
		SmootherController.StartSmoother();
	}

	public override void OnStopClient()
	{
		if (SmootherController != null)
		{
			SmootherController.StopSmoother();
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

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyFishNet_002EComponent_002ETransforming_002EBeta_002ENetworkTickSmootherFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___EarlyFishNet_002EComponent_002ETransforming_002EBeta_002ENetworkTickSmootherFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateFishNet_002EComponent_002ETransforming_002EBeta_002ENetworkTickSmootherFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___LateFishNet_002EComponent_002ETransforming_002EBeta_002ENetworkTickSmootherFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	public virtual void Awake()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}
}
