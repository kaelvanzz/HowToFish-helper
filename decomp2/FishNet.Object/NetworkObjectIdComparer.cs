using System.Collections.Generic;

namespace FishNet.Object;

public class NetworkObjectIdComparer : IEqualityComparer<NetworkObject>
{
	public bool Equals(NetworkObject valueA, NetworkObject valueB)
	{
		bool flag = (object)valueA == null;
		bool flag2 = (object)valueB == null;
		if (flag != flag2)
		{
			return false;
		}
		if (flag && flag2)
		{
			return true;
		}
		return valueA.ObjectId == valueB.ObjectId;
	}

	public int GetHashCode(NetworkObject obj)
	{
		return obj.ObjectId;
	}
}
