using FishNet.Connection;
using FishNet.Managing.Server;
using UnityEngine;

namespace FishNet.Object;

public class GlobalPreserveOwnedObjects : MonoBehaviour
{
	private void Awake()
	{
		GetComponent<ServerManager>().Objects.OnPreDestroyClientObjects += Objects_OnPreDestroyClientObjects;
	}

	protected virtual void Objects_OnPreDestroyClientObjects(NetworkConnection conn)
	{
		foreach (NetworkObject @object in conn.Objects)
		{
			@object.RemoveOwnership();
		}
	}
}
