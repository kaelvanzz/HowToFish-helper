public class Bottle : Item
{
	private bool NetworkInitialize___EarlyBottleAssembly_002DCSharp_002Edll_Excuted;

	private bool NetworkInitialize___LateBottleAssembly_002DCSharp_002Edll_Excuted;

	public override void OnPickUp()
	{
		base.OnPickUp();
		AchievementManager.CheckMessageInABottleAchievement(this);
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyBottleAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___EarlyBottleAssembly_002DCSharp_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateBottleAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___LateBottleAssembly_002DCSharp_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	public override void Awake()
	{
		NetworkInitialize___Early();
		base.Awake();
		NetworkInitialize___Late();
	}
}
