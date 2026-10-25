using System;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class IntSyncVar : SyncVar<int>, ICustomSync
{
	public object GetSerializedType()
	{
		return typeof(int);
	}

	protected override int Interpolate(int previous, int current, float percent)
	{
		return (int)Mathf.Lerp(previous, current, percent);
	}

	public IntSyncVar()
		: base(default(SyncTypeSettings))
	{
	}
}
