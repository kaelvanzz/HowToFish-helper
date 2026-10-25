using FishNet.Managing;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class Weapon : Tool
{
	[SerializeField]
	[Header("Weapon Stats")]
	private WeaponInfo _weaponInfo;

	[SerializeField]
	private Attachments _attachments;

	[SerializeField]
	private bool _usesArrows;

	[SerializeField]
	private bool _fullAuto;

	[SerializeField]
	private bool _canAds;

	[SerializeField]
	private bool _noShootingDuringShootAnim;

	[SerializeField]
	private bool _noQueueingShots;

	[SerializeField]
	private float _timeBetweenShots;

	[SerializeField]
	private float _projSpeed;

	[SerializeField]
	private float _spread;

	[SerializeField]
	private int _projectileCountPerShot;

	[SerializeField]
	private int _recoilKnockback;

	[SerializeField]
	private Vector3 _sprintPos;

	[SerializeField]
	private Vector3 _sprintRot;

	[SerializeField]
	private float _sprintSpeed;

	[Header("Effects")]
	[SerializeField]
	private AudioSequence _reloadAudioSeq;

	[FormerlySerializedAs("_reloadLongAudioSeq")]
	[SerializeField]
	private AudioSequence _reloadLastAudioSeq;

	[SerializeField]
	private AudioSequence _fireAudioSeq;

	[SerializeField]
	private AudioSequence _fireLastAudioSeq;

	[SerializeField]
	private bool _hasLastReloadAnim;

	[SerializeField]
	[Range(0f, 1f)]
	[Tooltip("Normalized reload animation progress required before ammo is refilled.")]
	private float _reloadAmmoRefillPercentage = 0.9f;

	[SerializeField]
	private bool _hasLastFireAnim;

	[SerializeField]
	private ParticleSystem _casingParticle;

	[SerializeField]
	private float _casingParticleDelay;

	[SerializeField]
	[Min(0f)]
	[Header("Inspect Kill Window")]
	[Tooltip("Seconds of the inspect animation before inspecting counts for a kill.")]
	private float _inspectStartDelay = 0.3f;

	[SerializeField]
	[Min(0f)]
	[Tooltip("Seconds remaining in the inspect animation when inspecting stops counting for a kill.")]
	private float _inspectEndLeadTime = 0.3f;

	private bool _holdingFireInput;

	private bool _holdingAdsInput;

	private bool _hasCoolDown;

	private bool _isReloading;

	private bool _queueReload;

	private bool _queuedShoot;

	private bool _disabledBeforeShootAnim;

	private bool _reloadAmmoRefilled;

	private bool _inspectWindowActive;

	private float _aimPercent;

	private float _refAim;

	private string _activeReloadAnimName;

	private Vector3 _refSprintPos;

	private Vector3 _refSprintRot;

	private bool NetworkInitialize___EarlyWeaponAssembly_002DCSharp_002Edll_Excuted;

	private bool NetworkInitialize___LateWeaponAssembly_002DCSharp_002Edll_Excuted;

	public bool IsAds { get; private set; }

	public int Damage => _attachments.Damage;

	public float AdsFov => _attachments.AdsFov;

	public float AdsSpeedDamping => _attachments.AdsSpeedDamping;

	public Attachments Attachments => _attachments;

	public int Ammo { get; private set; }

	public bool UsesArrows => _usesArrows;

	protected bool CanShoot
	{
		get
		{
			if (!HasCooldown() && !_isReloading)
			{
				return Ammo > 0;
			}
			return false;
		}
	}

	protected bool AllowsShotQueueing => !_noQueueingShots;

	protected bool HasPendingOrActiveReload
	{
		get
		{
			if (!_queueReload)
			{
				return _isReloading;
			}
			return true;
		}
	}

	protected virtual bool CanPlayAimIdle => true;

	protected virtual bool IsAnimationBlockingAds
	{
		get
		{
			if (_noShootingDuringShootAnim)
			{
				if (!_anim.IsPlaying("Fire"))
				{
					return _anim.IsPlaying("FireLast");
				}
				return true;
			}
			return false;
		}
	}

	public override void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_Weapon_Assembly_002DCSharp_002Edll();
		NetworkInitialize___Late();
	}

	public override void OnPickUp()
	{
		base.OnPickUp();
		if (_disabledBeforeShootAnim)
		{
			AfterShootEffects();
		}
		else
		{
			PlayIdleAnim();
		}
		if (_holder.Owner.IsLocalClient)
		{
			SetRecoilSprings();
		}
	}

	public override void OnDrop()
	{
		SetInspecting(inspecting: false);
		_holdingFireInput = false;
		_holdingAdsInput = false;
		_hasCoolDown = false;
		_queuedShoot = false;
		if (_isReloading)
		{
			TryRefillAmmo();
			_queueReload = !_reloadAmmoRefilled;
			_isReloading = false;
		}
		_refAim = 0f;
		_aimPercent = 0f;
		IsAds = false;
		if (_noShootingDuringShootAnim && (_anim.IsPlaying("FireLast") || _anim.IsPlaying("Fire")))
		{
			_disabledBeforeShootAnim = true;
		}
		AudioSequenceManager.CancelAllActiveSequencesFromOwner(base.gameObject);
		if ((bool)_holder && _holder.Owner.IsLocalClient)
		{
			PlayerUI.ToggleSniperUI(to: false);
			_attachments.ToggleSightModels(to: true);
		}
		base.OnDrop();
	}

	public override void PrimaryInput(InputAction.CallbackContext context)
	{
		if ((bool)_holder)
		{
			if (!HasCooldown())
			{
				Shoot();
			}
			else if (!_noQueueingShots)
			{
				_queuedShoot = true;
			}
			_holdingFireInput = true;
		}
	}

	public override void PrimaryInputCancel(InputAction.CallbackContext context)
	{
		_holdingFireInput = false;
	}

	public override void SecondaryInput(InputAction.CallbackContext context)
	{
		_holdingAdsInput = PlayerSettings.HoldToAim || !_holdingAdsInput;
		PlayerSkills.OnADSChange(_holdingAdsInput);
	}

	public override void SecondaryInputCanceled(InputAction.CallbackContext context)
	{
		if (PlayerSettings.HoldToAim)
		{
			_holdingAdsInput = false;
			PlayerSkills.OnADSChange(to: false);
		}
	}

	public override void ReloadInput(InputAction.CallbackContext context)
	{
		if (Ammo != Attachments.AmmoPerMag && !_isReloading)
		{
			if (_hasCoolDown || !_anim.IsPlaying("Fire") || !_anim.IsPlaying("FireLast"))
			{
				_queueReload = true;
			}
			else
			{
				LocalReload();
			}
		}
	}

	public override void InspectInput(InputAction.CallbackContext context)
	{
		if (_anim.IsPlaying("Idle") || (!_anim.isPlaying && !IsAds))
		{
			PlayInspectAnim(calledFromLocal: true);
			PlayerUI.ShowInspectInfo(this, Attachments.GetAttachmentInfo());
		}
	}

	protected override void Update()
	{
		base.Update();
		UpdateInspecting();
		if (_disabledBeforeShootAnim && !_anim.IsPlaying("FireLast") && !_anim.IsPlaying("Fire"))
		{
			_disabledBeforeShootAnim = false;
		}
		if (_isReloading)
		{
			TryRefillAmmo();
			if (!_queueReload && !_anim.IsPlaying("Reload") && !_anim.IsPlaying("ReloadLast"))
			{
				Ammo = _attachments.AmmoPerMag;
				_reloadAmmoRefilled = true;
				_isReloading = false;
				_hasCoolDown = false;
			}
		}
		if ((bool)_holder && _holder.Owner.IsLocalClient)
		{
			if (_holder.Movement.Sprinting && _holder.Movement.Grounded)
			{
				_holder.ToolMovement.SetSprintPos(Vector3.SmoothDamp(_holder.ToolMovement.SprintPos, _sprintPos, ref _refSprintPos, _sprintSpeed));
				_holder.ToolMovement.SetSprintRot(Vector3.SmoothDamp(_holder.ToolMovement.SprintRot, _sprintRot, ref _refSprintRot, _sprintSpeed));
			}
			else
			{
				_holder.ToolMovement.SetSprintPos(Vector3.SmoothDamp(_holder.ToolMovement.SprintPos, Vector3.zero, ref _refSprintPos, _sprintSpeed));
				_holder.ToolMovement.SetSprintRot(Vector3.SmoothDamp(_holder.ToolMovement.SprintRot, Vector3.zero, ref _refSprintRot, _sprintSpeed));
			}
			if (_queueReload && !_hasCoolDown && !_anim.IsPlaying("Fire") && !_anim.IsPlaying("FireLast"))
			{
				LocalReload();
			}
			else if (((_fullAuto || PlayerSettings.HoldToAttack) && _holdingFireInput) || _queuedShoot)
			{
				Shoot();
			}
		}
	}

	protected override void LateUpdate()
	{
		base.LateUpdate();
		if ((bool)_holder && _holder.Owner.IsLocalClient && _canAds)
		{
			HandleAiming();
		}
	}

	protected virtual void OnDisable()
	{
		SetInspecting(inspecting: false);
		PlayIdleAnim();
		if ((bool)_holder && _holder.Owner.IsLocalClient)
		{
			PlayerUI.ToggleSniperUI(to: false);
		}
	}

	public override void OnStopClient()
	{
		SetInspecting(inspecting: false);
		base.OnStopClient();
	}

	private void UpdateInspecting()
	{
		AnimationState animationState = (((bool)_holder && _holder.Owner.IsLocalClient && _holder.Holding.HeldItem == this && !base.IsInInventory && _anim.IsPlaying("Inspect")) ? _anim["Inspect"] : null);
		bool inspecting = animationState != null && animationState.time >= Mathf.Max(0f, _inspectStartDelay) && animationState.length - animationState.time > Mathf.Max(0f, _inspectEndLeadTime);
		SetInspecting(inspecting);
	}

	private void SetInspecting(bool inspecting)
	{
		if (_inspectWindowActive != inspecting)
		{
			_inspectWindowActive = inspecting;
		}
	}

	protected virtual float GetProjectileSpeed()
	{
		return _projSpeed;
	}

	protected virtual int GetShotDamage()
	{
		return _attachments.Damage;
	}

	protected virtual float GetFireSoundVolumeMultiplier()
	{
		return 1f;
	}

	protected virtual float GetFireSoundStrength()
	{
		return 1f;
	}

	protected virtual float GetReloadAudioSequenceSpeedMultiplier()
	{
		return 1f;
	}

	protected void Shoot()
	{
		if (HasCooldown() || _isReloading || Ammo == 0)
		{
			return;
		}
		if (_noShootingDuringShootAnim)
		{
			CancelToggledAim();
		}
		_queuedShoot = false;
		float num = Random.Range(-1f, 1f);
		AddModelRecoil(num);
		_holder.Camera.Recoil(new Vector2(num * _attachments.ScreenRecoilAmount.x, _attachments.ScreenRecoilAmount.y));
		_holder.ToolMovement.Recoil(new Vector2(num * _attachments.ScreenRecoilAmount.x, _attachments.ScreenRecoilAmount.y) * _attachments.WeaponRecoilMulti);
		_holder.Movement.Knockback(-_holder.CamObject.forward * _recoilKnockback);
		PlayerSkills.OnAttack(_timeBetweenShots);
		Ammo--;
		if (Ammo == 0)
		{
			_queueReload = true;
		}
		else
		{
			_hasCoolDown = true;
			Invoke("CoolDown", _timeBetweenShots);
		}
		float projectileSpeed = GetProjectileSpeed();
		int shotDamage = GetShotDamage();
		ShootEffects(fromLocal: true);
		Vector3 pos = _attachments.FirePoint.position;
		Vector3 forward;
		if (_attachments.UseSniperUi && _aimPercent > 0.9f)
		{
			pos = _holder.Camera.CamTransform.position + _holder.Camera.CamTransform.forward * _modelForwardLength;
			forward = _holder.Camera.CamTransform.forward;
		}
		else
		{
			forward = _attachments.FirePoint.forward;
		}
		RaycastHit raycastHit = GunClippingRay();
		if ((bool)raycastHit.transform)
		{
			Player playerFromBodyPart = PlayerManager.GetPlayerFromBodyPart(raycastHit.transform);
			Item item = ItemManager.Get(raycastHit.transform);
			if ((bool)playerFromBodyPart)
			{
				for (int i = 0; i < _projectileCountPerShot; i++)
				{
					playerFromBodyPart.Vitals.LocalHit(raycastHit.point, _attachments.FirePoint.forward, _holder, shotDamage, rangedHit: true, _attachments.FirePoint.forward * GameInfo.PlayerKillForce, UsesArrows);
				}
			}
			if ((bool)item)
			{
				for (int j = 0; j < _projectileCountPerShot; j++)
				{
					item.LocalHit(raycastHit.transform, raycastHit.point, _attachments.FirePoint.forward, _holder, shotDamage, rangedHit: true, _attachments.FirePoint.forward * _weaponInfo.ProjectileForce, fromNpc: false, UsesArrows, _inspectWindowActive);
				}
			}
			if (raycastHit.transform.CompareTag("NPC"))
			{
				DazedUtils.PlayDeadPlayerHitEffects(raycastHit.point, _attachments.FirePoint.forward, shotDamage, base.Holder, noDecals: true);
			}
			pos = Vector3.down * 10000f;
		}
		Vector3[] array = new Vector3[_projectileCountPerShot];
		for (int k = 0; k < array.Length; k++)
		{
			Vector3 vector = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
			vector = Vector3.ClampMagnitude(vector, 1f);
			array[k] = Quaternion.Euler(vector * _spread) * forward * projectileSpeed;
		}
		_weaponInfo.ProjectileType = _attachments.ProjectileType;
		_weaponInfo.ShotDamage = shotDamage;
		if (array.Length > 1)
		{
			ProjectileManager.Instance.AddProjectiles(_holder, _weaponInfo, isLocal: true, pos, array, 0u, 0u, canHitOwner: false, _inspectWindowActive);
		}
		else
		{
			ProjectileManager.Instance.AddProjectile(_holder, _weaponInfo, isLocal: true, pos, array[0], 0u, 0u, fromNpc: false, _inspectWindowActive);
		}
	}

	public void ShootEffects(bool fromLocal = false)
	{
		if (!_holder || !_holder.Owner.IsLocalClient)
		{
			Ammo--;
			if (Ammo < 0)
			{
				Ammo = 0;
			}
		}
		else if (!fromLocal)
		{
			return;
		}
		if ((bool)_attachments.FireParticle)
		{
			_attachments.FireParticle.Play();
		}
		AudioSequenceManager.CancelAllActiveSequencesFromOwner(base.gameObject);
		if ((bool)_holder)
		{
			AudioDistance distance = (_attachments.UseMediumSoundDistance ? AudioDistance.Medium : AudioDistance.Long);
			float minDelay = Mathf.Clamp(_timeBetweenShots * 0.9f, 0f, 0.2f);
			float num = (_holder.Owner.IsLocalClient ? 1f : 0.9f);
			float volume = _attachments.FireSoundVolume * num * GetFireSoundVolumeMultiplier();
			AudioManager.PlayRandomPlayerClip(_attachments.GetFireSound(GetFireSoundStrength()), 1, _attachments.FireSoundCount, _holder, variation: false, distance, volume, minDelay);
		}
		AfterShootEffects();
	}

	private void AfterShootEffects()
	{
		if ((bool)_casingParticle)
		{
			Invoke("PlayCasingParticle", _casingParticleDelay);
		}
		if ((bool)_holder)
		{
			AudioSequenceManager.PlayPlayerSequence((Ammo == 0) ? _fireLastAudioSeq : _fireAudioSeq, _holder, base.gameObject);
		}
		PlayShotAnimation();
	}

	protected virtual void PlayShotAnimation()
	{
		_anim.Stop();
		_anim.Play((Ammo == 0 && _hasLastFireAnim) ? "FireLast" : "Fire");
	}

	private void CoolDown()
	{
		_hasCoolDown = false;
	}

	private void PlayCasingParticle()
	{
		_casingParticle.Emit(1);
	}

	private void AddModelRecoil(float recoilDir)
	{
		float num = 1f - (1f - _attachments.AdsRecoilRotMulti) * _aimPercent;
		float num2 = 1f - (1f - _attachments.AdsRecoilPosMulti) * _aimPercent;
		_holder.ToolMovement.AddRotToRecoilRig(Vector3.up * (recoilDir * _attachments.ModelRecoilRot.x * num));
		_holder.ToolMovement.AddRotToRecoilRig(Vector3.back * (recoilDir * _attachments.ModelRecoilRot.x * num));
		_holder.ToolMovement.AddRotToRecoilRig(Vector3.left * (_attachments.ModelRecoilRot.y * num));
		_holder.ToolMovement.AddPosToRecoilRig(Vector3.right * (recoilDir * _attachments.ModelRecoilPos.x * num2));
		_holder.ToolMovement.AddPosToRecoilRig(Vector3.up * (_attachments.ModelRecoilPos.y * num2));
		_holder.ToolMovement.AddPosToRecoilRig(Vector3.back * (_attachments.ModelRecoilPos.z * num2));
	}

	private void HandleAiming()
	{
		bool flag = _holdingAdsInput && !_isReloading && !IsAnimationBlockingAds;
		float aimPercent = _aimPercent;
		_aimPercent = Mathf.SmoothDamp(_aimPercent, flag ? 1 : 0, ref _refAim, _attachments.AdsSpeedDamping);
		_holder.ToolMovement.SetAimPos(Vector3.Lerp(Vector3.zero, _attachments.AdsPos, _aimPercent));
		if (_attachments.UseSniperUi)
		{
			float num = _aimPercent - _attachments.SniperUiAimPercent;
			if (num < 0f)
			{
				num = 0f;
			}
			float num2 = 1f - _attachments.SniperUiAimPercent;
			num2 = 1f / num2;
			num *= num2;
			bool flag2 = num > 0f;
			PlayerUI.SetSniperUI(_attachments.SniperUiPos.position, _attachments.SniperUiPosCurve.Evaluate(num), Mathf.Lerp(_attachments.SniperUiScale, 1f, num));
			if ((aimPercent < _attachments.SniperUiAimPercent && flag2) || (aimPercent > _attachments.SniperUiAimPercent && !flag2))
			{
				PlayerUI.ToggleSniperUI(flag2);
				_attachments.ToggleSightModels(!flag2);
			}
		}
		if (IsAds != flag)
		{
			IsAds = flag;
			SetRecoilSprings();
			AudioManager.PlayPlayerClip(IsAds ? "AdsIn" : "AdsOut", _holder, variation: true, AudioDistance.VeryShort, 1f, 0.25f);
			if (IsAds && CanPlayAimIdle)
			{
				PlayIdleAnim();
			}
		}
	}

	private void LocalReload()
	{
		CancelToggledAim();
		Reload(fromLocal: true);
		Server.Instance.ReloadWeapon(this);
	}

	private void CancelToggledAim()
	{
		if (!PlayerSettings.HoldToAim && _holdingAdsInput)
		{
			_holdingAdsInput = false;
			PlayerSkills.OnADSChange(to: false);
		}
	}

	[ObserversRpc]
	public void ObserverReload()
	{
		RpcWriter___ObserverReload___2166136261();
	}

	private void Reload(bool fromLocal = false)
	{
		if (fromLocal || !_holder || !_holder.Owner.IsLocalClient)
		{
			bool flag = _hasLastReloadAnim && Ammo == 0;
			_activeReloadAnimName = (flag ? "ReloadLast" : "Reload");
			_anim.Play(_activeReloadAnimName);
			AudioSequenceManager.CancelAllActiveSequencesFromOwner(base.gameObject);
			float reloadAudioSequenceSpeedMultiplier = GetReloadAudioSequenceSpeedMultiplier();
			if (flag && _hasLastReloadAnim)
			{
				AudioSequenceManager.PlayPlayerSequence(_reloadLastAudioSeq, _holder, base.gameObject, reloadAudioSequenceSpeedMultiplier);
			}
			else
			{
				AudioSequenceManager.PlayPlayerSequence(_reloadAudioSeq, _holder, base.gameObject, reloadAudioSequenceSpeedMultiplier);
			}
			_queueReload = false;
			_isReloading = true;
			_reloadAmmoRefilled = false;
		}
	}

	private void TryRefillAmmo()
	{
		if (!_reloadAmmoRefilled && !string.IsNullOrEmpty(_activeReloadAnimName))
		{
			AnimationState animationState = _anim[_activeReloadAnimName];
			if (!(animationState == null) && !(animationState.normalizedTime < Mathf.Clamp01(_reloadAmmoRefillPercentage)))
			{
				Ammo = _attachments.AmmoPerMag;
				_reloadAmmoRefilled = true;
			}
		}
	}

	public void SetRecoilSprings()
	{
		if ((bool)_holder)
		{
			ConfigurableJoint toolRecoilRigJoint = _holder.ToolMovement.ToolRecoilRigJoint;
			float num = (IsAds ? _attachments.AdsRecoilSpringMulti : 1f);
			SoftJointLimitSpring softJointLimitSpring = new SoftJointLimitSpring
			{
				spring = _attachments.RecoilSpringPos * num,
				damper = _attachments.RecoilDamperPos
			};
			JointDrive slerpDrive = (toolRecoilRigJoint.zDrive = (toolRecoilRigJoint.yDrive = (toolRecoilRigJoint.xDrive = new JointDrive
			{
				positionSpring = _attachments.RecoilSpringPos * num,
				positionDamper = _attachments.RecoilDamperPos,
				maximumForce = float.PositiveInfinity
			})));
			toolRecoilRigJoint.linearLimitSpring = softJointLimitSpring;
			softJointLimitSpring.spring = _attachments.RecoilSpringRot * num;
			softJointLimitSpring.damper = _attachments.RecoilDamperRot;
			slerpDrive.positionSpring = _attachments.RecoilSpringRot * num;
			slerpDrive.positionDamper = _attachments.RecoilDamperRot;
			toolRecoilRigJoint.slerpDrive = slerpDrive;
			toolRecoilRigJoint.angularXLimitSpring = softJointLimitSpring;
			toolRecoilRigJoint.angularYZLimitSpring = softJointLimitSpring;
		}
	}

	private bool HasCooldown()
	{
		if (!_hasCoolDown)
		{
			if (_noShootingDuringShootAnim)
			{
				if (!_anim.IsPlaying("Fire"))
				{
					return _anim.IsPlaying("FireLast");
				}
				return true;
			}
			return false;
		}
		return true;
	}

	private RaycastHit GunClippingRay()
	{
		Vector3 direction = _attachments.FirePoint.position + _attachments.FirePoint.forward * 0.1f - _holder.CamObject.position;
		Physics.Raycast(_holder.CamObject.position, direction, out var hitInfo, direction.magnitude, (int)GameInfo.LevelLayer | (int)GameInfo.PlayerLayer | (int)GameInfo.ItemLayer | (int)GameInfo.NpcLayer);
		return hitInfo;
	}

	public override void LoadFromSave(SavedItem savedItem)
	{
		base.LoadFromSave(savedItem);
		_attachments.LoadFromSave(savedItem);
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyWeaponAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___EarlyWeaponAssembly_002DCSharp_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
			RegisterObserversRpc(3u, RpcReader___ObserverReload___2166136261);
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateWeaponAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___LateWeaponAssembly_002DCSharp_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	private void RpcWriter___ObserverReload___2166136261()
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			return;
		}
		Channel channel = Channel.Reliable;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		SendObserversRpc(3u, pooledWriter, channel, DataOrderType.Default, bufferLast: false, excludeServer: false, excludeOwner: false);
		pooledWriter.Store();
	}

	public void RpcLogic___ObserverReload___2166136261()
	{
		Reload();
	}

	private void RpcReader___ObserverReload___2166136261(PooledReader PooledReader0, Channel channel)
	{
		if (base.IsClientInitialized)
		{
			RpcLogic___ObserverReload___2166136261();
		}
	}

	protected virtual void Awake_UserLogic_Weapon_Assembly_002DCSharp_002Edll()
	{
		base.Awake();
		_attachments.InitAttachments(this);
		_weapon = this;
		_type = ItemType.Weapon;
		Ammo = _attachments.AmmoPerMag;
		_weaponInfo.Weapon = this;
	}
}
