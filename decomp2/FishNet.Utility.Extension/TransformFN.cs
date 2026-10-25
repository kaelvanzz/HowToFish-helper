using System;
using FishNet.Documenting;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Utility.Extension;

[APIExclude]
public static class TransformFN
{
	public static TransformProperties GetWorldProperties(this Transform t)
	{
		return new TransformProperties(t.position, t.rotation, t.localScale);
	}

	public static TransformProperties GetWorldProperties(this Transform t, TransformProperties offset)
	{
		TransformProperties result = new TransformProperties(t.position, t.rotation, t.localScale);
		result.Add(offset);
		return result;
	}

	public static TransformPropertiesCls GetWorldPropertiesCls(this Transform t)
	{
		return new TransformPropertiesCls(t.position, t.rotation, t.localScale);
	}

	public static TransformProperties GetLocalProperties(this Transform t)
	{
		return new TransformProperties(t.localPosition, t.localRotation, t.localScale);
	}

	public static TransformPropertiesCls GetLocalPropertiesCls(this Transform t)
	{
		return new TransformPropertiesCls(t.localPosition, t.localRotation, t.localScale);
	}

	[Obsolete("Use TransformPropertiesExtensions.SetWorldProperties.")]
	public static void SetWorldProperties(this TransformPropertiesCls tp, Transform t)
	{
		TransformPropertiesExtensions.SetWorldProperties(tp, t);
	}

	public static void SetTransformOffsets(this Transform t, Transform target, ref Vector3 pos, ref Quaternion rot)
	{
		if (!(target == null))
		{
			pos = target.position - t.position;
			rot = target.rotation * Quaternion.Inverse(t.rotation);
		}
	}

	public static TransformProperties GetTransformOffsets(this Transform t, Transform target)
	{
		if (target == null)
		{
			return default(TransformProperties);
		}
		return new TransformProperties(target.position - t.position, target.rotation * Quaternion.Inverse(t.rotation), target.localScale - t.localScale);
	}

	public static void SetLocalProperties(this Transform t, TransformPropertiesCls tp)
	{
		t.localPosition = tp.Position;
		t.localRotation = tp.Rotation;
		t.localScale = tp.LocalScale;
	}

	public static void SetLocalProperties(this Transform t, TransformProperties tp)
	{
		t.localPosition = tp.Position;
		t.localRotation = tp.Rotation;
		t.localScale = tp.Scale;
	}

	public static void SetWorldProperties(this Transform t, TransformPropertiesCls tp)
	{
		t.position = tp.Position;
		t.rotation = tp.Rotation;
		t.localScale = tp.LocalScale;
	}

	public static void SetWorldProperties(this Transform t, TransformProperties tp)
	{
		t.position = tp.Position;
		t.rotation = tp.Rotation;
		t.localScale = tp.Scale;
	}

	public static void SetLocalPositionAndRotation(this Transform t, Vector3 pos, Quaternion rot)
	{
		t.localPosition = pos;
		t.localRotation = rot;
	}

	public static void SetLocalPositionRotationAndScale(this Transform t, Vector3 pos, Quaternion rot, Vector3 scale)
	{
		t.localPosition = pos;
		t.localRotation = rot;
		t.localScale = scale;
	}

	public static void SetLocalPositionRotationAndScale(this Transform t, Vector3? nullablePos, Quaternion? nullableRot, Vector3? nullableScale)
	{
		if (nullablePos.HasValue)
		{
			t.localPosition = nullablePos.Value;
		}
		if (nullableRot.HasValue)
		{
			t.localRotation = nullableRot.Value;
		}
		if (nullableScale.HasValue)
		{
			t.localScale = nullableScale.Value;
		}
	}

	public static void SetWorldPositionRotationAndScale(this Transform t, Vector3? nullablePos, Quaternion? nullableRot, Vector3? nullableScale)
	{
		if (nullablePos.HasValue)
		{
			t.position = nullablePos.Value;
		}
		if (nullableRot.HasValue)
		{
			t.rotation = nullableRot.Value;
		}
		if (nullableScale.HasValue)
		{
			t.localScale = nullableScale.Value;
		}
	}

	public static void OutLocalPropertyValues(this Transform t, Vector3? nullablePos, Quaternion? nullableRot, Vector3? nullableScale, out Vector3 pos, out Quaternion rot, out Vector3 scale)
	{
		pos = ((!nullablePos.HasValue) ? t.localPosition : nullablePos.Value);
		rot = ((!nullableRot.HasValue) ? t.localRotation : nullableRot.Value);
		scale = ((!nullableScale.HasValue) ? t.localScale : nullableScale.Value);
	}

	public static void OutWorldPropertyValues(this Transform t, Vector3? nullablePos, Quaternion? nullableRot, Vector3? nullableScale, out Vector3 pos, out Quaternion rot, out Vector3 scale)
	{
		pos = ((!nullablePos.HasValue) ? t.position : nullablePos.Value);
		rot = ((!nullableRot.HasValue) ? t.rotation : nullableRot.Value);
		scale = ((!nullableScale.HasValue) ? t.localScale : nullableScale.Value);
	}
}
