using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.Ownership;

public class PredictedSpawn : NetworkBehaviour
{
	[Tooltip("True to allow clients to predicted spawn this object.")]
	[SerializeField]
	private bool _allowSpawning = true;

	[Tooltip("True to allow clients to predicted despawn this object.")]
	[SerializeField]
	private bool _allowDespawning = true;

	private bool NetworkInitialize___EarlyFishNet_002EComponent_002EOwnership_002EPredictedSpawnFishNet_002ERuntime_002Edll_Excuted;

	private bool NetworkInitialize___LateFishNet_002EComponent_002EOwnership_002EPredictedSpawnFishNet_002ERuntime_002Edll_Excuted;

	public bool GetAllowSpawning()
	{
		return _allowSpawning;
	}

	public void SetAllowSpawning(bool value)
	{
		_allowSpawning = value;
	}

	public bool GetAllowDespawning()
	{
		return _allowDespawning;
	}

	public void SetAllowDespawning(bool value)
	{
		_allowDespawning = value;
	}

	public virtual bool OnTrySpawnClient(NetworkConnection owner = null)
	{
		return GetAllowSpawning();
	}

	public virtual bool OnTrySpawnServer(NetworkConnection spawner, NetworkConnection owner = null)
	{
		return GetAllowSpawning();
	}

	public virtual bool OnTryDespawnClient()
	{
		return GetAllowDespawning();
	}

	public virtual bool OnTryDespawnServer(NetworkConnection despawner)
	{
		return GetAllowDespawning();
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyFishNet_002EComponent_002EOwnership_002EPredictedSpawnFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___EarlyFishNet_002EComponent_002EOwnership_002EPredictedSpawnFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateFishNet_002EComponent_002EOwnership_002EPredictedSpawnFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___LateFishNet_002EComponent_002EOwnership_002EPredictedSpawnFishNet_002ERuntime_002Edll_Excuted = true;
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
