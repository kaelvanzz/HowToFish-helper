using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Serializing;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;

namespace FishNet.Broadcast.Helping;

internal class ClientBroadcastHandler<T> : BroadcastHandlerBase
{
	private List<Action<NetworkConnection, T, Channel>> _handlers = new List<Action<NetworkConnection, T, Channel>>();

	private bool _requireAuthentication;

	public override bool RequireAuthentication => _requireAuthentication;

	public ClientBroadcastHandler(bool requireAuthentication)
	{
		_requireAuthentication = requireAuthentication;
	}

	public override void InvokeHandlers(NetworkConnection conn, PooledReader reader, Channel channel)
	{
		T arg = reader.Read<T>();
		for (IteratingIndex = 0; IteratingIndex < _handlers.Count; IteratingIndex++)
		{
			Action<NetworkConnection, T, Channel> action = _handlers[IteratingIndex];
			if (action != null)
			{
				action(conn, arg, channel);
			}
			else
			{
				_handlers.RemoveAt(IteratingIndex);
				IteratingIndex--;
			}
		}
		IteratingIndex = -1;
	}

	public override void RegisterHandler(object obj)
	{
		Action<NetworkConnection, T, Channel> item = (Action<NetworkConnection, T, Channel>)obj;
		_handlers.AddUnique(item);
	}

	public override void UnregisterHandler(object obj)
	{
		Action<NetworkConnection, T, Channel> item = (Action<NetworkConnection, T, Channel>)obj;
		int num = _handlers.IndexOf(item);
		if (num != -1)
		{
			if (IteratingIndex >= 0 && num <= IteratingIndex)
			{
				IteratingIndex--;
			}
			_handlers.RemoveAt(num);
		}
	}
}
