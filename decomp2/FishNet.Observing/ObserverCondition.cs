using FishNet.Connection;
using FishNet.Object;
using GameKit.Dependencies.Utilities.Types;
using UnityEngine;

namespace FishNet.Observing;

public abstract class ObserverCondition : ScriptableObject, IOrderable
{
	[HideInInspector]
	public NetworkObject NetworkObject;

	[Tooltip("Order in which conditions are added to the NetworkObserver. Lower values will added first, resulting in the condition being checked first. Timed conditions will never check before non-timed conditions.")]
	[SerializeField]
	[Range(-128f, 127f)]
	private sbyte _addOrder;

	[Tooltip("Setting this to true can save performance on conditions which do change settings or store data at runtime. This feature does not function yet but you may set values now for future implementation.")]
	[SerializeField]
	private bool _isConstant;

	private bool _isEnabled = true;

	public int Order => _addOrder;

	public bool IsConstant => _isConstant;

	public bool GetIsEnabled()
	{
		return _isEnabled;
	}

	public void SetIsEnabled(bool value)
	{
		if (value != GetIsEnabled())
		{
			_isEnabled = value;
			if (!(NetworkObject == null))
			{
				(NetworkObject?.ServerManager?.Objects)?.RebuildObservers(NetworkObject);
			}
		}
	}

	public virtual void Initialize(NetworkObject networkObject)
	{
		NetworkObject = networkObject;
	}

	public virtual void Deinitialize(bool destroyed)
	{
		NetworkObject = null;
	}

	public abstract bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed);

	public abstract ObserverConditionType GetConditionType();
}
