using System;
using FishNet.Managing.Logging;

namespace FishNet.Object;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public class ObserversRpcAttribute : RpcAttribute
{
	public bool ExcludeOwner;

	public bool ExcludeServer;

	public bool BufferLast;

	public LoggingType Logging = LoggingType.Warning;
}
