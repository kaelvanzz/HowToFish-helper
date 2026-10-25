using System;
using System.Collections.Generic;
using FishNet.CodeGenerating;
using FishNet.Component.Prediction;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.Scripting;

namespace FishNet.Object.Prediction;

[UseGlobalCustomSerializer]
[Preserve]
public class PredictionRigidbody : IResettable
{
	public struct AllForceData
	{
		public ForceMode Mode;

		public Vector3 Vector3Force;

		public Vector3 Position;

		public float FloatForce;

		public float Radius;

		public float UpwardsModifier;

		public AllForceData(Vector3 force, ForceMode mode)
		{
			this = default(AllForceData);
			Vector3Force = force;
			Mode = mode;
		}

		public AllForceData(Vector3 force, Vector3 position, ForceMode mode)
		{
			this = default(AllForceData);
			Vector3Force = force;
			Position = position;
			Mode = mode;
		}

		public AllForceData(float force, Vector3 position, float radius, float upwardsModifier, ForceMode mode)
		{
			this = default(AllForceData);
			FloatForce = force;
			Position = position;
			Radius = radius;
			UpwardsModifier = upwardsModifier;
			Mode = mode;
		}
	}

	public interface IForceData
	{
	}

	[Flags]
	public enum ForceApplicationType : byte
	{
		AddForceAtPosition = 1,
		AddExplosiveForce = 2,
		AddForce = 4,
		AddRelativeForce = 8,
		AddTorque = 0x10,
		AddRelativeTorque = 0x20
	}

	[UseGlobalCustomSerializer]
	public struct EntryData
	{
		public ForceApplicationType Type;

		public AllForceData Data;

		public EntryData(ForceApplicationType type, AllForceData data)
		{
			Type = type;
			Data = data;
		}

		public EntryData(EntryData fd)
		{
			Type = fd.Type;
			Data = fd.Data;
		}
	}

	[NonSerialized]
	internal RigidbodyState RigidbodyState;

	[ExcludeSerialization]
	private List<EntryData> _pendingForces;

	public Rigidbody Rigidbody { get; private set; }

	public bool HasPendingForces
	{
		get
		{
			if (_pendingForces != null)
			{
				return _pendingForces.Count > 0;
			}
			return false;
		}
	}

	public List<EntryData> GetPendingForces()
	{
		return _pendingForces;
	}

	~PredictionRigidbody()
	{
		if (_pendingForces != null)
		{
			CollectionCaches<EntryData>.StoreAndDefault(ref _pendingForces);
		}
		Rigidbody = null;
	}

	public void Initialize(Rigidbody rb)
	{
		Rigidbody = rb;
		if (_pendingForces == null)
		{
			_pendingForces = CollectionCaches<EntryData>.RetrieveList();
		}
		else
		{
			_pendingForces.Clear();
		}
	}

	public void AddForce(Vector3 force, ForceMode mode = ForceMode.Force)
	{
		EntryData item = new EntryData(ForceApplicationType.AddForce, new AllForceData(force, mode));
		_pendingForces.Add(item);
	}

	public void AddRelativeForce(Vector3 force, ForceMode mode = ForceMode.Force)
	{
		EntryData item = new EntryData(ForceApplicationType.AddRelativeForce, new AllForceData(force, mode));
		_pendingForces.Add(item);
	}

	public void AddTorque(Vector3 force, ForceMode mode = ForceMode.Force)
	{
		EntryData item = new EntryData(ForceApplicationType.AddTorque, new AllForceData(force, mode));
		_pendingForces.Add(item);
	}

	public void AddRelativeTorque(Vector3 force, ForceMode mode = ForceMode.Force)
	{
		EntryData item = new EntryData(ForceApplicationType.AddRelativeTorque, new AllForceData(force, mode));
		_pendingForces.Add(item);
	}

	public void AddExplosiveForce(float force, Vector3 position, float radius, float upwardsModifier = 0f, ForceMode mode = ForceMode.Force)
	{
		EntryData item = new EntryData(ForceApplicationType.AddExplosiveForce, new AllForceData(force, position, radius, upwardsModifier, mode));
		_pendingForces.Add(item);
	}

	public void AddForceAtPosition(Vector3 force, Vector3 position, ForceMode mode = ForceMode.Force)
	{
		EntryData item = new EntryData(ForceApplicationType.AddForceAtPosition, new AllForceData(force, position, mode));
		_pendingForces.Add(item);
	}

	public void Velocity(Vector3 force)
	{
		Rigidbody.linearVelocity = force;
		RemoveForces(velocity: true);
	}

	public void AngularVelocity(Vector3 force)
	{
		Rigidbody.angularVelocity = force;
		RemoveForces(velocity: false);
	}

	public void Simulate()
	{
		foreach (EntryData pendingForce in _pendingForces)
		{
			AllForceData data = pendingForce.Data;
			switch (pendingForce.Type)
			{
			case ForceApplicationType.AddTorque:
				Rigidbody.AddTorque(data.Vector3Force, data.Mode);
				break;
			case ForceApplicationType.AddForce:
				Rigidbody.AddForce(data.Vector3Force, data.Mode);
				break;
			case ForceApplicationType.AddRelativeTorque:
				Rigidbody.AddRelativeTorque(data.Vector3Force, data.Mode);
				break;
			case ForceApplicationType.AddRelativeForce:
				Rigidbody.AddRelativeForce(data.Vector3Force, data.Mode);
				break;
			case ForceApplicationType.AddExplosiveForce:
				Rigidbody.AddExplosionForce(data.FloatForce, data.Position, data.Radius, data.UpwardsModifier, data.Mode);
				break;
			case ForceApplicationType.AddForceAtPosition:
				Rigidbody.AddForceAtPosition(data.Vector3Force, data.Position, data.Mode);
				break;
			}
		}
		_pendingForces.Clear();
	}

	public void ClearPendingForces(bool velocity)
	{
		RemoveForces(velocity);
	}

	public void ClearPendingForces()
	{
		_pendingForces.Clear();
	}

	public void Reconcile(PredictionRigidbody pr)
	{
		_pendingForces.Clear();
		if (pr._pendingForces != null)
		{
			foreach (EntryData pendingForce in pr._pendingForces)
			{
				_pendingForces.Add(new EntryData(pendingForce));
			}
		}
		Rigidbody.SetState(pr.RigidbodyState);
		ResettableObjectCaches<PredictionRigidbody>.Store(pr);
	}

	private void RemoveForces(bool velocity)
	{
		if (_pendingForces.Count <= 0)
		{
			return;
		}
		ForceApplicationType velocityApplicationTypes = ForceApplicationType.AddExplosiveForce | ForceApplicationType.AddForce | ForceApplicationType.AddRelativeForce;
		List<EntryData> list = CollectionCaches<EntryData>.RetrieveList();
		foreach (EntryData pendingForce in _pendingForces)
		{
			if (VelocityApplicationTypesContains(pendingForce.Type) == !velocity)
			{
				list.Add(pendingForce);
			}
		}
		if (list.Count != _pendingForces.Count)
		{
			_pendingForces.Clear();
			foreach (EntryData item in list)
			{
				_pendingForces.Add(item);
			}
		}
		CollectionCaches<EntryData>.Store(list);
		bool VelocityApplicationTypesContains(ForceApplicationType apt)
		{
			return (velocityApplicationTypes & apt) == apt;
		}
	}

	internal void SetReconcileData(RigidbodyState rs, List<EntryData> lst)
	{
		RigidbodyState = rs;
		_pendingForces = lst;
	}

	public void ResetState()
	{
		CollectionCaches<EntryData>.StoreAndDefault(ref _pendingForces);
		Rigidbody = null;
	}

	public void InitializeState()
	{
	}
}
