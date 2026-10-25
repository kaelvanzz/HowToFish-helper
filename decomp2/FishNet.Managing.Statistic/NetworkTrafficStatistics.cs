using System;
using FishNet.Editing;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Managing.Statistic;

[Serializable]
public class NetworkTrafficStatistics
{
	public enum EnabledMode
	{
		Disabled,
		Development,
		Release
	}

	public delegate void NetworkTrafficUpdateDel(uint tick, BidirectionalNetworkTraffic serverTraffic, BidirectionalNetworkTraffic clientTraffic);

	[Tooltip("When to enable network traffic statistics.")]
	[SerializeField]
	private EnabledMode _enableMode;

	[Tooltip("True to update client statistics.")]
	[SerializeField]
	private bool _updateClient;

	[Tooltip("True to update server statistics.")]
	[SerializeField]
	private bool _updateServer;

	private NetworkManager _networkManager;

	private BidirectionalNetworkTraffic _serverTraffic;

	private BidirectionalNetworkTraffic _clientTraffic;

	private static readonly string[] _sizeSuffixes = new string[9] { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

	internal const PacketId UNSPECIFIED_PACKETID = (PacketId)65535;

	public EnabledMode EnableMode => _enableMode;

	public bool UpdateClient
	{
		get
		{
			return _updateClient;
		}
		private set
		{
			_updateClient = value;
		}
	}

	public bool UpdateServer
	{
		get
		{
			return _updateServer;
		}
		private set
		{
			_updateServer = value;
		}
	}

	public event NetworkTrafficUpdateDel OnNetworkTraffic;

	public void SetUpdateClient(bool update)
	{
		UpdateClient = update;
	}

	public void SetUpdateServer(bool update)
	{
		UpdateServer = update;
	}

	internal void InitializeOnce_Internal(NetworkManager manager)
	{
		_networkManager = manager;
		_serverTraffic = ResettableObjectCaches<BidirectionalNetworkTraffic>.Retrieve();
		_clientTraffic = ResettableObjectCaches<BidirectionalNetworkTraffic>.Retrieve();
		manager.TimeManager.OnPreTick += TimeManager_OnPreTick;
	}

	private void TimeManager_OnPreTick()
	{
		long num = _networkManager.TimeManager.LocalTick - 1;
		if (num > 0)
		{
			if (_networkManager.IsClientStarted || _networkManager.IsServerStarted)
			{
				this.OnNetworkTraffic?.Invoke((uint)num, _serverTraffic, _clientTraffic);
			}
			_clientTraffic.Reinitialize();
			_serverTraffic.Reinitialize();
		}
	}

	internal void PacketBundleReceived(bool asServer)
	{
	}

	internal void AddOutboundPacketIdData(PacketId typeSource, string details, int bytes, GameObject gameObject, bool asServer)
	{
		if (bytes > 0)
		{
			GetBidirectionalNetworkTraffic(asServer).OutboundTraffic.AddPacketIdData(typeSource, details, (ulong)bytes, gameObject);
		}
	}

	internal void AddOutboundSocketData(ulong bytes, bool asServer)
	{
		if (bytes > int.MaxValue)
		{
			bytes = 2147483647uL;
		}
		else if (bytes == 0)
		{
			return;
		}
		GetBidirectionalNetworkTraffic(asServer).OutboundTraffic.AddSocketData(bytes);
	}

	internal void AddInboundPacketIdData(PacketId typeSource, string details, int bytes, GameObject gameObject, bool asServer)
	{
		if (bytes > 0)
		{
			GetBidirectionalNetworkTraffic(asServer).InboundTraffic.AddPacketIdData(typeSource, details, (ulong)bytes, gameObject);
		}
	}

	internal void AddInboundSocketData(ulong bytes, bool asServer)
	{
		if (bytes > int.MaxValue)
		{
			bytes = 2147483647uL;
		}
		else if (bytes == 0)
		{
			return;
		}
		GetBidirectionalNetworkTraffic(asServer).InboundTraffic.AddSocketData(bytes);
	}

	private BidirectionalNetworkTraffic GetBidirectionalNetworkTraffic(bool asServer)
	{
		if (!asServer)
		{
			return _clientTraffic;
		}
		return _serverTraffic;
	}

	public static string FormatBytesToLargest(float bytes)
	{
		int decimalPlaces = 2;
		if (bytes < 1f || float.IsInfinity(bytes) || float.IsNaN(bytes))
		{
			return ReturnZero();
		}
		int num = (int)Math.Log(bytes, 1024.0);
		decimal num2 = (decimal)bytes / (decimal)(1L << num * 10);
		if (Math.Round(num2, decimalPlaces) >= 1000m)
		{
			num++;
			num2 /= 1024m;
		}
		if (num == 0)
		{
			decimalPlaces = 0;
		}
		return string.Format("{0:n" + decimalPlaces + "} {1}", num2, _sizeSuffixes[num]);
		string ReturnZero()
		{
			decimalPlaces = 0;
			return string.Format("{0:n" + decimalPlaces + "} B/s", 0);
		}
	}

	public bool IsEnabled()
	{
		if (_enableMode == EnabledMode.Disabled)
		{
			return false;
		}
		return _enableMode == EnabledMode.Release;
	}
}
