using System;
using FishNet.Editing;
using FishNet.Managing;
using FishNet.Managing.Statistic;
using FishNet.Managing.Timing;
using GameKit.Dependencies.Utilities.Types;
using UnityEngine;

namespace FishNet.Component.Utility;

[AddComponentMenu("FishNet/Component/BandwidthDisplay")]
public class BandwidthDisplay : MonoBehaviour
{
	private enum Corner
	{
		TopLeft,
		TopRight,
		BottomLeft,
		BottomRight
	}

	public class InOutAverage
	{
		private RingBuffer<ulong> _in;

		private RingBuffer<ulong> _out;

		public InOutAverage(int ticks)
		{
			_in = new RingBuffer<ulong>(ticks);
			_out = new RingBuffer<ulong>(ticks);
		}

		public void AddIn(ulong value)
		{
			_in.Add(value);
		}

		public void AddOut(ulong value)
		{
			_out.Add(value);
		}

		public ulong GetAverage(bool inAverage)
		{
			RingBuffer<ulong> ringBuffer = (inAverage ? _in : _out);
			int count = ringBuffer.Count;
			if (count == 0)
			{
				return 0uL;
			}
			ulong num = 0uL;
			foreach (ulong item in ringBuffer)
			{
				num += item;
			}
			return num / (uint)count;
		}

		public void ResetState()
		{
			_in.Clear();
			_out.Clear();
		}

		public void InitializeState(int capacity)
		{
			_in.Initialize(capacity);
			_out.Initialize(capacity);
		}
	}

	[Header("Misc")]
	[Tooltip("True to operate while in release. This may cause allocations and impact performance.")]
	[SerializeField]
	private bool _runInRelease;

	[Header("Timing")]
	[Tooltip("Number of seconds used to gather data per second. Lower values will show more up to date usage per second while higher values provide a better over-all estimate.")]
	[SerializeField]
	[Range(1f, 255f)]
	private byte _secondsAveraged = 1;

	[Tooltip("How often to update displayed text.")]
	[Range(0f, 10f)]
	[SerializeField]
	private float _updateInterval = 1f;

	[Header("Appearance")]
	[Tooltip("Color for text.")]
	[SerializeField]
	private Color _color = Color.white;

	[Tooltip("Which corner to display network statistics in.")]
	[SerializeField]
	private Corner _placement = Corner.TopRight;

	[Tooltip("True to show outgoing data bytes.")]
	[SerializeField]
	private bool _showOutgoing = true;

	[Tooltip("True to show incoming data bytes.")]
	[SerializeField]
	private bool _showIncoming = true;

	private readonly GUIStyle _style = new GUIStyle();

	private string _clientText;

	private string _serverText;

	private NetworkTrafficStatistics _networkTrafficStatistics;

	private float _nextServerTextUpdateTime;

	private float _nextClientTextUpdateTime;

	private bool _initialized;

	public InOutAverage ClientAverages { get; private set; }

	public InOutAverage ServerAverages { get; private set; }

	public void SetShowOutgoing(bool value)
	{
		_showOutgoing = value;
	}

	public void SetShowIncoming(bool value)
	{
		_showIncoming = value;
	}

	private void Start()
	{
		if (_runInRelease && InstanceFinder.NetworkManager.StatisticsManager.TryGetNetworkTrafficStatistics(out _networkTrafficStatistics))
		{
			if (!_networkTrafficStatistics.UpdateClient && !_networkTrafficStatistics.UpdateServer)
			{
				Debug.LogWarning("StatisticsManager.NetworkTraffic is not updating for client nor server. To see results ensure your NetworkManager has a StatisticsManager component added with the NetworkTraffic values configured.");
				return;
			}
			SetSecondsAveraged(_secondsAveraged);
			_networkTrafficStatistics.OnNetworkTraffic += NetworkTrafficStatistics_OnNetworkTraffic;
			_initialized = true;
		}
	}

	private void OnDestroy()
	{
		if (_networkTrafficStatistics != null)
		{
			_networkTrafficStatistics.OnNetworkTraffic -= NetworkTrafficStatistics_OnNetworkTraffic;
		}
	}

	public void SetSecondsAveraged(byte seconds)
	{
		NetworkManager networkManager = InstanceFinder.NetworkManager;
		if (!(networkManager == null))
		{
			if (seconds <= 0)
			{
				seconds = 1;
			}
			uint num = networkManager.TimeManager.TimeToTicks(seconds, TickRounding.RoundUp);
			if (num == 0)
			{
				num = 60u;
			}
			ClientAverages = new InOutAverage((int)num);
			ServerAverages = new InOutAverage((int)num);
		}
	}

	private void NetworkTrafficStatistics_OnNetworkTraffic(uint tick, BidirectionalNetworkTraffic serverTraffic, BidirectionalNetworkTraffic clientTraffic)
	{
		if (!_initialized)
		{
			return;
		}
		ServerAverages.AddIn(serverTraffic.InboundTraffic.Bytes);
		ServerAverages.AddOut(serverTraffic.OutboundTraffic.Bytes);
		ClientAverages.AddIn(clientTraffic.InboundTraffic.Bytes);
		ClientAverages.AddOut(clientTraffic.OutboundTraffic.Bytes);
		if (!(Time.time < _nextServerTextUpdateTime))
		{
			_nextServerTextUpdateTime = Time.time + _updateInterval;
			string newLine = Environment.NewLine;
			string text = string.Empty;
			if (_showIncoming)
			{
				text = text + "Server In: " + NetworkTrafficStatistics.FormatBytesToLargest(ServerAverages.GetAverage(inAverage: true)) + "/s" + newLine;
			}
			if (_showOutgoing)
			{
				text = text + "Server Out: " + NetworkTrafficStatistics.FormatBytesToLargest(ServerAverages.GetAverage(inAverage: false)) + "/s" + newLine;
			}
			_serverText = text;
			text = string.Empty;
			if (_showIncoming)
			{
				text = text + "Client In: " + NetworkTrafficStatistics.FormatBytesToLargest(ClientAverages.GetAverage(inAverage: true)) + "/s" + newLine;
			}
			if (_showOutgoing)
			{
				text = text + "Client Out: " + NetworkTrafficStatistics.FormatBytesToLargest(ClientAverages.GetAverage(inAverage: false)) + "/s" + newLine;
			}
			_clientText = text;
		}
	}

	private void NetworkTraffic_OnClientNetworkTraffic(BidirectionalNetworkTraffic traffic)
	{
		if (!_initialized)
		{
			return;
		}
		ClientAverages.AddIn(traffic.InboundTraffic.Bytes);
		ClientAverages.AddOut(traffic.OutboundTraffic.Bytes);
		if (!(Time.time < _nextClientTextUpdateTime))
		{
			_nextClientTextUpdateTime = Time.time + _updateInterval;
			string newLine = Environment.NewLine;
			string text = string.Empty;
			if (_showIncoming)
			{
				text = text + "Client In: " + NetworkTrafficStatistics.FormatBytesToLargest(ClientAverages.GetAverage(inAverage: true)) + "/s" + newLine;
			}
			if (_showOutgoing)
			{
				text = text + "Client Out: " + NetworkTrafficStatistics.FormatBytesToLargest(ClientAverages.GetAverage(inAverage: false)) + "/s" + newLine;
			}
			_clientText = text;
		}
	}

	private void NetworkTraffic_OnServerNetworkTraffic(BidirectionalNetworkTraffic traffic)
	{
		if (!_initialized)
		{
			return;
		}
		ServerAverages.AddIn(traffic.InboundTraffic.Bytes);
		ServerAverages.AddOut(traffic.OutboundTraffic.Bytes);
		if (!(Time.time < _nextServerTextUpdateTime))
		{
			_nextServerTextUpdateTime = Time.time + _updateInterval;
			string newLine = Environment.NewLine;
			string text = string.Empty;
			if (_showIncoming)
			{
				text = text + "Server In: " + NetworkTrafficStatistics.FormatBytesToLargest(ServerAverages.GetAverage(inAverage: true)) + "/s" + newLine;
			}
			if (_showOutgoing)
			{
				text = text + "Server Out: " + NetworkTrafficStatistics.FormatBytesToLargest(ServerAverages.GetAverage(inAverage: false)) + "/s" + newLine;
			}
			_serverText = text;
		}
	}

	private void OnGUI()
	{
		_style.normal.textColor = _color;
		_style.fontSize = 15;
		float num = 100f;
		float num2 = 0f;
		if (_showIncoming)
		{
			num2 += 15f;
		}
		if (_showOutgoing)
		{
			num2 += 15f;
		}
		bool isClientStarted = InstanceFinder.IsClientStarted;
		bool isServerStarted = InstanceFinder.IsServerStarted;
		if (!isClientStarted)
		{
			ResetCalculationsAndDisplay(forServer: false);
		}
		if (!isServerStarted)
		{
			ResetCalculationsAndDisplay(forServer: true);
		}
		if (isServerStarted && isClientStarted)
		{
			num2 *= 2f;
		}
		float num3 = 10f;
		float x;
		float y;
		if (_placement == Corner.TopLeft)
		{
			x = 10f;
			y = 10f;
			_style.alignment = TextAnchor.UpperLeft;
		}
		else if (_placement == Corner.TopRight)
		{
			x = (float)Screen.width - num - num3;
			y = 10f;
			_style.alignment = TextAnchor.UpperRight;
		}
		else if (_placement == Corner.BottomLeft)
		{
			x = 10f;
			y = (float)Screen.height - num2 - num3;
			_style.alignment = TextAnchor.LowerLeft;
		}
		else
		{
			x = (float)Screen.width - num - num3;
			y = (float)Screen.height - num2 - num3;
			_style.alignment = TextAnchor.LowerRight;
		}
		GUI.Label(new Rect(x, y, num, num2), _clientText + _serverText, _style);
	}

	[ContextMenu("Reset Averages")]
	public void ResetAverages()
	{
		ResetCalculationsAndDisplay(forServer: true);
		ResetCalculationsAndDisplay(forServer: false);
	}

	private void ResetCalculationsAndDisplay(bool forServer)
	{
		if (_initialized)
		{
			if (forServer)
			{
				_serverText = string.Empty;
				ServerAverages.ResetState();
			}
			else
			{
				_clientText = string.Empty;
				ClientAverages.ResetState();
			}
		}
	}
}
