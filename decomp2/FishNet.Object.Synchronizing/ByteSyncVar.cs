using System;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class ByteSyncVar : SyncVar<byte>, ICustomSync
{
	public object GetSerializedType()
	{
		return typeof(byte);
	}

	protected override byte Interpolate(byte previous, byte current, float percent)
	{
		return (byte)Mathf.Lerp((int)previous, (int)current, percent);
	}

	public ByteSyncVar()
		: base(default(SyncTypeSettings))
	{
	}
}
