using System;
using FishNet.Managing.Logging;

namespace FishNet.Object;

[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public class ServerAttribute : Attribute
{
	public LoggingType Logging = LoggingType.Warning;

	public bool UseIsStarted;
}
