using System;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class Vector2SyncVar : SyncVar<Vector2>, ICustomSync
{
	public object GetSerializedType()
	{
		return typeof(Vector2);
	}

	protected override Vector2 Interpolate(Vector2 previous, Vector2 current, float percent)
	{
		return Vector2.Lerp(previous, current, percent);
	}

	public Vector2SyncVar()
		: base(default(SyncTypeSettings))
	{
	}
}
