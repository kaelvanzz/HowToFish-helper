using System.Collections.Generic;
using FishNet.Serializing;
using GameKit.Dependencies.Utilities;

namespace FishNet.Managing.Server;

internal static class ConnectedClientsBroadcastSerializers
{
	public static void WriteConnectedClientsBroadcast(this Writer writer, ConnectedClientsBroadcast value)
	{
		writer.WriteList(value.Values);
	}

	public static ConnectedClientsBroadcast ReadConnectedClientsBroadcast(this Reader reader)
	{
		List<int> collection = CollectionCaches<int>.RetrieveList();
		reader.ReadList(ref collection);
		ConnectedClientsBroadcast result = default(ConnectedClientsBroadcast);
		result.Values = collection;
		return result;
	}
}
