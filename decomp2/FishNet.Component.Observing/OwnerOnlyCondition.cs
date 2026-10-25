using FishNet.Connection;
using FishNet.Observing;
using UnityEngine;

namespace FishNet.Component.Observing;

[CreateAssetMenu(menuName = "FishNet/Observers/Owner Only Condition", fileName = "New Owner Only Condition")]
public class OwnerOnlyCondition : ObserverCondition
{
	public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
	{
		notProcessed = false;
		return false;
	}

	public override ObserverConditionType GetConditionType()
	{
		return ObserverConditionType.Normal;
	}
}
