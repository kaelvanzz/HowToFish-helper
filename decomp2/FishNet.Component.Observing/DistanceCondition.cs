using System;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Observing;
using UnityEngine;

namespace FishNet.Component.Observing;

[CreateAssetMenu(menuName = "FishNet/Observers/Distance Condition", fileName = "New Distance Condition")]
public class DistanceCondition : ObserverCondition
{
	[Tooltip("Maximum distance a client must be within this object to see it.")]
	[SerializeField]
	private float _maximumDistance = 100f;

	[Tooltip("Additional percent of distance client must be until this object is hidden. For example, if distance was 100f and percent was 0.5f the client must be 150f units away before this object is hidden again. This can be useful for keeping objects from regularly appearing and disappearing.")]
	[Range(0f, 1f)]
	[SerializeField]
	private float _hideDistancePercent = 0.1f;

	private float _sqrMaximumDistance;

	private float _sqrHideMaximumDistance;

	[Obsolete("Use Get/SetMaximumDistance.")]
	public float MaximumDistance
	{
		get
		{
			return GetMaximumDistance();
		}
		set
		{
			SetMaximumDistance(value);
		}
	}

	public float GetMaximumDistance()
	{
		return _maximumDistance;
	}

	public void SetMaximumDistance(float value)
	{
		_maximumDistance = value;
		_sqrMaximumDistance = _maximumDistance * _maximumDistance;
		float num = _maximumDistance * (1f + _hideDistancePercent);
		_sqrHideMaximumDistance = num * num;
	}

	private void Awake()
	{
		SetMaximumDistance(_maximumDistance);
	}

	public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
	{
		notProcessed = false;
		float num = (currentlyAdded ? _sqrHideMaximumDistance : _sqrMaximumDistance);
		Vector3 position = NetworkObject.transform.position;
		foreach (NetworkObject @object in connection.Objects)
		{
			if (Vector3.SqrMagnitude(@object.transform.position - position) <= num)
			{
				return true;
			}
		}
		return false;
	}

	public override ObserverConditionType GetConditionType()
	{
		return ObserverConditionType.Timed;
	}
}
