namespace FishNet.Component.Prediction;

public sealed class NetworkTrigger : NetworkCollider
{
	private bool NetworkInitialize___EarlyFishNet_002EComponent_002EPrediction_002ENetworkTriggerFishNet_002ERuntime_002Edll_Excuted;

	private bool NetworkInitialize___LateFishNet_002EComponent_002EPrediction_002ENetworkTriggerFishNet_002ERuntime_002Edll_Excuted;

	public override void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_FishNet_002EComponent_002EPrediction_002ENetworkTrigger_FishNet_002ERuntime_002Edll();
		NetworkInitialize___Late();
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyFishNet_002EComponent_002EPrediction_002ENetworkTriggerFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___EarlyFishNet_002EComponent_002EPrediction_002ENetworkTriggerFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateFishNet_002EComponent_002EPrediction_002ENetworkTriggerFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___LateFishNet_002EComponent_002EPrediction_002ENetworkTriggerFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	protected void Awake_UserLogic_FishNet_002EComponent_002EPrediction_002ENetworkTrigger_FishNet_002ERuntime_002Edll()
	{
		IsTrigger = true;
		base.Awake();
	}
}
