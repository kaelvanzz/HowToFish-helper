using System;
using System.Collections.Generic;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Component.Prediction;

public abstract class NetworkCollider2D : NetworkColliderBase
{
	private Collider2D[] _colliders;

	private Collider2D[] _hits;

	private Dictionary<Collider2D, CollisionData> _enteredColliders;

	private bool NetworkInitialize___EarlyFishNet_002EComponent_002EPrediction_002ENetworkCollider2DFishNet_002ERuntime_002Edll_Excuted;

	private bool NetworkInitialize___LateFishNet_002EComponent_002EPrediction_002ENetworkCollider2DFishNet_002ERuntime_002Edll_Excuted;

	public event Action<Collider2D> OnEnter;

	public event Action<Collider2D> OnStay;

	public event Action<Collider2D> OnExit;

	public override void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_FishNet_002EComponent_002EPrediction_002ENetworkCollider2D_FishNet_002ERuntime_002Edll();
		NetworkInitialize___Late();
	}

	private void OnDestroy()
	{
		CollectionCaches<Collider2D, CollisionData>.StoreAndDefault(ref _enteredColliders);
		CollectionCaches<Collider2D>.StoreAndDefault(ref _hits, _hits.Length);
	}

	protected override void PredictionManager_OnPreReconcile(uint clientTick, uint serverTick)
	{
		if (_enteredColliders.Count > 0)
		{
			List<Collider2D> list = CollectionCaches<Collider2D>.RetrieveList();
			foreach (KeyValuePair<Collider2D, CollisionData> enteredCollider in _enteredColliders)
			{
				uint exitTick = enteredCollider.Value.ExitTick;
				if (exitTick != 0 && exitTick < clientTick)
				{
					list.Add(enteredCollider.Key);
				}
			}
			foreach (Collider2D item in list)
			{
				_enteredColliders.Remove(item);
			}
			CollectionCaches<Collider2D>.Store(list);
		}
		base.PredictionManager_OnPreReconcile(clientTick, serverTick);
	}

	protected override void CheckColliders(uint clientTick)
	{
		if (!TryPrepareColliderCheck(clientTick))
		{
			return;
		}
		HashSet<Collider2D> hashSet = CollectionCaches<Collider2D>.RetrieveHashSet();
		Dictionary<Collider2D, CollisionData> enteredColliders = _enteredColliders;
		Quaternion rotation = base.transform.rotation;
		Collider2D[] colliders = _colliders;
		foreach (Collider2D collider2D in colliders)
		{
			if (!collider2D.enabled || IsTrigger != collider2D.isTrigger)
			{
				continue;
			}
			int num = ((!(collider2D is CircleCollider2D circleCollider)) ? ((collider2D is BoxCollider2D boxCollider) ? GetBoxCollider2DHits(boxCollider, rotation, InteractableLayers) : 0) : GetCircleCollider2DHits(circleCollider, InteractableLayers));
			for (int j = 0; j < num; j++)
			{
				Collider2D collider2D2 = _hits[j];
				if (collider2D2 == null || collider2D2 == collider2D)
				{
					continue;
				}
				hashSet.Add(collider2D2);
				if (enteredColliders.TryGetValueIL2CPP(collider2D2, out var value))
				{
					if (value.EnterTick >= clientTick || value.ExitTick != 0)
					{
						this.OnExit?.Invoke(collider2D2);
						this.OnEnter?.Invoke(collider2D2);
						enteredColliders[collider2D2] = new CollisionData(clientTick);
					}
				}
				else
				{
					this.OnEnter?.Invoke(collider2D2);
					enteredColliders[collider2D2] = new CollisionData(clientTick);
				}
				this.OnStay?.Invoke(collider2D2);
			}
			List<Collider2D> list = CollectionCaches<Collider2D>.RetrieveList();
			foreach (Collider2D key in enteredColliders.Keys)
			{
				if (!hashSet.Contains(key) && enteredColliders[key].EnterTick != clientTick)
				{
					list.Add(key);
				}
			}
			foreach (Collider2D item in list)
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
		CollectionCaches<Collider2D>.Store(hashSet);
	}

	private int GetCircleCollider2DHits(CircleCollider2D circleCollider, int layerMask)
	{
		circleCollider.GetCircleOverlapParams(out var center, out var radius);
		radius += AdditionalSize;
		return base.gameObject.scene.GetPhysicsScene2D().OverlapCircle(center, radius, _hits, layerMask);
	}

	private int GetBoxCollider2DHits(BoxCollider2D boxCollider, Quaternion rotation, int layerMask)
	{
		boxCollider.GetBox2DOverlapParams(out var center, out var halfExtents);
		Vector3 vector = Vector3.one * AdditionalSize;
		halfExtents += vector;
		return base.gameObject.scene.GetPhysicsScene2D().OverlapBox(center, halfExtents, rotation.z, _hits, layerMask);
	}

	public override bool TryFindColliders(bool force = false)
	{
		if (!base.TryFindColliders(force))
		{
			return false;
		}
		ClearColliderDataHistory(invokeOnExit: true);
		_colliders = GetComponents<Collider2D>();
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
			foreach (KeyValuePair<Collider2D, CollisionData> enteredCollider in _enteredColliders)
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
		if (!NetworkInitialize___EarlyFishNet_002EComponent_002EPrediction_002ENetworkCollider2DFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___EarlyFishNet_002EComponent_002EPrediction_002ENetworkCollider2DFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateFishNet_002EComponent_002EPrediction_002ENetworkCollider2DFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___LateFishNet_002EComponent_002EPrediction_002ENetworkCollider2DFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	protected virtual void Awake_UserLogic_FishNet_002EComponent_002EPrediction_002ENetworkCollider2D_FishNet_002ERuntime_002Edll()
	{
		base.Awake();
		_enteredColliders = CollectionCaches<Collider2D, CollisionData>.RetrieveDictionary();
		_hits = CollectionCaches<Collider2D>.RetrieveArray();
		if (_hits.Length < MaximumSimultaneousHits)
		{
			_hits = new Collider2D[MaximumSimultaneousHits];
		}
	}
}
