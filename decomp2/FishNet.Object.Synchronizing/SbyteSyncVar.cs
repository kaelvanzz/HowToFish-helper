using System;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class SbyteSyncVar : SyncVar<sbyte>, ICustomSync
{
	public object GetSerializedType()
	{
		return typeof(sbyte);
	}

	protected override sbyte Interpolate(sbyte previous, sbyte current, float percent)
	{
		return (sbyte)Mathf.Lerp(previous, current, percent);
	}

	public SbyteSyncVar()
		: base(default(SyncTypeSettings))
	{
	}
}
