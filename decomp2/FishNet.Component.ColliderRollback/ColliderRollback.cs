using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.ColliderRollback;

public class ColliderRollback : NetworkBehaviour
{
	internal enum BoundingBoxType
	{
		Disabled,
		Manual
	}

	[Tooltip("How to configure the bounding box check.")]
	[SerializeField]
	private BoundingBoxType _boundingBox;

	[Tooltip("Physics type to generate a bounding box for.")]
	[SerializeField]
	private RollbackPhysicsType _physicsType = RollbackPhysicsType.Physics;

	[Tooltip("Size for the bounding box.. This is only used when BoundingBox is set to Manual.")]
	[SerializeField]
	private Vector3 _boundingBoxSize = new Vector3(3f, 3f, 3f);

	[Tooltip("Objects holding colliders which can rollback.")]
	[SerializeField]
	private GameObject[] _colliderParents = new GameObject[0];

	private bool NetworkInitialize___EarlyFishNet_002EComponent_002EColliderRollback_002EColliderRollbackFishNet_002ERuntime_002Edll_Excuted;

	private bool NetworkInitialize___LateFishNet_002EComponent_002EColliderRollback_002EColliderRollbackFishNet_002ERuntime_002Edll_Excuted;

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyFishNet_002EComponent_002EColliderRollback_002EColliderRollbackFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___EarlyFishNet_002EComponent_002EColliderRollback_002EColliderRollbackFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateFishNet_002EComponent_002EColliderRollback_002EColliderRollbackFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___LateFishNet_002EComponent_002EColliderRollback_002EColliderRollbackFishNet_002ERuntime_002Edll_Excuted = true;
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
