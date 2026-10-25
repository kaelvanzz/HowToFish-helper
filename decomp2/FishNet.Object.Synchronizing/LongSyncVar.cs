using System;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class LongSyncVar : SyncVar<long>, ICustomSync
{
	public object GetSerializedType()
	{
		return typeof(long);
	}

	protected override long Interpolate(long previous, long current, float percent)
	{
		return (long)Mathf.Lerp(previous, current, percent);
	}

	public LongSyncVar()
		: base(default(SyncTypeSettings))
	{
	}
}
