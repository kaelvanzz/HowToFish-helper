using System;
using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;

namespace FishNet.Utility.Template;

public abstract class TickNetworkBehaviour : NetworkBehaviour
{
	[Serializable]
	[Flags]
	public enum TickCallback : uint
	{
		None = 0u,
		PreTick = 1u,
		Tick = 2u,
		PostTick = 4u,
		Update = 8u,
		LateUpdate = 0x10u,
		Everything = uint.MaxValue
	}

	[Tooltip("Tick callbacks to use.")]
	[SerializeField]
	private TickCallback _tickCallbacks = TickCallback.Tick | TickCallback.PostTick;

	private bool _subscribed;

	private TimeManager _timeManager;

	private bool NetworkInitialize___EarlyFishNet_002EUtility_002ETemplate_002ETickNetworkBehaviourFishNet_002ERuntime_002Edll_Excuted;

	private bool NetworkInitialize___LateFishNet_002EUtility_002ETemplate_002ETickNetworkBehaviourFishNet_002ERuntime_002Edll_Excuted;

	internal override void OnStartNetwork_Internal()
	{
		_timeManager = base.TimeManager;
		ChangeSubscriptions(subscribe: true);
		base.OnStartNetwork_Internal();
	}

	internal override void OnStopNetwork_Internal()
	{
		ChangeSubscriptions(subscribe: false);
		base.OnStopNetwork_Internal();
	}

	public void SetTickCallbacks(TickCallback value)
	{
		ChangeSubscriptions(subscribe: false);
		_tickCallbacks = value;
		if (value != 0)
		{
			ChangeSubscriptions(subscribe: true);
		}
	}

	private void ChangeSubscriptions(bool subscribe)
	{
		TimeManager timeManager = _timeManager;
		if (timeManager == null || subscribe == _subscribed)
		{
			return;
		}
		_subscribed = subscribe;
		if (subscribe)
		{
			if (TickCallbackFastContains(_tickCallbacks, TickCallback.PreTick))
			{
				timeManager.OnPreTick += TimeManager_OnPreTick;
			}
			if (TickCallbackFastContains(_tickCallbacks, TickCallback.Tick))
			{
				timeManager.OnTick += TimeManager_OnTick;
			}
			if (TickCallbackFastContains(_tickCallbacks, TickCallback.PostTick))
			{
				timeManager.OnPostTick += TimeManager_OnPostTick;
			}
			if (TickCallbackFastContains(_tickCallbacks, TickCallback.Update))
			{
				timeManager.OnUpdate += TimeManager_OnUpdate;
			}
			if (TickCallbackFastContains(_tickCallbacks, TickCallback.LateUpdate))
			{
				timeManager.OnLateUpdate += TimeManager_OnLateUpdate;
			}
		}
		else
		{
			if (TickCallbackFastContains(_tickCallbacks, TickCallback.PreTick))
			{
				timeManager.OnPreTick -= TimeManager_OnPreTick;
			}
			if (TickCallbackFastContains(_tickCallbacks, TickCallback.Tick))
			{
				timeManager.OnTick -= TimeManager_OnTick;
			}
			if (TickCallbackFastContains(_tickCallbacks, TickCallback.PostTick))
			{
				timeManager.OnPostTick -= TimeManager_OnPostTick;
			}
			if (TickCallbackFastContains(_tickCallbacks, TickCallback.Update))
			{
				timeManager.OnUpdate -= TimeManager_OnUpdate;
			}
			if (TickCallbackFastContains(_tickCallbacks, TickCallback.LateUpdate))
			{
				timeManager.OnLateUpdate -= TimeManager_OnLateUpdate;
			}
		}
	}

	protected virtual void TimeManager_OnPreTick()
	{
	}

	protected virtual void TimeManager_OnTick()
	{
	}

	protected virtual void TimeManager_OnPostTick()
	{
	}

	protected virtual void TimeManager_OnUpdate()
	{
	}

	protected virtual void TimeManager_OnLateUpdate()
	{
	}

	private bool TickCallbackFastContains(TickCallback whole, TickCallback part)
	{
		return (whole & part) == part;
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyFishNet_002EUtility_002ETemplate_002ETickNetworkBehaviourFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___EarlyFishNet_002EUtility_002ETemplate_002ETickNetworkBehaviourFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateFishNet_002EUtility_002ETemplate_002ETickNetworkBehaviourFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___LateFishNet_002EUtility_002ETemplate_002ETickNetworkBehaviourFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	public virtual void Awake()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}
}
