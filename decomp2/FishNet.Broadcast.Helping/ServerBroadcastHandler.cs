using System;
using System.Collections.Generic;
using FishNet.Serializing;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;

namespace FishNet.Broadcast.Helping;

internal class ServerBroadcastHandler<T> : BroadcastHandlerBase
{
	private List<Action<T, Channel>> _handlers = new List<Action<T, Channel>>();

	public override bool RequireAuthentication => false;

	public override void InvokeHandlers(PooledReader reader, Channel channel)
	{
		T arg = reader.Read<T>();
		for (IteratingIndex = 0; IteratingIndex < _handlers.Count; IteratingIndex++)
		{
			Action<T, Channel> action = _handlers[IteratingIndex];
			if (action != null)
			{
				action(arg, channel);
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
		Action<T, Channel> item = (Action<T, Channel>)obj;
		_handlers.AddUnique(item);
	}

	public override void UnregisterHandler(object obj)
	{
		Action<T, Channel> item = (Action<T, Channel>)obj;
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
