using System;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.InputSystem;

public class Bow : Weapon
{
	[Serializable]
	private class ChargeSoundBand
	{
		[SerializeField]
		[Min(0f)]
		private float _minimumChargeSpeed;

		[SerializeField]
		private AudioClip[] _clips;

		public float MinimumChargeSpeed => _minimumChargeSpeed;

		public AudioClip[] Clips => _clips;
	}

	[SerializeField]
	[Min(0.01f)]
	[Header("Bow Charge")]
	private float _chargeDuration = 1f;

	[SerializeField]
	private AnimationCurve _projectileSpeedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

	[SerializeField]
	private AnimationCurve _shootVolumeCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

	[SerializeField]
	private AudioSource _chargeSource;

	[SerializeField]
	private ChargeSoundBand[] _chargeSoundBands;

	private float _curCharge;

	private bool _isCharging;

	private Player _chargingHolder;

	private Player _queuedChargeHolder;

	private AnimationState _chargeAnimation;

	private float _originalAnimationSpeed;

	private WrapMode _originalWrapMode;

	private AnimationState _reloadAnimation;

	private float _originalReloadSpeed;

	private bool NetworkInitialize___EarlyBowAssembly_002DCSharp_002Edll_Excuted;

	private bool NetworkInitialize___LateBowAssembly_002DCSharp_002Edll_Excuted;

	protected override bool CanPlayAimIdle => !_isCharging;

	protected override bool IsAnimationBlockingAds
	{
		get
		{
			if (!_isCharging)
			{
				return base.IsAnimationBlockingAds;
			}
			return false;
		}
	}

	public override void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_Bow_Assembly_002DCSharp_002Edll();
		NetworkInitialize___Late();
	}

	protected override float GetProjectileSpeed()
	{
		return base.GetProjectileSpeed() * GetChargeMultiplier();
	}

	protected override int GetShotDamage()
	{
		return Mathf.RoundToInt((float)base.GetShotDamage() * GetChargeMultiplier());
	}

	protected override float GetFireSoundVolumeMultiplier()
	{
		return _shootVolumeCurve.Evaluate(_curCharge);
	}

	protected override float GetFireSoundStrength()
	{
		return _curCharge;
	}

	protected override float GetReloadAudioSequenceSpeedMultiplier()
	{
		return base.Attachments.ChargeSpeed;
	}

	private float GetChargeMultiplier()
	{
		float b = ((_projectileSpeedCurve == null || _projectileSpeedCurve.length == 0) ? _curCharge : _projectileSpeedCurve.Evaluate(_curCharge));
		return Mathf.Max(0f, b);
	}

	public override void PrimaryInput(InputAction.CallbackContext context)
	{
		if (_isCharging || !_holder || !_holder.Owner.IsLocalClient || _holder.BlockInputs)
		{
			return;
		}
		if (base.HasPendingOrActiveReload)
		{
			if (base.AllowsShotQueueing)
			{
				_queuedChargeHolder = _holder;
			}
		}
		else if (base.CanShoot)
		{
			_queuedChargeHolder = null;
			BeginCharge();
			SendChargeState(charging: true);
		}
	}

	public override void PrimaryInputCancel(InputAction.CallbackContext context)
	{
		_queuedChargeHolder = null;
		if (!_isCharging)
		{
			return;
		}
		if (!_holder || _holder != _chargingHolder || !_holder.Owner.IsLocalClient || _holder.BlockInputs)
		{
			CancelCharge();
			return;
		}
		RestoreChargeAnimation();
		PlayIdleAnim();
		if (base.CanShoot)
		{
			Shoot();
		}
		else
		{
			SendChargeState(charging: false);
		}
		ClearCharge();
	}

	protected override void Update()
	{
		float chargeSpeed = base.Attachments.ChargeSpeed;
		if (_reloadAnimation != null)
		{
			_reloadAnimation.speed = _originalReloadSpeed * chargeSpeed;
		}
		if (_isCharging)
		{
			if (!_holder || _holder != _chargingHolder)
			{
				CancelCharge();
			}
			else
			{
				_curCharge = Mathf.Clamp01(_curCharge + Time.deltaTime * chargeSpeed / Mathf.Max(0.01f, _chargeDuration));
				if (_chargeAnimation != null)
				{
					_chargeAnimation.normalizedTime = _curCharge;
					_anim.Sample();
				}
			}
		}
		base.Update();
		if ((bool)_queuedChargeHolder)
		{
			if (!_holder || _holder != _queuedChargeHolder || !_holder.Owner.IsLocalClient || _holder.BlockInputs || !base.AllowsShotQueueing)
			{
				_queuedChargeHolder = null;
			}
			else if (!_isCharging && !base.HasPendingOrActiveReload && base.CanShoot)
			{
				_queuedChargeHolder = null;
				BeginCharge();
				SendChargeState(charging: true);
			}
		}
	}

	public override void ReloadInput(InputAction.CallbackContext context)
	{
		if (!_isCharging)
		{
			base.ReloadInput(context);
		}
	}

	public override void InspectInput(InputAction.CallbackContext context)
	{
		if (!_isCharging)
		{
			base.InspectInput(context);
		}
	}

	public override void OnDrop()
	{
		_queuedChargeHolder = null;
		CancelCharge();
		base.OnDrop();
	}

	protected override void OnDisable()
	{
		_queuedChargeHolder = null;
		CancelCharge();
		base.OnDisable();
	}

	protected override void PlayShotAnimation()
	{
		RestoreChargeAnimation();
		ClearCharge();
		_anim.Stop();
		_anim.Play("Idle");
	}

	private void BeginCharge()
	{
		_isCharging = true;
		_chargingHolder = _holder;
		_curCharge = 0f;
		AudioSequenceManager.CancelAllActiveSequencesFromOwner(base.gameObject);
		if ((bool)_chargeSource)
		{
			_chargeSource.clip = GetRandomChargeClip(base.Attachments.ChargeSpeed);
			_chargeSource.spatialBlend = (_holder.Owner.IsLocalClient ? 0f : 1f);
			if ((bool)_chargeSource.clip)
			{
				_chargeSource.Play();
			}
		}
		_anim.Stop();
		_chargeAnimation = _anim["Fire"];
		if (!(_chargeAnimation == null))
		{
			_originalAnimationSpeed = _chargeAnimation.speed;
			_originalWrapMode = _chargeAnimation.wrapMode;
			_chargeAnimation.speed = 0f;
			_chargeAnimation.wrapMode = WrapMode.ClampForever;
			_anim.Play("Fire");
			_chargeAnimation.normalizedTime = 0f;
			_anim.Sample();
		}
	}

	private AudioClip GetRandomChargeClip(float chargeSpeed)
	{
		ChargeSoundBand chargeSoundBand = null;
		ChargeSoundBand chargeSoundBand2 = null;
		if (_chargeSoundBands == null)
		{
			return null;
		}
		ChargeSoundBand[] chargeSoundBands = _chargeSoundBands;
		foreach (ChargeSoundBand chargeSoundBand3 in chargeSoundBands)
		{
			if (chargeSoundBand3 != null && chargeSoundBand3.Clips != null && !Array.TrueForAll(chargeSoundBand3.Clips, (AudioClip clip) => !clip))
			{
				if (chargeSoundBand2 == null || chargeSoundBand3.MinimumChargeSpeed < chargeSoundBand2.MinimumChargeSpeed)
				{
					chargeSoundBand2 = chargeSoundBand3;
				}
				if (chargeSoundBand3.MinimumChargeSpeed <= chargeSpeed && (chargeSoundBand == null || chargeSoundBand3.MinimumChargeSpeed > chargeSoundBand.MinimumChargeSpeed))
				{
					chargeSoundBand = chargeSoundBand3;
				}
			}
		}
		AudioClip[] array = (chargeSoundBand ?? chargeSoundBand2)?.Clips;
		if (array == null)
		{
			return null;
		}
		int num = 0;
		AudioClip[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			if ((bool)array2[i])
			{
				num++;
			}
		}
		int num2 = UnityEngine.Random.Range(0, num);
		array2 = array;
		foreach (AudioClip audioClip in array2)
		{
			if ((bool)audioClip && num2-- == 0)
			{
				return audioClip;
			}
		}
		return null;
	}

	private void RestoreChargeAnimation()
	{
		if (!(_chargeAnimation == null))
		{
			_chargeAnimation.speed = _originalAnimationSpeed;
			_chargeAnimation.wrapMode = _originalWrapMode;
			_chargeAnimation = null;
		}
	}

	private void ClearCharge()
	{
		if ((bool)_chargeSource)
		{
			_chargeSource.Stop();
		}
		_isCharging = false;
		_chargingHolder = null;
		_curCharge = 0f;
	}

	private void CancelCharge(bool notify = true)
	{
		if (_isCharging)
		{
			if (notify)
			{
				SendChargeState(charging: false);
			}
			RestoreChargeAnimation();
			ClearCharge();
			PlayIdleAnim();
		}
	}

	private void SendChargeState(bool charging)
	{
		if ((bool)_holder && _holder.Owner.IsLocalClient && (bool)Server.Instance && base.IsClientInitialized)
		{
			Server.Instance.SetBowCharging(this, charging);
		}
	}

	[ObserversRpc]
	public void ObserverSetCharging(bool charging)
	{
		RpcWriter___ObserverSetCharging___1140765316(charging);
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyBowAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___EarlyBowAssembly_002DCSharp_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
			RegisterObserversRpc(4u, RpcReader___ObserverSetCharging___1140765316);
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateBowAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___LateBowAssembly_002DCSharp_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	private void RpcWriter___ObserverSetCharging___1140765316(bool charging)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			return;
		}
		Channel channel = Channel.Reliable;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		pooledWriter.WriteBoolean(charging);
		SendObserversRpc(4u, pooledWriter, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
		pooledWriter.Store();
	}

	public void RpcLogic___ObserverSetCharging___1140765316(bool P_0)
	{
		if ((bool)_holder && !_holder.Owner.IsLocalClient)
		{
			if (P_0 && !_isCharging)
			{
				BeginCharge();
			}
			else if (!P_0)
			{
				CancelCharge(notify: false);
			}
		}
	}

	private void RpcReader___ObserverSetCharging___1140765316(PooledReader PooledReader0, Channel channel)
	{
		bool flag = PooledReader0.ReadBoolean();
		if (base.IsClientInitialized)
		{
			RpcLogic___ObserverSetCharging___1140765316(flag);
		}
	}

	protected virtual void Awake_UserLogic_Bow_Assembly_002DCSharp_002Edll()
	{
		base.Awake();
		_reloadAnimation = _anim["Reload"];
		if (_reloadAnimation != null)
		{
			_originalReloadSpeed = _reloadAnimation.speed;
		}
	}
}
