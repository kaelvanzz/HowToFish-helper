using System;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class Vector3SyncVar : SyncVar<Vector3>, ICustomSync
{
	public object GetSerializedType()
	{
		return typeof(Vector3);
	}

	protected override Vector3 Interpolate(Vector3 previous, Vector3 current, float percent)
	{
		return Vector3.Lerp(previous, current, percent);
	}

	public Vector3SyncVar()
		: base(default(SyncTypeSettings))
	{
	}
}
