using System;
using UnityEngine;

namespace FishNet.Object;

[Serializable]
public struct TransformProperties
{
	public Vector3 Position;

	public Quaternion Rotation;

	public Vector3 Scale;

	public bool IsValid;

	[Obsolete("Use Scale.")]
	public Vector3 LocalScale => Scale;

	public TransformProperties(Vector3 position, Quaternion rotation, Vector3 localScale)
	{
		Position = position;
		Rotation = rotation;
		Scale = localScale;
		IsValid = true;
	}

	public static TransformProperties GetTransformDefault()
	{
		return new TransformProperties(Vector3.zero, Quaternion.identity, Vector3.one);
	}

	public override string ToString()
	{
		return "Position: " + Position.ToString() + ", Rotation " + Rotation.ToString() + ", Scale " + Scale;
	}

	public TransformProperties(Transform t)
		: this(t.position, t.rotation, t.localScale)
	{
	}

	[Obsolete("Use ResetState.")]
	public void Reset()
	{
		ResetState();
	}

	public void ResetState()
	{
		Update(Vector3.zero, Quaternion.identity, Vector3.zero);
		IsValid = false;
	}

	public void Update(Transform t)
	{
		Update(t.position, t.rotation, t.localScale);
	}

	public void Update(TransformProperties tp)
	{
		Update(tp.Position, tp.Rotation, tp.Scale);
	}

	public void Update(Vector3 position, Quaternion rotation)
	{
		Update(position, rotation, Scale);
	}

	public void Update(Vector3 position, Quaternion rotation, Vector3 localScale)
	{
		Position = position;
		Rotation = rotation;
		Scale = localScale;
		IsValid = true;
	}

	public void Add(TransformProperties tp)
	{
		Position += tp.Position;
		Rotation *= tp.Rotation;
		Scale += tp.Scale;
	}

	public void Subtract(TransformProperties tp)
	{
		Position -= tp.Position;
		Rotation *= Quaternion.Inverse(tp.Rotation);
		Scale -= tp.Scale;
	}

	public bool ValuesEquals(TransformProperties properties)
	{
		if (Position == properties.Position && Rotation == properties.Rotation)
		{
			return Scale == properties.Scale;
		}
		return false;
	}
}
