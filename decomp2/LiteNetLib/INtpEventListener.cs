using LiteNetLib.Utils;

namespace LiteNetLib;

public interface INtpEventListener
{
	void OnNtpResponse(NtpPacket packet);
}
