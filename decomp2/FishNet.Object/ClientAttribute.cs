using System;
using FishNet.Managing.Logging;

namespace FishNet.Object;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public class ClientAttribute : Attribute
{
	public LoggingType Logging = LoggingType.Warning;

	public bool RequireOwnership;

	public bool UseIsStarted;
}
