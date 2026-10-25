using System;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class FloatSyncVar : SyncVar<float>, ICustomSync
{
	public object GetSerializedType()
	{
		return typeof(float);
	}

	protected override float Interpolate(float previous, float current, float percent)
	{
		return Mathf.Lerp(previous, current, percent);
	}

	public FloatSyncVar()
		: base(default(SyncTypeSettings))
	{
	}
}
