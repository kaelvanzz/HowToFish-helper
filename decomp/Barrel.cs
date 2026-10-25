using UnityEngine;

public class Barrel : Item
{
	[Header("Barrel")]
	[SerializeField]
	private Item _toSpawn;

	private bool NetworkInitialize___EarlyBarrelAssembly_002DCSharp_002Edll_Excuted;

	private bool NetworkInitialize___LateBarrelAssembly_002DCSharp_002Edll_Excuted;

	protected override void FixedUpdate()
	{
		base.FixedUpdate();
		if (base.IsServerInitialized)
		{
			TrySpawnItem();
		}
	}

	private void TrySpawnItem()
	{
		if (!base.Holder && !base.SyncedHolder && !base.IsDestroying && !base.IsDeinitializing && !(base.transform.position.y > WaterManager.WaterHeight - 1.5f))
		{
			if ((bool)_toSpawn && !BossManager.Boss)
			{
				Item item = Object.Instantiate(_toSpawn, base.transform.position, Quaternion.identity);
				Spawn(item.gameObject);
			}
			DestroyItem(1);
		}
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyBarrelAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___EarlyBarrelAssembly_002DCSharp_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateBarrelAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___LateBarrelAssembly_002DCSharp_002Edll_Excuted = true;
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
