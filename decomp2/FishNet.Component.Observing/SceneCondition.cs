using FishNet.Connection;
using FishNet.Observing;
using UnityEngine;

namespace FishNet.Component.Observing;

[CreateAssetMenu(menuName = "FishNet/Observers/Scene Condition", fileName = "New Scene Condition")]
public class SceneCondition : ObserverCondition
{
	public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
	{
		notProcessed = false;
		if (NetworkObject == null || connection == null)
		{
			return false;
		}
		return connection.Scenes.Contains(NetworkObject.gameObject.scene);
	}

	public override ObserverConditionType GetConditionType()
	{
		return ObserverConditionType.Normal;
	}
}
