using System;
using FishNet.Managing.Logging;

namespace FishNet.Object;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public class TargetRpcAttribute : RpcAttribute
{
	public bool ExcludeServer;

	public bool ValidateTarget = true;

	public LoggingType Logging = LoggingType.Warning;
}
