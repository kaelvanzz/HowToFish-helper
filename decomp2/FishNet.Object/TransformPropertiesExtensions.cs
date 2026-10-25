using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Object;

public static class TransformPropertiesExtensions
{
	public static TransformProperties CreateDirections(this TransformProperties prevProperties, TransformProperties nextProperties, uint divisor = 1u)
	{
		Vector3 position = (nextProperties.Position - prevProperties.Position) / divisor;
		Quaternion rotation = nextProperties.Rotation.Subtract(prevProperties.Rotation);
		if (divisor > 1)
		{
			rotation = Quaternion.Lerp(t: 1f / (float)divisor, a: Quaternion.identity, b: nextProperties.Rotation);
		}
		Vector3 localScale = (nextProperties.Scale - prevProperties.Scale) / divisor;
		return new TransformProperties(position, rotation, localScale);
	}

	public static void SetWorldProperties(this TransformPropertiesCls tp, Transform t)
	{
		tp.Position = t.position;
		tp.Rotation = t.rotation;
		tp.LocalScale = t.localScale;
	}

	public static void SetWorldProperties(this TransformProperties tp, Transform t)
	{
		tp.Position = t.position;
		tp.Rotation = t.rotation;
		tp.Scale = t.localScale;
	}
}
