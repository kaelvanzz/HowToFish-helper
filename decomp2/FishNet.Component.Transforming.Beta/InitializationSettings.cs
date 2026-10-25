using System;
using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.Transforming.Beta;

[Serializable]
public struct InitializationSettings
{
	[Tooltip("While this script is typically placed on a nested graphical object, the targetTransform would be the object which moves every tick; the TargetTransform can be the same object this script resides but may not be a rigidbody if true;")]
	[SerializeField]
	public Transform TargetTransform;

	[NonSerialized]
	[Tooltip("The transform which is smoothed.")]
	internal Transform GraphicalTransform;

	[Tooltip("True to detach this object from it's parent on client start.")]
	public bool DetachOnStart;

	[Tooltip("True to re-attach this object to it's parent on client stop.")]
	public bool AttachOnStop;

	[NonSerialized]
	internal NetworkBehaviour InitializingNetworkBehaviour;

	[NonSerialized]
	internal TimeManager InitializingTimeManager;

	[Tooltip("True to begin moving soon as movement data becomes available. Movement will ease in until at interpolation value. False to prevent movement until movement data count meet interpolation.")]
	public bool MoveImmediately => false;

	public void SetNetworkedRuntimeValues(NetworkBehaviour initializingNetworkBehaviour, Transform graphicalTransform)
	{
		InitializingNetworkBehaviour = initializingNetworkBehaviour;
		GraphicalTransform = graphicalTransform;
		InitializingTimeManager = initializingNetworkBehaviour.TimeManager;
	}

	public void SetOfflineRuntimeValues(TimeManager timeManager, Transform graphicalTransform)
	{
		InitializingNetworkBehaviour = null;
		GraphicalTransform = graphicalTransform;
		InitializingTimeManager = timeManager;
	}
}
