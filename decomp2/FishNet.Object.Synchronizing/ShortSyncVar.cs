using System;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class ShortSyncVar : SyncVar<short>, ICustomSync
{
	public object GetSerializedType()
	{
		return typeof(short);
	}

	protected override short Interpolate(short previous, short current, float percent)
	{
		return (short)Mathf.Lerp(previous, current, percent);
	}

	public ShortSyncVar()
		: base(default(SyncTypeSettings))
	{
	}
}
