using GameKit.Dependencies.Utilities;

namespace FishNet.Editing;

internal class ProfiledTickData : IResettable
{
	public uint Tick;

	public BidirectionalNetworkTraffic ServerTraffic;

	public BidirectionalNetworkTraffic ClientTraffic;

	public bool TryInitialize(uint tick, BidirectionalNetworkTraffic serverTraffic, BidirectionalNetworkTraffic clientTraffic)
	{
		Tick = tick;
		ServerTraffic = serverTraffic.CloneUsingCache();
		ClientTraffic = clientTraffic.CloneUsingCache();
		if (ServerTraffic != null)
		{
			return ClientTraffic != null;
		}
		return false;
	}

	public void ResetState()
	{
		Tick = 0u;
		ResettableObjectCaches<BidirectionalNetworkTraffic>.StoreAndDefault(ref ServerTraffic);
		ResettableObjectCaches<BidirectionalNetworkTraffic>.StoreAndDefault(ref ClientTraffic);
	}

	public void InitializeState()
	{
	}
}
