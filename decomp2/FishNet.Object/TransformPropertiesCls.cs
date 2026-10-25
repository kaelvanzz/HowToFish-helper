using System;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Object;

[Serializable]
public class TransformPropertiesCls : IResettable
{
	public Vector3 Position;

	public Quaternion Rotation;

	public Vector3 LocalScale;

	public TransformPropertiesCls()
	{
	}

	public TransformPropertiesCls(Vector3 position, Quaternion rotation, Vector3 localScale)
	{
		Position = position;
		Rotation = rotation;
		LocalScale = localScale;
	}

	public void InitializeState()
	{
	}

	public void ResetState()
	{
		Update(Vector3.zero, Quaternion.identity, Vector3.zero);
	}

	public void Update(Transform t)
	{
		Update(t.position, t.rotation, t.localScale);
	}

	public void Update(TransformPropertiesCls tp)
	{
		Update(tp.Position, tp.Rotation, tp.LocalScale);
	}

	public void Update(TransformProperties tp)
	{
		Update(tp.Position, tp.Rotation, tp.Scale);
	}

	public void Update(Vector3 position, Quaternion rotation)
	{
		Update(position, rotation, LocalScale);
	}

	public void Update(Vector3 position, Quaternion rotation, Vector3 localScale)
	{
		Position = position;
		Rotation = rotation;
		LocalScale = localScale;
	}

	public bool ValuesEquals(TransformPropertiesCls properties)
	{
		if (Position == properties.Position && Rotation == properties.Rotation)
		{
			return LocalScale == properties.LocalScale;
		}
		return false;
	}

	public TransformProperties ToStruct()
	{
		return new TransformProperties(Position, Rotation, LocalScale);
	}
}
