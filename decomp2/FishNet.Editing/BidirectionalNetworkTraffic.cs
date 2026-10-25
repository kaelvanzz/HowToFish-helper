using FishNet.Managing;
using GameKit.Dependencies.Utilities;

namespace FishNet.Editing;

public class BidirectionalNetworkTraffic : IResettable
{
	internal NetworkTraffic InboundTraffic;

	internal NetworkTraffic OutboundTraffic;

	public BidirectionalNetworkTraffic CloneUsingCache()
	{
		if (InboundTraffic == null)
		{
			NetworkManagerExtensions.LogError("One or more NetworkTraffic values is null. BidirectionalNetworkTraffic cannot be cloned.");
			return null;
		}
		BidirectionalNetworkTraffic bidirectionalNetworkTraffic = ResettableObjectCaches<BidirectionalNetworkTraffic>.Retrieve();
		bidirectionalNetworkTraffic.InboundTraffic = InboundTraffic;
		bidirectionalNetworkTraffic.OutboundTraffic = OutboundTraffic;
		return bidirectionalNetworkTraffic;
	}

	public void Reinitialize()
	{
		ResetState();
		InitializeState();
	}

	public void ResetState()
	{
		ResettableObjectCaches<NetworkTraffic>.StoreAndDefault(ref InboundTraffic);
		ResettableObjectCaches<NetworkTraffic>.StoreAndDefault(ref OutboundTraffic);
	}

	public void InitializeState()
	{
		InboundTraffic = ResettableObjectCaches<NetworkTraffic>.Retrieve();
		OutboundTraffic = ResettableObjectCaches<NetworkTraffic>.Retrieve();
	}
}
