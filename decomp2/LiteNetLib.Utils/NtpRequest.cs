using System.Net;
using System.Net.Sockets;

namespace LiteNetLib.Utils;

internal sealed class NtpRequest
{
	private const int ResendTimer = 1000;

	private const int KillTimer = 10000;

	public const int DefaultPort = 123;

	private readonly IPEndPoint _ntpEndPoint;

	private int _resendTime = 1000;

	private int _killTime;

	public bool NeedToKill => _killTime >= 10000;

	public NtpRequest(IPEndPoint endPoint)
	{
		_ntpEndPoint = endPoint;
	}

	public bool Send(Socket socket, int time)
	{
		_resendTime += time;
		_killTime += time;
		if (_resendTime < 1000)
		{
			return false;
		}
		NtpPacket ntpPacket = new NtpPacket();
		try
		{
			return socket.SendTo(ntpPacket.Bytes, 0, ntpPacket.Bytes.Length, SocketFlags.None, _ntpEndPoint) == ntpPacket.Bytes.Length;
		}
		catch
		{
			return false;
		}
	}
}
