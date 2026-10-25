using System;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class ULongSyncVar : SyncVar<ulong>, ICustomSync
{
	public object GetSerializedType()
	{
		return typeof(ulong);
	}

	protected override ulong Interpolate(ulong previous, ulong current, float percent)
	{
		return (ulong)Mathf.Lerp(previous, current, percent);
	}

	public ULongSyncVar()
		: base(default(SyncTypeSettings))
	{
	}
}
