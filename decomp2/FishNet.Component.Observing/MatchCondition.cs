using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Observing;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Component.Observing;

[CreateAssetMenu(menuName = "FishNet/Observers/Match Condition", fileName = "New Match Condition")]
public class MatchCondition : ObserverCondition
{
	public class ConditionCollections
	{
		public Dictionary<int, HashSet<NetworkConnection>> MatchConnections = new Dictionary<int, HashSet<NetworkConnection>>();

		public Dictionary<NetworkConnection, HashSet<int>> ConnectionMatches = new Dictionary<NetworkConnection, HashSet<int>>();

		public Dictionary<int, HashSet<NetworkObject>> MatchObjects = new Dictionary<int, HashSet<NetworkObject>>();

		public Dictionary<NetworkObject, HashSet<int>> ObjectMatches = new Dictionary<NetworkObject, HashSet<int>>();
	}

	private static Dictionary<NetworkManager, ConditionCollections> _collections = new Dictionary<NetworkManager, ConditionCollections>();

	internal static void StoreCollections(NetworkManager manager)
	{
		if (!_collections.TryGetValue(manager, out var value))
		{
			return;
		}
		foreach (HashSet<int> value2 in value.ObjectMatches.Values)
		{
			CollectionCaches<int>.Store(value2);
		}
		foreach (HashSet<NetworkConnection> value3 in value.MatchConnections.Values)
		{
			CollectionCaches<NetworkConnection>.Store(value3);
		}
		foreach (HashSet<NetworkObject> value4 in value.MatchObjects.Values)
		{
			CollectionCaches<NetworkObject>.Store(value4);
		}
		foreach (HashSet<int> value5 in value.ConnectionMatches.Values)
		{
			CollectionCaches<int>.Store(value5);
		}
		_collections.Remove(manager);
	}

	private static ConditionCollections GetCollections(NetworkManager manager = null)
	{
		if (manager == null)
		{
			manager = InstanceFinder.NetworkManager;
		}
		if (!_collections.TryGetValue(manager, out var value))
		{
			value = new ConditionCollections();
			_collections[manager] = value;
		}
		return value;
	}

	public static Dictionary<int, HashSet<NetworkConnection>> GetMatchConnections(NetworkManager manager = null)
	{
		return GetCollections(manager).MatchConnections;
	}

	public static Dictionary<NetworkConnection, HashSet<int>> GetConnectionMatches(NetworkManager manager = null)
	{
		return GetCollections(manager).ConnectionMatches;
	}

	public static Dictionary<int, HashSet<NetworkObject>> GetMatchObjects(NetworkManager manager = null)
	{
		return GetCollections(manager).MatchObjects;
	}

	public static Dictionary<NetworkObject, HashSet<int>> GetObjectMatches(NetworkManager manager = null)
	{
		return GetCollections(manager).ObjectMatches;
	}

	private static bool AddToMatch(int match, NetworkConnection conn, NetworkManager manager, bool replaceMatch, bool rebuild)
	{
		Dictionary<int, HashSet<NetworkConnection>> matchConnections = GetMatchConnections(manager);
		if (replaceMatch)
		{
			RemoveFromMatchesWithoutRebuild(conn, manager);
		}
		if (!matchConnections.TryGetValueIL2CPP(match, out var value))
		{
			value = CollectionCaches<NetworkConnection>.RetrieveHashSet();
			matchConnections.Add(match, value);
		}
		bool num = value.Add(conn);
		AddToConnectionMatches(conn, match, manager);
		if (num && rebuild)
		{
			GetServerObjects(manager).RebuildObservers();
		}
		return num;
	}

	public static void AddToMatch(int match, NetworkConnection conn, NetworkManager manager = null, bool replaceMatch = false)
	{
		AddToMatch(match, conn, manager, replaceMatch, rebuild: true);
	}

	private static void AddToConnectionMatches(NetworkConnection conn, int match, NetworkManager manager)
	{
		Dictionary<NetworkConnection, HashSet<int>> connectionMatches = GetConnectionMatches(manager);
		if (!connectionMatches.TryGetValueIL2CPP(conn, out var value))
		{
			value = (connectionMatches[conn] = CollectionCaches<int>.RetrieveHashSet());
		}
		value.Add(match);
	}

	public static void AddToMatch(int match, NetworkConnection[] conns, NetworkManager manager = null, bool replaceMatch = false)
	{
		AddToMatch(match, conns.ToList(), manager, replaceMatch);
	}

	public static void AddToMatch(int match, List<NetworkConnection> conns, NetworkManager manager = null, bool replaceMatch = false)
	{
		bool flag = false;
		foreach (NetworkConnection conn in conns)
		{
			flag |= AddToMatch(match, conn, manager, replaceMatch, rebuild: false);
		}
		if (flag)
		{
			GetServerObjects(manager).RebuildObservers();
		}
	}

	private static bool AddToMatch(int match, NetworkObject nob, NetworkManager manager, bool replaceMatch, bool rebuild)
	{
		Dictionary<int, HashSet<NetworkObject>> matchObjects = GetMatchObjects(manager);
		Dictionary<NetworkObject, HashSet<int>> objectMatches = GetObjectMatches(manager);
		if (replaceMatch)
		{
			RemoveFromMatchWithoutRebuild(nob, manager);
		}
		if (!matchObjects.TryGetValueIL2CPP(match, out var value))
		{
			value = CollectionCaches<NetworkObject>.RetrieveHashSet();
			matchObjects.Add(match, value);
		}
		bool num = value.Add(nob);
		if (!objectMatches.TryGetValueIL2CPP(nob, out var value2))
		{
			value2 = CollectionCaches<int>.RetrieveHashSet();
			objectMatches.Add(nob, value2);
		}
		value2.Add(match);
		if (num && rebuild)
		{
			GetServerObjects(manager).RebuildObservers();
		}
		return num;
	}

	public static void AddToMatch(int match, NetworkObject nob, NetworkManager manager = null, bool replaceMatch = false)
	{
		AddToMatch(match, nob, manager, replaceMatch, rebuild: true);
	}

	public static void AddToMatch(int match, NetworkObject[] nobs, NetworkManager manager = null, bool replaceMatch = false)
	{
		AddToMatch(match, nobs.ToList(), manager, replaceMatch);
	}

	public static void AddToMatch(int match, List<NetworkObject> nobs, NetworkManager manager = null, bool replaceMatch = false)
	{
		if (replaceMatch)
		{
			foreach (NetworkObject nob in nobs)
			{
				RemoveFromMatchWithoutRebuild(nob, manager);
			}
		}
		bool flag = false;
		foreach (NetworkObject nob2 in nobs)
		{
			flag |= AddToMatch(match, nob2, manager, replaceMatch, rebuild: false);
		}
		if (flag)
		{
			GetServerObjects(manager).RebuildObservers();
		}
	}

	private static void TryRemoveKey(Dictionary<int, HashSet<NetworkObject>> dict, int key, HashSet<NetworkObject> value)
	{
		bool flag = true;
		if (value != null)
		{
			flag = value.Count == 0;
			if (flag)
			{
				CollectionCaches<NetworkObject>.Store(value);
			}
		}
		if (flag)
		{
			dict.Remove(key);
		}
	}

	private static void TryRemoveKey(Dictionary<int, HashSet<NetworkObject>> dict, int key)
	{
		dict.TryGetValue(key, out var value);
		TryRemoveKey(dict, key, value);
	}

	private static void TryRemoveKey(Dictionary<NetworkObject, HashSet<int>> dict, NetworkObject key, HashSet<int> value)
	{
		bool flag = true;
		if (value != null)
		{
			flag = value.Count == 0;
			if (flag)
			{
				CollectionCaches<int>.Store(value);
			}
		}
		if (flag)
		{
			dict.Remove(key);
		}
	}

	private static void TryRemoveKey(Dictionary<NetworkObject, HashSet<int>> dict, NetworkObject key)
	{
		dict.TryGetValueIL2CPP(key, out var value);
		TryRemoveKey(dict, key, value);
	}

	private static void TryRemoveKey(Dictionary<int, HashSet<NetworkConnection>> dict, int key, HashSet<NetworkConnection> value)
	{
		bool flag = true;
		if (value != null)
		{
			flag = value.Count == 0;
			if (flag)
			{
				CollectionCaches<NetworkConnection>.Store(value);
			}
		}
		if (flag)
		{
			dict.Remove(key);
		}
	}

	private static void TryRemoveKey(Dictionary<int, HashSet<NetworkConnection>> dict, int key)
	{
		dict.TryGetValueIL2CPP(key, out var value);
		TryRemoveKey(dict, key, value);
	}

	private static void TryRemoveKey(Dictionary<NetworkConnection, HashSet<int>> dict, NetworkConnection key, HashSet<int> value)
	{
		bool flag = true;
		if (value != null)
		{
			flag = value.Count == 0;
			if (flag)
			{
				CollectionCaches<int>.Store(value);
			}
		}
		if (flag)
		{
			dict.Remove(key);
		}
	}

	private static void TryRemoveKey(Dictionary<NetworkConnection, HashSet<int>> dict, NetworkConnection key)
	{
		dict.TryGetValueIL2CPP(key, out var value);
		TryRemoveKey(dict, key, value);
	}

	internal static bool RemoveFromMatchesWithoutRebuild(NetworkConnection conn, NetworkManager manager)
	{
		Dictionary<NetworkConnection, HashSet<int>> connectionMatches = GetConnectionMatches(manager);
		Dictionary<int, HashSet<NetworkConnection>> matchConnections = GetMatchConnections(manager);
		bool result = false;
		if (connectionMatches.TryGetValueIL2CPP(conn, out var value))
		{
			result = value.Count > 0;
			foreach (int item in value)
			{
				if (matchConnections.TryGetValue(item, out var value2))
				{
					value2.Remove(conn);
					TryRemoveKey(matchConnections, item, value2);
				}
			}
			value.Clear();
			TryRemoveKey(connectionMatches, conn, value);
		}
		return result;
	}

	public static void RemoveFromMatch(NetworkConnection conn, NetworkManager manager)
	{
		if (RemoveFromMatchesWithoutRebuild(conn, manager))
		{
			GetServerObjects(manager).RebuildObservers();
		}
	}

	private static bool RemoveFromMatch(int match, NetworkConnection conn, NetworkManager manager, bool rebuild)
	{
		Dictionary<NetworkConnection, HashSet<int>> connectionMatches = GetConnectionMatches(manager);
		Dictionary<int, HashSet<NetworkConnection>> matchConnections = GetMatchConnections(manager);
		bool flag = false;
		if (matchConnections.TryGetValueIL2CPP(match, out var value))
		{
			flag |= value.Remove(conn);
			if (connectionMatches.TryGetValueIL2CPP(conn, out var value2))
			{
				value2.Remove(match);
				TryRemoveKey(connectionMatches, conn, value2);
			}
			if (flag && rebuild)
			{
				TryRemoveKey(matchConnections, match, value);
				GetServerObjects(manager).RebuildObservers();
			}
		}
		return flag;
	}

	public static bool RemoveFromMatch(int match, NetworkConnection conn, NetworkManager manager = null)
	{
		return RemoveFromMatch(match, conn, manager, rebuild: true);
	}

	public static void RemoveFromMatch(int match, NetworkConnection[] conns, NetworkManager manager)
	{
		RemoveFromMatch(match, conns.ToList(), manager);
	}

	public static void RemoveFromMatch(int match, List<NetworkConnection> conns, NetworkManager manager)
	{
		bool flag = false;
		foreach (NetworkConnection conn in conns)
		{
			flag |= RemoveFromMatch(match, conn, manager, rebuild: false);
		}
		if (flag)
		{
			GetServerObjects(manager).RebuildObservers();
		}
	}

	internal static bool RemoveFromMatchWithoutRebuild(NetworkObject nob, NetworkManager manager)
	{
		Dictionary<NetworkObject, HashSet<int>> objectMatches = GetObjectMatches(manager);
		Dictionary<int, HashSet<NetworkObject>> matchObjects = GetMatchObjects(manager);
		bool result = false;
		if (objectMatches.TryGetValueIL2CPP(nob, out var value))
		{
			result = value.Count > 0;
			foreach (int item in value)
			{
				if (matchObjects.TryGetValue(item, out var value2))
				{
					value2.Remove(nob);
					TryRemoveKey(matchObjects, item, value2);
				}
			}
			value.Clear();
			TryRemoveKey(objectMatches, nob, value);
		}
		return result;
	}

	public static bool RemoveFromMatch(NetworkObject nob, NetworkManager manager = null)
	{
		bool num = RemoveFromMatchWithoutRebuild(nob, manager);
		if (num)
		{
			GetServerObjects(manager).RebuildObservers(nob);
		}
		return num;
	}

	public static void RemoveFromMatch(NetworkObject[] nobs, NetworkManager manager = null)
	{
		RemoveFromMatch(nobs.ToList(), manager);
	}

	public static void RemoveFromMatch(List<NetworkObject> nobs, NetworkManager manager = null)
	{
		bool flag = false;
		foreach (NetworkObject nob in nobs)
		{
			flag |= RemoveFromMatchWithoutRebuild(nob, manager);
		}
		if (flag)
		{
			GetServerObjects(manager).RebuildObservers(nobs);
		}
	}

	public static void RemoveFromMatch(int match, NetworkObject nob, NetworkManager manager = null)
	{
		Dictionary<int, HashSet<NetworkObject>> matchObjects = GetMatchObjects(manager);
		Dictionary<NetworkObject, HashSet<int>> objectMatches = GetObjectMatches(manager);
		if (matchObjects.TryGetValueIL2CPP(match, out var value) && value.Remove(nob))
		{
			if (objectMatches.TryGetValueIL2CPP(nob, out var value2))
			{
				value2.Remove(match);
				TryRemoveKey(objectMatches, nob, value2);
			}
			TryRemoveKey(matchObjects, match, value);
			GetServerObjects(manager).RebuildObservers(nob);
		}
	}

	public static void RemoveFromMatch(int match, NetworkObject[] nobs, NetworkManager manager = null)
	{
		Dictionary<int, HashSet<NetworkObject>> matchObjects = GetMatchObjects(manager);
		Dictionary<NetworkObject, HashSet<int>> objectMatches = GetObjectMatches(manager);
		if (matchObjects.TryGetValueIL2CPP(match, out var value))
		{
			bool flag = false;
			foreach (NetworkObject networkObject in nobs)
			{
				flag |= value.Remove(networkObject);
				objectMatches.Remove(networkObject);
			}
			if (flag)
			{
				TryRemoveKey(matchObjects, match, value);
				GetServerObjects(manager).RebuildObservers(nobs);
			}
		}
	}

	public static void RemoveFromMatch(int match, List<NetworkObject> nobs, NetworkManager manager = null)
	{
		Dictionary<int, HashSet<NetworkObject>> matchObjects = GetMatchObjects(manager);
		Dictionary<NetworkObject, HashSet<int>> objectMatches = GetObjectMatches(manager);
		if (matchObjects.TryGetValueIL2CPP(match, out var value))
		{
			bool flag = false;
			for (int i = 0; i < nobs.Count; i++)
			{
				NetworkObject networkObject = nobs[i];
				flag |= value.Remove(networkObject);
				objectMatches.Remove(networkObject);
			}
			if (flag)
			{
				TryRemoveKey(matchObjects, match, value);
				GetServerObjects(manager).RebuildObservers(nobs);
			}
		}
	}

	public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
	{
		notProcessed = false;
		NetworkConnection owner = NetworkObject.Owner;
		if (owner.IsValid)
		{
			Dictionary<NetworkConnection, HashSet<int>> connectionMatches = GetConnectionMatches(NetworkObject.NetworkManager);
			if (!connectionMatches.TryGetValueIL2CPP(owner, out var value))
			{
				return true;
			}
			if (!connectionMatches.TryGetValue(connection, out var value2))
			{
				return false;
			}
			foreach (int item in value2)
			{
				if (value.Contains(item))
				{
					return true;
				}
			}
			return false;
		}
		Dictionary<NetworkObject, HashSet<int>> objectMatches = GetObjectMatches(NetworkObject.NetworkManager);
		Dictionary<NetworkConnection, HashSet<int>> connectionMatches2 = GetConnectionMatches(NetworkObject.NetworkManager);
		if (!objectMatches.TryGetValueIL2CPP(NetworkObject, out var value3))
		{
			return true;
		}
		if (!connectionMatches2.TryGetValueIL2CPP(connection, out var value4))
		{
			return false;
		}
		foreach (int item2 in value4)
		{
			if (value3.Contains(item2))
			{
				return true;
			}
		}
		return false;
	}

	private static ServerObjects GetServerObjects(NetworkManager manager)
	{
		if (!(manager == null))
		{
			return manager.ServerManager.Objects;
		}
		return InstanceFinder.ServerManager.Objects;
	}

	public override ObserverConditionType GetConditionType()
	{
		return ObserverConditionType.Normal;
	}
}
