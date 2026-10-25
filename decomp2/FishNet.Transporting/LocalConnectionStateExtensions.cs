namespace FishNet.Transporting;

public static class LocalConnectionStateExtensions
{
	public static bool IsStoppedOrStopping(this LocalConnectionState connectionState)
	{
		if (connectionState != LocalConnectionState.Stopped)
		{
			return connectionState == LocalConnectionState.Stopping;
		}
		return true;
	}

	public static bool IsStartedOrStarting(this LocalConnectionState connectionState)
	{
		if (connectionState != LocalConnectionState.Started)
		{
			return connectionState == LocalConnectionState.Starting;
		}
		return true;
	}
}
