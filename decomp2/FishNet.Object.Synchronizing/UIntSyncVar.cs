using System;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class UIntSyncVar : SyncVar<uint>, ICustomSync
{
	public object GetSerializedType()
	{
		return typeof(uint);
	}

	protected override uint Interpolate(uint previous, uint current, float percent)
	{
		return (uint)Mathf.Lerp(previous, current, percent);
	}

	public UIntSyncVar()
		: base(default(SyncTypeSettings))
	{
	}
}
