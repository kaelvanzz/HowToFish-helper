using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.Scripting;

namespace FishNet.Object.Prediction;

[Preserve]
public struct MoveRates
{
	public float Position;

	public float Rotation;

	public float Scale;

	public float TimeRemaining;

	public const float UNSET_VALUE = float.NegativeInfinity;

	public const float INSTANT_VALUE = float.PositiveInfinity;

	public bool IsValid { get; private set; }

	public bool IsPositionSet => Position != float.NegativeInfinity;

	public bool IsRotationSet => Rotation != float.NegativeInfinity;

	public bool IsScaleSet => Scale != float.NegativeInfinity;

	public bool IsPositionInstantValue => Position == float.PositiveInfinity;

	public bool IsRotationInstantValue => Rotation == float.PositiveInfinity;

	public bool IsScaleInstantValue => Scale == float.PositiveInfinity;

	public MoveRates(float value)
	{
		this = default(MoveRates);
		Position = value;
		Rotation = value;
		Scale = value;
		IsValid = true;
	}

	public MoveRates(float position, float rotation)
	{
		this = default(MoveRates);
		Position = position;
		Rotation = rotation;
		Scale = float.PositiveInfinity;
		IsValid = true;
	}

	public MoveRates(float position, float rotation, float scale)
	{
		this = default(MoveRates);
		Position = position;
		Rotation = rotation;
		Scale = scale;
		IsValid = true;
	}

	public MoveRates(float position, float rotation, float scale, float timeRemaining)
	{
		Position = position;
		Rotation = rotation;
		Scale = scale;
		TimeRemaining = timeRemaining;
		IsValid = true;
	}

	public void SetInstantRates()
	{
		Update(float.PositiveInfinity);
	}

	public void Update(float value)
	{
		Update(value, value, value);
	}

	public void Update(float position, float rotation, float scale)
	{
		Position = position;
		Rotation = rotation;
		Scale = scale;
		IsValid = true;
	}

	public void Update(float position, float rotation, float scale, float timeRemaining)
	{
		Position = position;
		Rotation = rotation;
		Scale = scale;
		TimeRemaining = timeRemaining;
		IsValid = true;
	}

	public void Update(MoveRates moveRates)
	{
		Update(moveRates.Position, moveRates.Rotation, moveRates.Scale, moveRates.TimeRemaining);
	}

	public void Update(MoveRatesCls moveRates)
	{
		Update(moveRates.Position, moveRates.Rotation, moveRates.Scale, moveRates.TimeRemaining);
	}

	public void ResetState()
	{
		Update(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, 0f);
		IsValid = false;
	}

	public static MoveRates GetWorldMoveRates(Transform from, Transform to, float duration, float teleportThreshold)
	{
		return GetMoveRates(from.position, to.position, from.rotation, to.rotation, from.localScale, to.localScale, duration, teleportThreshold);
	}

	public static MoveRates GetLocalMoveRates(Transform from, Transform to, float duration, float teleportThreshold)
	{
		return GetMoveRates(from.localPosition, to.localPosition, from.localRotation, to.localRotation, from.localScale, to.localScale, duration, teleportThreshold);
	}

	public static MoveRates GetWorldMoveRates(TransformProperties prevValues, Transform t, float duration, float teleportThreshold)
	{
		return GetMoveRates(prevValues.Position, t.position, prevValues.Rotation, t.rotation, prevValues.Scale, t.localScale, duration, teleportThreshold);
	}

	public static MoveRates GetLocalMoveRates(TransformProperties prevValues, Transform t, float duration, float teleportThreshold)
	{
		return GetMoveRates(prevValues.Position, t.localPosition, prevValues.Rotation, t.localRotation, prevValues.Scale, t.localScale, duration, teleportThreshold);
	}

	public static MoveRates GetMoveRates(TransformProperties prevValues, TransformProperties nextValues, float duration, float teleportThreshold)
	{
		return GetMoveRates(prevValues.Position, nextValues.Position, prevValues.Rotation, nextValues.Rotation, prevValues.Scale, nextValues.Scale, duration, teleportThreshold);
	}

	public static MoveRates GetMoveRates(Vector3 fromPosition, Vector3 toPosition, Quaternion fromRotation, Quaternion toRotation, Vector3 fromScale, Vector3 toScale, float duration, float teleportThreshold)
	{
		float rate = toPosition.GetRate(fromPosition, duration, out var distance);
		if (teleportThreshold != float.NegativeInfinity && distance > teleportThreshold)
		{
			return new MoveRates(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, duration);
		}
		float position = rate.SetIfUnderTolerance(0.0001f, float.PositiveInfinity);
		rate = toRotation.GetRate(fromRotation, duration, out var angle);
		float rotation = rate.SetIfUnderTolerance(0.2f, float.PositiveInfinity);
		rate = toScale.GetRate(fromScale, duration, out angle);
		float scale = rate.SetIfUnderTolerance(0.0001f, float.PositiveInfinity);
		return new MoveRates(position, rotation, scale, duration);
	}

	public static float GetMoveRate(Vector3 fromPosition, Vector3 toPosition, float duration, float teleportThreshold)
	{
		float distance;
		float rate = toPosition.GetRate(fromPosition, duration, out distance);
		if (teleportThreshold != float.NegativeInfinity && distance > teleportThreshold)
		{
			return float.PositiveInfinity;
		}
		return rate.SetIfUnderTolerance(0.0001f, float.PositiveInfinity);
	}

	public static float GetMoveRate(Quaternion fromRotation, Quaternion toRotation, float duration)
	{
		float angle;
		return toRotation.GetRate(fromRotation, duration, out angle).SetIfUnderTolerance(0.2f, float.PositiveInfinity);
	}

	public void Move(Transform movingTransform, TransformProperties goalProperties, float delta, bool useWorldSpace)
	{
		if (IsValid)
		{
			Move(movingTransform, TransformPropertiesFlag.Everything, goalProperties.Position, Position, goalProperties.Rotation, Rotation, goalProperties.Scale, Scale, delta, useWorldSpace);
			TimeRemaining -= delta;
		}
	}

	public void Move(Transform movingTransform, TransformProperties goalProperties, TransformPropertiesFlag movedProperties, float delta, bool useWorldSpace)
	{
		if (IsValid)
		{
			Move(movingTransform, movedProperties, goalProperties.Position, Position, goalProperties.Rotation, Rotation, goalProperties.Scale, Scale, delta, useWorldSpace);
			TimeRemaining -= delta;
		}
	}

	public static void Move(Transform movingTransform, TransformPropertiesFlag movedProperties, Vector3 posGoal, float posRate, Quaternion rotGoal, float rotRate, Vector3 scaleGoal, float scaleRate, float delta, bool useWorldSpace)
	{
		bool flag = movedProperties.FastContains(TransformPropertiesFlag.Position);
		bool flag2 = movedProperties.FastContains(TransformPropertiesFlag.Rotation);
		bool num = movedProperties.FastContains(TransformPropertiesFlag.Scale);
		if (useWorldSpace)
		{
			if (flag)
			{
				if (posRate == float.PositiveInfinity)
				{
					movingTransform.position = posGoal;
				}
				else if (posRate != float.NegativeInfinity)
				{
					movingTransform.position = Vector3.MoveTowards(movingTransform.position, posGoal, posRate * delta);
				}
			}
			if (flag2)
			{
				if (rotRate == float.PositiveInfinity)
				{
					movingTransform.rotation = rotGoal;
				}
				else if (rotRate != float.NegativeInfinity)
				{
					movingTransform.rotation = Quaternion.RotateTowards(movingTransform.rotation, rotGoal, rotRate * delta);
				}
			}
		}
		else
		{
			if (flag)
			{
				if (posRate == float.PositiveInfinity)
				{
					movingTransform.localPosition = posGoal;
				}
				else if (posRate != float.NegativeInfinity)
				{
					movingTransform.localPosition = Vector3.MoveTowards(movingTransform.localPosition, posGoal, posRate * delta);
				}
			}
			if (flag2)
			{
				if (rotRate == float.PositiveInfinity)
				{
					movingTransform.localRotation = rotGoal;
				}
				else if (rotRate != float.NegativeInfinity)
				{
					movingTransform.localRotation = Quaternion.RotateTowards(movingTransform.localRotation, rotGoal, rotRate * delta);
				}
			}
		}
		if (num)
		{
			if (scaleRate == float.PositiveInfinity)
			{
				movingTransform.localScale = scaleGoal;
			}
			else if (scaleRate != float.NegativeInfinity)
			{
				movingTransform.localScale = Vector3.MoveTowards(movingTransform.localScale, scaleGoal, scaleRate * delta);
			}
		}
	}
}
