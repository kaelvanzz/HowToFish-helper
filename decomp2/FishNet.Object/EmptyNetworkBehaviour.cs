namespace FishNet.Object;

public class EmptyNetworkBehaviour : NetworkBehaviour
{
	private bool NetworkInitialize___EarlyFishNet_002EObject_002EEmptyNetworkBehaviourFishNet_002ERuntime_002Edll_Excuted;

	private bool NetworkInitialize___LateFishNet_002EObject_002EEmptyNetworkBehaviourFishNet_002ERuntime_002Edll_Excuted;

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyFishNet_002EObject_002EEmptyNetworkBehaviourFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___EarlyFishNet_002EObject_002EEmptyNetworkBehaviourFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateFishNet_002EObject_002EEmptyNetworkBehaviourFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___LateFishNet_002EObject_002EEmptyNetworkBehaviourFishNet_002ERuntime_002Edll_Excuted = true;
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
