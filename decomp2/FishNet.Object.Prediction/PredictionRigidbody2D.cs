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
public class PredictionRigidbody2D : IResettable
{
	[Flags]
	public enum ForceApplicationType : byte
	{
		AddForceAtPosition = 1,
		AddForce = 4,
		AddRelativeForce = 8,
		AddTorque = 0x10
	}

	public struct AllForceData
	{
		public Vector3 Vector3Force;

		public float FloatForce;

		public Vector3 Position;

		public ForceMode2D Mode;

		public AllForceData(Vector3 force, ForceMode2D mode)
		{
			this = default(AllForceData);
			Vector3Force = force;
			Mode = mode;
		}

		public AllForceData(float force, ForceMode2D mode)
		{
			this = default(AllForceData);
			FloatForce = force;
			Mode = mode;
		}

		public AllForceData(Vector3 force, Vector3 position, ForceMode2D mode)
		{
			this = default(AllForceData);
			Vector3Force = force;
			Position = position;
			Mode = mode;
		}
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
	internal Rigidbody2DState Rigidbody2DState;

	[ExcludeSerialization]
	private List<EntryData> _pendingForces;

	public Rigidbody2D Rigidbody2D { get; private set; }

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

	~PredictionRigidbody2D()
	{
		if (_pendingForces != null)
		{
			CollectionCaches<EntryData>.StoreAndDefault(ref _pendingForces);
		}
		Rigidbody2D = null;
	}

	public void Initialize(Rigidbody2D rb)
	{
		Rigidbody2D = rb;
		if (_pendingForces == null)
		{
			_pendingForces = CollectionCaches<EntryData>.RetrieveList();
		}
		else
		{
			_pendingForces.Clear();
		}
	}

	public void AddForce(Vector3 force, ForceMode2D mode = ForceMode2D.Force)
	{
		EntryData item = new EntryData(ForceApplicationType.AddForce, new AllForceData(force, mode));
		_pendingForces.Add(item);
	}

	public void AddRelativeForce(Vector3 force, ForceMode2D mode = ForceMode2D.Force)
	{
		EntryData item = new EntryData(ForceApplicationType.AddRelativeForce, new AllForceData(force, mode));
		_pendingForces.Add(item);
	}

	public void AddTorque(float force, ForceMode2D mode = ForceMode2D.Force)
	{
		EntryData item = new EntryData(ForceApplicationType.AddTorque, new AllForceData(force, mode));
		_pendingForces.Add(item);
	}

	public void AddForceAtPosition(Vector3 force, Vector3 position, ForceMode2D mode = ForceMode2D.Force)
	{
		EntryData item = new EntryData(ForceApplicationType.AddForceAtPosition, new AllForceData(force, position, mode));
		_pendingForces.Add(item);
	}

	public void Velocity(Vector3 force)
	{
		Rigidbody2D.linearVelocity = force;
		RemoveForces(velocity: true);
	}

	public void AngularVelocity(float force)
	{
		Rigidbody2D.angularVelocity = force;
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
				Rigidbody2D.AddTorque(data.FloatForce, data.Mode);
				break;
			case ForceApplicationType.AddForce:
				Rigidbody2D.AddForce(data.Vector3Force, data.Mode);
				break;
			case ForceApplicationType.AddRelativeForce:
				Rigidbody2D.AddRelativeForce(data.Vector3Force, data.Mode);
				break;
			case ForceApplicationType.AddForceAtPosition:
				Rigidbody2D.AddForceAtPosition(data.Vector3Force, data.Position, data.Mode);
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

	public void Reconcile(PredictionRigidbody2D pr)
	{
		_pendingForces.Clear();
		if (pr._pendingForces != null)
		{
			foreach (EntryData pendingForce in pr._pendingForces)
			{
				_pendingForces.Add(new EntryData(pendingForce));
			}
		}
		Rigidbody2D.SetState(pr.Rigidbody2DState);
		ResettableObjectCaches<PredictionRigidbody2D>.Store(pr);
	}

	private void RemoveForces(bool velocity)
	{
		if (_pendingForces.Count <= 0)
		{
			return;
		}
		ForceApplicationType velocityApplicationTypes = ForceApplicationType.AddForce | ForceApplicationType.AddRelativeForce;
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

	internal void SetPendingForces(List<EntryData> lst)
	{
		_pendingForces = lst;
	}

	internal void SetReconcileData(Rigidbody2DState rs, List<EntryData> lst)
	{
		Rigidbody2DState = rs;
		_pendingForces = lst;
	}

	public void ResetState()
	{
		CollectionCaches<EntryData>.StoreAndDefault(ref _pendingForces);
		Rigidbody2D = null;
	}

	public void InitializeState()
	{
	}
}
