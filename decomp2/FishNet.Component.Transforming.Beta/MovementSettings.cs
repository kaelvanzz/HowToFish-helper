using System;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Component.Transforming.Beta;

[Serializable]
public struct MovementSettings
{
	[Tooltip("True to enable teleport threshold.")]
	public bool EnableTeleport;

	[Tooltip("How far the object must move between ticks to teleport rather than smooth.")]
	[Range(0f, 65535f)]
	public float TeleportThreshold;

	[Tooltip("Amount of adaptive interpolation to use. Adaptive interpolation increases interpolation with the local client's latency. Lower values of adaptive interpolation results in smaller interpolation increases. In most cases adaptive interpolation is only used with prediction where objects might be affected by other moving objects.")]
	public AdaptiveInterpolationType AdaptiveInterpolationValue;

	[Tooltip("Number of ticks to smooth over when not using adaptive interpolation.")]
	public byte InterpolationValue;

	[Tooltip("Properties to smooth. Any value not selected will become offset with every movement.")]
	public TransformPropertiesFlag SmoothedProperties;

	[Tooltip("True to keep non-smoothed properties at their original localspace every tick. A false value will keep the properties in the same world space as they were before each tick.")]
	public bool SnapNonSmoothedProperties;

	public MovementSettings(bool unityReallyNeedsToSupportParameterlessInitializersOnStructsAlready)
	{
		EnableTeleport = false;
		TeleportThreshold = 0f;
		AdaptiveInterpolationValue = AdaptiveInterpolationType.Off;
		InterpolationValue = 2;
		SmoothedProperties = TransformPropertiesFlag.Everything;
		SnapNonSmoothedProperties = false;
	}
}
