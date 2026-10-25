using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Object.Prediction;

public class MoveRatesCls : IResettable
{
	private MoveRates _moveRates;

	public float Position => _moveRates.Position;

	public float Rotation => _moveRates.Rotation;

	public float Scale => _moveRates.Scale;

	public float TimeRemaining => _moveRates.TimeRemaining;

	public bool IsPositionInstantValue => _moveRates.IsPositionInstantValue;

	public bool IsRotationInstantValue => _moveRates.IsRotationInstantValue;

	public bool IsScaleInstantValue => _moveRates.IsScaleInstantValue;

	public bool IsValid => _moveRates.IsValid;

	public MoveRatesCls(float value)
	{
		_moveRates = new MoveRates(value);
	}

	public MoveRatesCls(float position, float rotation)
	{
		_moveRates = new MoveRates(position, rotation);
	}

	public MoveRatesCls(float position, float rotation, float scale)
	{
		_moveRates = new MoveRates(position, rotation, scale);
	}

	public MoveRatesCls(float position, float rotation, float scale, float timeRemaining)
	{
		_moveRates = new MoveRates(position, rotation, scale, timeRemaining);
	}

	public MoveRatesCls()
	{
		_moveRates.ResetState();
	}

	public void SetInstantRates()
	{
		_moveRates.SetInstantRates();
	}

	public void Update(float value)
	{
		_moveRates.Update(value);
	}

	public void Update(float position, float rotation, float scale)
	{
		_moveRates.Update(position, rotation, scale);
	}

	public void Update(float position, float rotation, float scale, float timeRemaining)
	{
		_moveRates.Update(position, rotation, scale, timeRemaining);
	}

	public void Update(MoveRatesCls mr)
	{
		_moveRates.Update(mr.Position, mr.Rotation, mr.Scale);
	}

	public void Move(Transform movingTransform, TransformProperties goalProperties, float delta, bool useWorldSpace)
	{
		_moveRates.Move(movingTransform, goalProperties, delta, useWorldSpace);
	}

	public void Move(Transform movingTransform, TransformProperties goalProperties, TransformPropertiesFlag movedProperties, float delta, bool useWorldSpace)
	{
		_moveRates.Move(movingTransform, goalProperties, movedProperties, delta, useWorldSpace);
	}

	public void ResetState()
	{
		_moveRates.ResetState();
	}

	public void InitializeState()
	{
	}
}
