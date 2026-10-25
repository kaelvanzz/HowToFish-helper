using System;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class UShortSyncVar : SyncVar<ushort>, ICustomSync
{
	public object GetSerializedType()
	{
		return typeof(ushort);
	}

	protected override ushort Interpolate(ushort previous, ushort current, float percent)
	{
		return (ushort)Mathf.Lerp((int)previous, (int)current, percent);
	}

	public UShortSyncVar()
		: base(default(SyncTypeSettings))
	{
	}
}
