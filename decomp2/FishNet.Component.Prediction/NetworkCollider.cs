using System;
using System.Collections.Generic;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Component.Prediction;

public abstract class NetworkCollider : NetworkColliderBase
{
	private Collider[] _colliders;

	private Collider[] _hits;

	private Dictionary<Collider, CollisionData> _enteredColliders;

	private bool NetworkInitialize___EarlyFishNet_002EComponent_002EPrediction_002ENetworkColliderFishNet_002ERuntime_002Edll_Excuted;

	private bool NetworkInitialize___LateFishNet_002EComponent_002EPrediction_002ENetworkColliderFishNet_002ERuntime_002Edll_Excuted;

	public event Action<Collider> OnEnter;

	public event Action<Collider> OnStay;

	public event Action<Collider> OnExit;

	public override void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_FishNet_002EComponent_002EPrediction_002ENetworkCollider_FishNet_002ERuntime_002Edll();
		NetworkInitialize___Late();
	}

	private void OnDestroy()
	{
		CollectionCaches<Collider, CollisionData>.StoreAndDefault(ref _enteredColliders);
		CollectionCaches<Collider>.StoreAndDefault(ref _hits, _hits.Length);
	}

	protected override void PredictionManager_OnPreReconcile(uint clientTick, uint serverTick)
	{
		if (_enteredColliders.Count > 0)
		{
			List<Collider> list = CollectionCaches<Collider>.RetrieveList();
			foreach (KeyValuePair<Collider, CollisionData> enteredCollider in _enteredColliders)
			{
				uint exitTick = enteredCollider.Value.ExitTick;
				if (exitTick != 0 && exitTick < clientTick)
				{
					list.Add(enteredCollider.Key);
				}
			}
			foreach (Collider item in list)
			{
				_enteredColliders.Remove(item);
			}
			CollectionCaches<Collider>.Store(list);
		}
		base.PredictionManager_OnPreReconcile(clientTick, serverTick);
	}

	protected override void CheckColliders(uint clientTick)
	{
		if (!TryPrepareColliderCheck(clientTick))
		{
			return;
		}
		HashSet<Collider> hashSet = CollectionCaches<Collider>.RetrieveHashSet();
		Dictionary<Collider, CollisionData> enteredColliders = _enteredColliders;
		Quaternion rotation = base.transform.rotation;
		Collider[] colliders = _colliders;
		foreach (Collider collider in colliders)
		{
			if (!collider.enabled || IsTrigger != collider.isTrigger)
			{
				continue;
			}
			int num = ((!(collider is SphereCollider sphereCollider)) ? ((!(collider is CapsuleCollider capsuleCollider)) ? ((collider is BoxCollider boxCollider) ? GetBoxColliderHits(boxCollider, rotation, InteractableLayers) : 0) : GetCapsuleColliderHits(capsuleCollider, InteractableLayers)) : GetSphereColliderHits(sphereCollider, InteractableLayers));
			for (int j = 0; j < num; j++)
			{
				Collider collider2 = _hits[j];
				if (collider2 == null || collider2 == collider)
				{
					continue;
				}
				hashSet.Add(collider2);
				if (enteredColliders.TryGetValueIL2CPP(collider2, out var value))
				{
					if (value.EnterTick >= clientTick || value.ExitTick != 0)
					{
						this.OnExit?.Invoke(collider2);
						this.OnEnter?.Invoke(collider2);
						enteredColliders[collider2] = new CollisionData(clientTick);
					}
				}
				else
				{
					this.OnEnter?.Invoke(collider2);
					enteredColliders[collider2] = new CollisionData(clientTick);
				}
				this.OnStay?.Invoke(collider2);
			}
			List<Collider> list = CollectionCaches<Collider>.RetrieveList();
			foreach (Collider key in enteredColliders.Keys)
			{
				if (!hashSet.Contains(key) && enteredColliders[key].EnterTick != clientTick)
				{
					list.Add(key);
				}
			}
			foreach (Collider item in list)
			{
				this.OnExit?.Invoke(item);
				if (base.IsServerStarted)
				{
					enteredColliders.Remove(item);
				}
				else if (enteredColliders[item].EnterTick > clientTick)
				{
					enteredColliders[item] = new CollisionData(enteredColliders[item].EnterTick, clientTick);
				}
				else
				{
					enteredColliders.Remove(item);
				}
			}
		}
		CollectionCaches<Collider>.Store(hashSet);
	}

	private int GetSphereColliderHits(SphereCollider sphereCollider, int layerMask)
	{
		sphereCollider.GetSphereOverlapParams(out var center, out var radius);
		radius += AdditionalSize;
		return base.gameObject.scene.GetPhysicsScene().OverlapSphere(center, radius, _hits, layerMask, QueryTriggerInteraction.UseGlobal);
	}

	private int GetCapsuleColliderHits(CapsuleCollider capsuleCollider, int layerMask)
	{
		capsuleCollider.GetCapsuleCastParams(out var point, out var point2, out var radius);
		radius += AdditionalSize;
		return base.gameObject.scene.GetPhysicsScene().OverlapCapsule(point, point2, radius, _hits, layerMask);
	}

	private int GetBoxColliderHits(BoxCollider boxCollider, Quaternion rotation, int layerMask)
	{
		boxCollider.GetBoxOverlapParams(out var center, out var halfExtents);
		Vector3 vector = Vector3.one * AdditionalSize;
		halfExtents += vector;
		return base.gameObject.scene.GetPhysicsScene().OverlapBox(center, halfExtents, _hits, rotation, layerMask);
	}

	public override bool TryFindColliders(bool force = false)
	{
		if (!base.TryFindColliders(force))
		{
			return false;
		}
		ClearColliderDataHistory(invokeOnExit: true);
		_colliders = GetComponents<Collider>();
		return true;
	}

	public override void ResetState(bool asServer)
	{
		ClearColliderDataHistory(invokeOnExit: true);
		base.ResetState(asServer);
	}

	protected override void ClearColliderDataHistory(bool invokeOnExit)
	{
		if (_enteredColliders == null)
		{
			return;
		}
		if (invokeOnExit)
		{
			foreach (KeyValuePair<Collider, CollisionData> enteredCollider in _enteredColliders)
			{
				if (enteredCollider.Value.ExitTick == 0)
				{
					this.OnExit?.Invoke(enteredCollider.Key);
				}
			}
		}
		_enteredColliders.Clear();
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyFishNet_002EComponent_002EPrediction_002ENetworkColliderFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___EarlyFishNet_002EComponent_002EPrediction_002ENetworkColliderFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateFishNet_002EComponent_002EPrediction_002ENetworkColliderFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___LateFishNet_002EComponent_002EPrediction_002ENetworkColliderFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	protected virtual void Awake_UserLogic_FishNet_002EComponent_002EPrediction_002ENetworkCollider_FishNet_002ERuntime_002Edll()
	{
		base.Awake();
		_enteredColliders = CollectionCaches<Collider, CollisionData>.RetrieveDictionary();
		_hits = CollectionCaches<Collider>.RetrieveArray();
		if (_hits.Length < MaximumSimultaneousHits)
		{
			_hits = new Collider[MaximumSimultaneousHits];
		}
	}
}
