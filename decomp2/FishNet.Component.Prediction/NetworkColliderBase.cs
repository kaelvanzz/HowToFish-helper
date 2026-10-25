using FishNet.Object;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace FishNet.Component.Prediction;

public abstract class NetworkColliderBase : NetworkBehaviour
{
	protected struct CollisionData
	{
		public uint EnterTick;

		public uint ExitTick;

		public CollisionData(uint enterTick)
		{
			this = default(CollisionData);
			EnterTick = enterTick;
			ExitTick = 0u;
		}

		public CollisionData(uint enterTick, uint exitTick)
		{
			this = default(CollisionData);
			EnterTick = enterTick;
			ExitTick = exitTick;
		}
	}

	[HideInInspector]
	protected bool IsTrigger;

	[FormerlySerializedAs("_maximumSimultaneousHits")]
	[Tooltip("Maximum number of simultaneous hits to check for. Larger values decrease performance but allow detection to work for more overlapping colliders. Typically the default value of 16 is more than sufficient.")]
	[SerializeField]
	protected ushort MaximumSimultaneousHits = 16;

	[FormerlySerializedAs("_additionalSize")]
	[Tooltip("Units to extend collision traces by. This is used to prevent missed overlaps when colliders do not intersect enough.")]
	[Range(0f, 100f)]
	[SerializeField]
	protected float AdditionalSize = 0.1f;

	[FormerlySerializedAs("_layers")]
	[Tooltip("Layers to trace on. This is used when value is not nothing.")]
	[SerializeField]
	protected LayerMask Layers = 0;

	private bool _collidersFound;

	private int _lastGameObjectLayer = -1;

	[HideInInspector]
	protected int InteractableLayers;

	private bool NetworkInitialize___EarlyFishNet_002EComponent_002EPrediction_002ENetworkColliderBaseFishNet_002ERuntime_002Edll_Excuted;

	private bool NetworkInitialize___LateFishNet_002EComponent_002EPrediction_002ENetworkColliderBaseFishNet_002ERuntime_002Edll_Excuted;

	public virtual void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_FishNet_002EComponent_002EPrediction_002ENetworkColliderBase_FishNet_002ERuntime_002Edll();
		NetworkInitialize___Late();
	}

	public override void OnStartNetwork()
	{
		base.TimeManager.OnPostPhysicsSimulation += TimeManager_OnPostPhysicsSimulation;
	}

	public override void OnStartClient()
	{
		base.PredictionManager.OnPostReplicateReplay += PredictionManager_OnPostReplicateReplay;
		base.PredictionManager.OnPostReconcileSyncTransforms += PredictionManager_OnPreReconcile;
	}

	public override void OnStopClient()
	{
		base.PredictionManager.OnPostReplicateReplay -= PredictionManager_OnPostReplicateReplay;
		base.PredictionManager.OnPostReconcileSyncTransforms -= PredictionManager_OnPreReconcile;
	}

	public override void OnStopNetwork()
	{
		base.TimeManager.OnPostPhysicsSimulation -= TimeManager_OnPostPhysicsSimulation;
	}

	protected virtual void PredictionManager_OnPreReconcile(uint clientTick, uint serverTick)
	{
		CheckColliders(clientTick);
	}

	private void TimeManager_OnPostPhysicsSimulation(float delta)
	{
		CheckColliders(base.TimeManager.LocalTick);
	}

	private void PredictionManager_OnPostReplicateReplay(uint clientTick, uint serverTick)
	{
		CheckColliders(clientTick);
	}

	protected bool TryPrepareColliderCheck(uint tick)
	{
		if (tick == 0)
		{
			return false;
		}
		if ((int)Layers != (int)(LayerMask)0)
		{
			InteractableLayers = Layers;
		}
		else
		{
			int layer = base.gameObject.layer;
			if (_lastGameObjectLayer != layer)
			{
				_lastGameObjectLayer = layer;
				InteractableLayers = GameKit.Dependencies.Utilities.Layers.GetInteractableLayersValue(layer);
			}
		}
		return true;
	}

	protected abstract void CheckColliders(uint clientTick);

	protected abstract void ClearColliderDataHistory(bool invokeOnExit);

	public virtual bool TryFindColliders(bool force = false)
	{
		return !_collidersFound || force;
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyFishNet_002EComponent_002EPrediction_002ENetworkColliderBaseFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___EarlyFishNet_002EComponent_002EPrediction_002ENetworkColliderBaseFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateFishNet_002EComponent_002EPrediction_002ENetworkColliderBaseFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___LateFishNet_002EComponent_002EPrediction_002ENetworkColliderBaseFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	protected virtual void Awake_UserLogic_FishNet_002EComponent_002EPrediction_002ENetworkColliderBase_FishNet_002ERuntime_002Edll()
	{
		TryFindColliders(force: true);
	}
}
