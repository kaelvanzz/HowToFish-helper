using System;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class Vector2IntSyncVar : SyncVar<Vector2Int>, ICustomSync
{
	public object GetSerializedType()
	{
		return typeof(Vector2);
	}

	protected override Vector2Int Interpolate(Vector2Int previous, Vector2Int current, float percent)
	{
		int x = (int)Mathf.Lerp(previous.x, current.x, percent);
		int y = (int)Mathf.Lerp(previous.y, current.y, percent);
		return new Vector2Int(x, y);
	}

	public Vector2IntSyncVar()
		: base(default(SyncTypeSettings))
	{
	}
}
