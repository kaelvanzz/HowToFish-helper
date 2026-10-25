using System;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class Vector3IntSyncVar : SyncVar<Vector3Int>, ICustomSync
{
	public object GetSerializedType()
	{
		return typeof(Vector3Int);
	}

	protected override Vector3Int Interpolate(Vector3Int previous, Vector3Int current, float percent)
	{
		int x = (int)Mathf.Lerp(previous.x, current.x, percent);
		int y = (int)Mathf.Lerp(previous.y, current.y, percent);
		int z = (int)Mathf.Lerp(previous.z, current.z, percent);
		return new Vector3Int(x, y, z);
	}

	public Vector3IntSyncVar()
		: base(default(SyncTypeSettings))
	{
	}
}
