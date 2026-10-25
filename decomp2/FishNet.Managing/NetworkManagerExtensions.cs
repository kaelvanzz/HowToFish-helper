using FishNet.Managing.Logging;
using UnityEngine;

namespace FishNet.Managing;

public static class NetworkManagerExtensions
{
	internal static bool CanLog(this NetworkManager networkManager, LoggingType loggingType)
	{
		if (GetNetworkManager(ref networkManager))
		{
			return networkManager.InternalCanLog(loggingType);
		}
		return false;
	}

	public static void Log(this NetworkManager networkManager, LoggingType loggingType, string value)
	{
		switch (loggingType)
		{
		case LoggingType.Common:
			networkManager.Log(value);
			break;
		case LoggingType.Warning:
			networkManager.LogWarning(value);
			break;
		case LoggingType.Error:
			networkManager.LogError(value);
			break;
		}
	}

	public static void Log(this NetworkManager networkManager, string message)
	{
		if (GetNetworkManager(ref networkManager))
		{
			networkManager.InternalLog(message);
		}
		else
		{
			Debug.Log(message);
		}
	}

	public static void LogWarning(this NetworkManager networkManager, string message)
	{
		if (GetNetworkManager(ref networkManager))
		{
			networkManager.InternalLogWarning(message);
		}
		else
		{
			Debug.LogWarning(message);
		}
	}

	public static void LogError(this NetworkManager networkManager, string message)
	{
		if (GetNetworkManager(ref networkManager))
		{
			networkManager.InternalLogError(message);
		}
		else
		{
			Debug.LogError(message);
		}
	}

	private static bool GetNetworkManager(ref NetworkManager preferredNm)
	{
		if (preferredNm != null)
		{
			return true;
		}
		preferredNm = InstanceFinder.NetworkManager;
		return preferredNm != null;
	}

	public static void Log(string msg)
	{
		((NetworkManager)null).Log(msg);
	}

	public static void LogWarning(string msg)
	{
		((NetworkManager)null).LogWarning(msg);
	}

	public static void LogError(string msg)
	{
		((NetworkManager)null).LogError(msg);
	}

	public static bool CanLog(LoggingType lt)
	{
		return ((NetworkManager)null).CanLog(lt);
	}
}
