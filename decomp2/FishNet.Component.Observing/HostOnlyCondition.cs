using FishNet.Connection;
using FishNet.Observing;
using UnityEngine;

namespace FishNet.Component.Observing;

[CreateAssetMenu(menuName = "FishNet/Observers/Host Only Condition", fileName = "New Host Only Condition")]
public class HostOnlyCondition : ObserverCondition
{
	public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
	{
		notProcessed = false;
		return NetworkObject.ClientManager.Connection == connection;
	}

	public override ObserverConditionType GetConditionType()
	{
		return ObserverConditionType.Normal;
	}
}
