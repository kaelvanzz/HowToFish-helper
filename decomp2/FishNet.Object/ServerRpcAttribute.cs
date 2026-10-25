using System;
using FishNet.Managing.Logging;

namespace FishNet.Object;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public class ServerRpcAttribute : RpcAttribute
{
	public bool RequireOwnership = true;

	public LoggingType Logging = LoggingType.Warning;
}
