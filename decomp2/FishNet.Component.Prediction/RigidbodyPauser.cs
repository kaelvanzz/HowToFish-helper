using System.Collections.Generic;
using FishNet.Managing;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Component.Prediction;

public class RigidbodyPauser : IResettable
{
	private struct RigidbodyData
	{
		public Rigidbody Rigidbody;

		public Vector3 Velocity;

		public Vector3 AngularVelocity;

		public bool IsKinematic;

		public CollisionDetectionMode CollisionDetectionMode;

		public RigidbodyData(Rigidbody rb)
		{
			Rigidbody = rb;
			Velocity = Vector3.zero;
			AngularVelocity = Vector3.zero;
			IsKinematic = rb.isKinematic;
			CollisionDetectionMode = rb.collisionDetectionMode;
		}

		public void Update(Rigidbody rb)
		{
			Velocity = rb.linearVelocity;
			AngularVelocity = rb.angularVelocity;
			IsKinematic = rb.isKinematic;
			CollisionDetectionMode = rb.collisionDetectionMode;
		}
	}

	private struct Rigidbody2DData
	{
		public Rigidbody2D Rigidbody2d;

		public Vector2 Velocity;

		public float AngularVelocity;

		public bool IsKinematic;

		public bool Simulated;

		public CollisionDetectionMode2D CollisionDetectionMode;

		public Rigidbody2DData(Rigidbody2D rb)
		{
			Rigidbody2d = rb;
			Velocity = Vector2.zero;
			AngularVelocity = 0f;
			Simulated = rb.simulated;
			IsKinematic = rb.isKinematic;
			CollisionDetectionMode = rb.collisionDetectionMode;
		}

		public void Update(Rigidbody2D rb)
		{
			Velocity = rb.linearVelocity;
			AngularVelocity = rb.angularVelocity;
			Simulated = rb.simulated;
			IsKinematic = rb.isKinematic;
			CollisionDetectionMode = rb.collisionDetectionMode;
		}
	}

	private List<RigidbodyData> _rigidbodyDatas = new List<RigidbodyData>();

	private List<Rigidbody2DData> _rigidbody2dDatas = new List<Rigidbody2DData>();

	private bool _getInChildren;

	private Transform _transform;

	private RigidbodyType _rigidbodyType;

	private bool _initialized;

	public bool Paused { get; private set; }

	public void UpdateRigidbodies()
	{
		if (!_initialized)
		{
			InstanceFinder.NetworkManager.LogError("T" + GetType().Name + " has not been initialized yet. This method cannot be used.");
		}
		else
		{
			UpdateRigidbodies(_transform, _rigidbodyType, _getInChildren);
		}
	}

	public void UpdateRigidbodies(Rigidbody[] rbs)
	{
		List<Rigidbody> list = CollectionCaches<Rigidbody>.RetrieveList();
		foreach (Rigidbody item in rbs)
		{
			list.Add(item);
		}
		UpdateRigidbodies(list);
		CollectionCaches<Rigidbody>.Store(list);
	}

	private void UpdateRigidbodies(List<Rigidbody> rbs)
	{
		_rigidbodyDatas.Clear();
		foreach (Rigidbody rb in rbs)
		{
			_rigidbodyDatas.Add(new RigidbodyData(rb));
		}
		_initialized = true;
	}

	public void UpdateRigidbodies2D(Rigidbody2D[] rbs)
	{
		List<Rigidbody2D> list = CollectionCaches<Rigidbody2D>.RetrieveList();
		foreach (Rigidbody2D item in rbs)
		{
			list.Add(item);
		}
		UpdateRigidbodies2D(list);
		CollectionCaches<Rigidbody2D>.Store(list);
	}

	private void UpdateRigidbodies2D(List<Rigidbody2D> rbs)
	{
		_rigidbody2dDatas.Clear();
		foreach (Rigidbody2D rb in rbs)
		{
			_rigidbody2dDatas.Add(new Rigidbody2DData(rb));
		}
		_initialized = true;
	}

	public void UpdateRigidbodies(Transform t, RigidbodyType rbType, bool getInChildren)
	{
		_rigidbodyType = rbType;
		_getInChildren = getInChildren;
		if (rbType == RigidbodyType.Rigidbody)
		{
			List<Rigidbody> list = CollectionCaches<Rigidbody>.RetrieveList();
			if (getInChildren)
			{
				Rigidbody[] componentsInChildren = t.GetComponentsInChildren<Rigidbody>();
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					list.Add(componentsInChildren[i]);
				}
			}
			else
			{
				Rigidbody component = t.GetComponent<Rigidbody>();
				if (component != null)
				{
					list.Add(component);
				}
			}
			UpdateRigidbodies(list);
			CollectionCaches<Rigidbody>.Store(list);
			return;
		}
		List<Rigidbody2D> list2 = CollectionCaches<Rigidbody2D>.RetrieveList();
		if (getInChildren)
		{
			Rigidbody2D[] componentsInChildren2 = t.GetComponentsInChildren<Rigidbody2D>();
			for (int j = 0; j < componentsInChildren2.Length; j++)
			{
				list2.Add(componentsInChildren2[j]);
			}
		}
		else
		{
			Rigidbody2D component2 = t.GetComponent<Rigidbody2D>();
			if (component2 != null)
			{
				list2.Add(component2);
			}
		}
		UpdateRigidbodies2D(list2);
		CollectionCaches<Rigidbody2D>.Store(list2);
	}

	public void Pause()
	{
		if (Paused)
		{
			return;
		}
		Paused = true;
		if (_rigidbodyType == RigidbodyType.Rigidbody)
		{
			for (int i = 0; i < _rigidbodyDatas.Count; i++)
			{
				if (!PauseRigidbody(i))
				{
					_rigidbodyDatas.RemoveAt(i);
					i--;
				}
			}
			return;
		}
		for (int j = 0; j < _rigidbody2dDatas.Count; j++)
		{
			if (!PauseRigidbody(j))
			{
				_rigidbody2dDatas.RemoveAt(j);
				j--;
			}
		}
		bool PauseRigidbody(int index)
		{
			RigidbodyData value2 = _rigidbodyDatas[index];
			Rigidbody rigidbody = value2.Rigidbody;
			if (rigidbody == null)
			{
				return false;
			}
			value2.Update(rigidbody);
			_rigidbodyDatas[index] = value2;
			rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
			rigidbody.isKinematic = true;
			return true;
		}
		bool PauseRigidbody(int index)
		{
			Rigidbody2DData value = _rigidbody2dDatas[index];
			Rigidbody2D rigidbody2d = value.Rigidbody2d;
			if (rigidbody2d == null)
			{
				return false;
			}
			value.Update(rigidbody2d);
			_rigidbody2dDatas[index] = value;
			rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.None;
			rigidbody2d.isKinematic = true;
			rigidbody2d.simulated = false;
			return true;
		}
	}

	public void Unpause()
	{
		if (!Paused)
		{
			return;
		}
		Paused = false;
		if (_rigidbodyType == RigidbodyType.Rigidbody)
		{
			for (int i = 0; i < _rigidbodyDatas.Count; i++)
			{
				if (!UnpauseRigidbody(i))
				{
					_rigidbodyDatas.RemoveAt(i);
					i--;
				}
			}
			return;
		}
		for (int j = 0; j < _rigidbody2dDatas.Count; j++)
		{
			if (!UnpauseRigidbody(j))
			{
				_rigidbody2dDatas.RemoveAt(j);
				j--;
			}
		}
		bool UnpauseRigidbody(int index)
		{
			RigidbodyData rigidbodyData = _rigidbodyDatas[index];
			Rigidbody rigidbody = rigidbodyData.Rigidbody;
			if (rigidbody == null)
			{
				return false;
			}
			if (rigidbodyData.IsKinematic)
			{
				return true;
			}
			rigidbody.isKinematic = rigidbodyData.IsKinematic;
			rigidbody.collisionDetectionMode = rigidbodyData.CollisionDetectionMode;
			if (!rigidbody.isKinematic)
			{
				rigidbody.linearVelocity = rigidbodyData.Velocity;
				rigidbody.angularVelocity = rigidbodyData.AngularVelocity;
			}
			return true;
		}
		bool UnpauseRigidbody(int index)
		{
			Rigidbody2DData rigidbody2DData = _rigidbody2dDatas[index];
			Rigidbody2D rigidbody2d = rigidbody2DData.Rigidbody2d;
			if (rigidbody2d == null)
			{
				return false;
			}
			if (rigidbody2DData.IsKinematic || !rigidbody2DData.Simulated)
			{
				return true;
			}
			rigidbody2d.isKinematic = rigidbody2DData.IsKinematic;
			rigidbody2d.simulated = rigidbody2DData.Simulated;
			rigidbody2d.collisionDetectionMode = rigidbody2DData.CollisionDetectionMode;
			if (!rigidbody2d.isKinematic)
			{
				rigidbody2d.linearVelocity = rigidbody2DData.Velocity;
				rigidbody2d.angularVelocity = rigidbody2DData.AngularVelocity;
			}
			return true;
		}
	}

	public void ResetState()
	{
		_rigidbodyDatas.Clear();
		_rigidbody2dDatas.Clear();
		_getInChildren = false;
		_transform = null;
		_rigidbodyType = RigidbodyType.Rigidbody;
		_initialized = false;
		Paused = false;
	}

	public void InitializeState()
	{
	}
}
