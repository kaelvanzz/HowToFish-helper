using FishNet.Managing.Predicting;
using UnityEngine;

namespace FishNet.Component.Prediction;

public class OfflineRigidbody : MonoBehaviour
{
	[Tooltip("Type of prediction movement which is being used.")]
	[SerializeField]
	private RigidbodyType _rigidbodyType;

	[Tooltip("True to also get rigidbody components within children.")]
	[SerializeField]
	private bool _getInChildren;

	private RigidbodyPauser _rigidbodyPauser = new RigidbodyPauser();

	private PredictionManager _predictionManager;

	private void Awake()
	{
		InitializeOnce();
	}

	private void OnDestroy()
	{
		ChangeSubscription(subscribe: false);
	}

	private void InitializeOnce()
	{
		_predictionManager = InstanceFinder.PredictionManager;
		UpdateRigidbodies();
		ChangeSubscription(subscribe: true);
	}

	public void SetPredictionManager(PredictionManager pm)
	{
		if (!(pm == _predictionManager))
		{
			ChangeSubscription(subscribe: false);
			_predictionManager = pm;
			ChangeSubscription(subscribe: true);
		}
	}

	public void UpdateRigidbodies()
	{
		_rigidbodyPauser.UpdateRigidbodies(base.transform, _rigidbodyType, _getInChildren);
	}

	private void ChangeSubscription(bool subscribe)
	{
		if (!(_predictionManager == null))
		{
			if (subscribe)
			{
				_predictionManager.OnPreReconcile += _predictionManager_OnPreReconcile;
				_predictionManager.OnPostReconcile += _predictionManager_OnPostReconcile;
			}
			else
			{
				_predictionManager.OnPreReconcile -= _predictionManager_OnPreReconcile;
				_predictionManager.OnPostReconcile -= _predictionManager_OnPostReconcile;
			}
		}
	}

	private void _predictionManager_OnPreReconcile(uint clientTick, uint serverTick)
	{
		_rigidbodyPauser.Pause();
	}

	private void _predictionManager_OnPostReconcile(uint clientTick, uint serverTick)
	{
		_rigidbodyPauser.Unpause();
	}
}
