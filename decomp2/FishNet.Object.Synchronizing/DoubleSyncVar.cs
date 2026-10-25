using System;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class DoubleSyncVar : SyncVar<double>, ICustomSync
{
	public object GetSerializedType()
	{
		return typeof(double);
	}

	protected override double Interpolate(double previous, double current, float percent)
	{
		float a = (float)previous;
		float b = (float)current;
		return Mathf.Lerp(a, b, percent);
	}

	public DoubleSyncVar()
		: base(default(SyncTypeSettings))
	{
	}
}
