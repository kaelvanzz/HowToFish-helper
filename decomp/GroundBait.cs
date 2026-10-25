using UnityEngine;

public class GroundBait : Item
{
	[Space]
	[SerializeField]
	private BaitInfo _bait;

	private bool NetworkInitialize___EarlyGroundBaitAssembly_002DCSharp_002Edll_Excuted;

	private bool NetworkInitialize___LateGroundBaitAssembly_002DCSharp_002Edll_Excuted;

	public BaitInfo Bait => _bait;

	public override void PickUp(Player player, bool calledFromLocal = false, bool sendToServer = false)
	{
		if ((bool)_bait)
		{
			Server.Instance.PickUpBait(player, this);
		}
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyGroundBaitAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___EarlyGroundBaitAssembly_002DCSharp_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateGroundBaitAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___LateGroundBaitAssembly_002DCSharp_002Edll_Excuted = true;
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
