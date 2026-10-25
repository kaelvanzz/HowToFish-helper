using System;
using FishNet.Utility.Extension;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Object.Prediction;

[Obsolete("This class will be removed in version 5.")]
internal class LocalTransformTickSmoother : IResettable
{
	private Transform _graphicalObject;

	private float _teleportThreshold;

	private MoveRates _moveRates;

	private bool _preTicked;

	private TransformProperties _gfxInitializedLocalValues;

	private TransformProperties _gfxPreSimulateWorldValues;

	private float _tickDelta;

	private byte _interpolation;

	internal void InitializeOnce(Transform graphicalObject, float teleportDistance, float tickDelta, byte interpolation)
	{
		_gfxInitializedLocalValues = graphicalObject.GetLocalProperties();
		_tickDelta = tickDelta;
		_graphicalObject = graphicalObject;
		_teleportThreshold = teleportDistance * (float)(int)interpolation;
		_interpolation = interpolation;
	}

	internal void Update()
	{
		if (CanSmooth())
		{
			MoveToTarget();
		}
	}

	internal void OnPreTick()
	{
		if (CanSmooth())
		{
			_preTicked = true;
			_gfxPreSimulateWorldValues = _graphicalObject.GetWorldProperties();
		}
	}

	internal void OnPostTick()
	{
		if (CanSmooth())
		{
			if (_preTicked)
			{
				_graphicalObject.SetWorldProperties(_gfxPreSimulateWorldValues);
				SetMoveRates(_gfxInitializedLocalValues, _graphicalObject);
			}
			else
			{
				_graphicalObject.SetLocalProperties(_gfxInitializedLocalValues);
			}
		}
	}

	private bool CanSmooth()
	{
		if (_graphicalObject == null)
		{
			return false;
		}
		return true;
	}

	private void SetMoveRates(TransformProperties prevValues, Transform t)
	{
		float num = _tickDelta * (float)(int)_interpolation;
		if (_interpolation == 1)
		{
			num += Mathf.Max(Time.deltaTime, 0.02f);
		}
		float teleportThreshold = _teleportThreshold;
		_moveRates = MoveRates.GetLocalMoveRates(prevValues, t, num, teleportThreshold);
	}

	private void MoveToTarget()
	{
		_moveRates.Move(_graphicalObject, _gfxInitializedLocalValues, Time.deltaTime, useWorldSpace: false);
	}

	public void ResetState()
	{
		if (_graphicalObject != null)
		{
			_graphicalObject.SetLocalProperties(_gfxInitializedLocalValues);
			_graphicalObject = null;
		}
		_teleportThreshold = 0f;
		_moveRates = default(MoveRates);
		_preTicked = false;
		_gfxInitializedLocalValues = default(TransformProperties);
		_gfxPreSimulateWorldValues = default(TransformProperties);
		_tickDelta = 0f;
		_interpolation = 0;
	}

	public void InitializeState()
	{
	}
}
