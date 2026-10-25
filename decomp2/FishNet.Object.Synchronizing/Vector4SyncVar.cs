using System;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class Vector4SyncVar : SyncVar<Vector4>, ICustomSync
{
	public object GetSerializedType()
	{
		return typeof(Vector4);
	}

	protected override Vector4 Interpolate(Vector4 previous, Vector4 current, float percent)
	{
		return Vector4.Lerp(previous, current, percent);
	}

	public Vector4SyncVar()
		: base(default(SyncTypeSettings))
	{
	}
}
