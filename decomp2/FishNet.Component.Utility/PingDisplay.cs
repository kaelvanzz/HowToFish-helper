using FishNet.Managing.Timing;
using UnityEngine;

namespace FishNet.Component.Utility;

[AddComponentMenu("FishNet/Component/PingDisplay")]
public class PingDisplay : MonoBehaviour
{
	private enum Corner
	{
		TopLeft,
		TopRight,
		BottomLeft,
		BottomRight
	}

	[Tooltip("Color for text.")]
	[SerializeField]
	private Color _color = Color.white;

	[Tooltip("Which corner to display ping in.")]
	[SerializeField]
	private Corner _placement = Corner.TopRight;

	[Tooltip("True to show the real ping. False to include tick rate latency within the ping.")]
	[SerializeField]
	private bool _hideTickRate = true;

	private GUIStyle _style = new GUIStyle();

	private void OnGUI()
	{
		if (!InstanceFinder.IsClientStarted)
		{
			return;
		}
		_style.normal.textColor = _color;
		_style.fontSize = 15;
		float num = 85f;
		float num2 = 15f;
		float num3 = 10f;
		float x;
		float y;
		if (_placement == Corner.TopLeft)
		{
			x = 10f;
			y = 10f;
		}
		else if (_placement == Corner.TopRight)
		{
			x = (float)Screen.width - num - num3;
			y = 10f;
		}
		else if (_placement == Corner.BottomLeft)
		{
			x = 10f;
			y = (float)Screen.height - num2 - num3;
		}
		else
		{
			x = (float)Screen.width - num - num3;
			y = (float)Screen.height - num2 - num3;
		}
		TimeManager timeManager = InstanceFinder.TimeManager;
		long num4;
		if (timeManager == null)
		{
			num4 = 0L;
		}
		else
		{
			num4 = timeManager.RoundTripTime;
			long num5 = 0L;
			if (_hideTickRate)
			{
				num5 = (long)(timeManager.TickDelta * 2000.0);
			}
			num4 = (long)Mathf.Max(1f, num4 - num5);
		}
		GUI.Label(new Rect(x, y, num, num2), $"Ping: {num4}ms", _style);
	}
}
