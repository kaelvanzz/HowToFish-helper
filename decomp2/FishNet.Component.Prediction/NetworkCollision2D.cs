namespace FishNet.Component.Prediction;

public sealed class NetworkCollision2D : NetworkCollider2D
{
	private bool NetworkInitialize___EarlyFishNet_002EComponent_002EPrediction_002ENetworkCollision2DFishNet_002ERuntime_002Edll_Excuted;

	private bool NetworkInitialize___LateFishNet_002EComponent_002EPrediction_002ENetworkCollision2DFishNet_002ERuntime_002Edll_Excuted;

	public override void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_FishNet_002EComponent_002EPrediction_002ENetworkCollision2D_FishNet_002ERuntime_002Edll();
		NetworkInitialize___Late();
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyFishNet_002EComponent_002EPrediction_002ENetworkCollision2DFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___EarlyFishNet_002EComponent_002EPrediction_002ENetworkCollision2DFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateFishNet_002EComponent_002EPrediction_002ENetworkCollision2DFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___LateFishNet_002EComponent_002EPrediction_002ENetworkCollision2DFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	protected void Awake_UserLogic_FishNet_002EComponent_002EPrediction_002ENetworkCollision2D_FishNet_002ERuntime_002Edll()
	{
		IsTrigger = false;
		base.Awake();
	}
}
