using FishNet.Object.Synchronizing;
using UnityEngine;

public class Manta : AttackingFish
{
	private enum MantaState
	{
		Attacking,
		Gliding,
		Swimming
	}

	private enum MantaNoiseSound
	{
		Attack,
		Idle,
		Hurt
	}

	[Header("Manta Noises")]
	[SerializeField]
	private AudioSource _noiseSource;

	[SerializeField]
	private AudioSource _waterSource;

	[SerializeField]
	private string _attackNoise;

	[SerializeField]
	private int _attackNoises;

	[SerializeField]
	private float _attackVol = 2f;

	[Space]
	[SerializeField]
	private string _idleNoise;

	[SerializeField]
	private int _idleNoises;

	[SerializeField]
	private float _idleVol;

	[Space]
	[SerializeField]
	private string _hurtNoise;

	[SerializeField]
	private int _hurtNoises;

	[SerializeField]
	private float _hurtVol = 2f;

	[Header("Manta Movement")]
	[SerializeField]
	[Range(0f, 1f)]
	private float _flyChanceOnLand = 0.25f;

	[SerializeField]
	private float _flyForce = 28f;

	[SerializeField]
	private Vector2 _glideForceMinMax = new Vector2(14f, 28f);

	[SerializeField]
	private Vector2 _sqrLerpForGlideForceMinMax;

	[SerializeField]
	private float _glideRotForce = 8f;

	[SerializeField]
	private AnimationCurve _glideForceCurve;

	[SerializeField]
	private float _swimForce = 8f;

	[SerializeField]
	private float _swimRotForce = 6f;

	[SerializeField]
	private AnimationCurve _swimForceCurve;

	[SerializeField]
	private Vector2 _swimTimeMinMax = new Vector2(3f, 10f);

	[SerializeField]
	[Tooltip("Squared horizontal distance from the target before the manta starts circling it.")]
	private float _swimDistanceSqr = 200f;

	[SerializeField]
	[Tooltip("Squared horizontal distance at which the circling manta starts steering back towards the target.")]
	private float _maxSwimDistanceSqr = 260f;

	[SerializeField]
	[Tooltip("How long target steering is ignored after colliding with the level.")]
	private float _swimCollisionAvoidTime = 0.5f;

	[SerializeField]
	private ParticleSystem _constantWaterParticles;

	private MantaState _curState;

	private bool _justUsedFlyAttack;

	private bool _isSwimming;

	private float _changedStateTime;

	private float _randomizedSwimTime;

	private float _swimCollisionAvoidUntil;

	private string _prevNoise;

	private Vector3 _swimDir;

	private int _timesTakenDamage;

	public readonly SyncVar<byte> _syncedCurState = new SyncVar<byte>();

	private bool NetworkInitialize___EarlyMantaAssembly_002DCSharp_002Edll_Excuted;

	private bool NetworkInitialize___LateMantaAssembly_002DCSharp_002Edll_Excuted;

	public override void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_Manta_Assembly_002DCSharp_002Edll();
		NetworkInitialize___Late();
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		if (!base.IsDead)
		{
			MonoBehaviour.print("playing water");
			_waterSource.Play();
			OnCurStateChange(0, _syncedCurState.Value, asServer: false);
		}
	}

	protected override void FixedUpdate()
	{
		base.FixedUpdate();
		if (_curState == MantaState.Swimming)
		{
			SwimEffects();
		}
	}

	protected override void UpdateMovement()
	{
		if (!base.IsServerInitialized)
		{
			return;
		}
		if (_curState != MantaState.Swimming && _rig.linearVelocity.y < 0f && WaterManager.IsUnderWater(_rig.worldCenterOfMass + Vector3.down))
		{
			StartSwimming();
		}
		switch (_curState)
		{
		case MantaState.Swimming:
			Swim();
			return;
		case MantaState.Gliding:
			if ((bool)_target)
			{
				Glide();
			}
			break;
		}
		base.UpdateMovement();
	}

	private void StartSwimming()
	{
		_randomizedSwimTime = Random.Range(_swimTimeMinMax.x, _swimTimeMinMax.y);
		ServerSetState(MantaState.Swimming);
		ToggleMotor(to: false);
		_swimDir = base.transform.forward;
	}

	private void ServerSetState(MantaState state)
	{
		if (base.IsServerInitialized)
		{
			byte value = (byte)state;
			_syncedCurState.Value = value;
			MonoBehaviour.print($"server set state: {state}");
		}
	}

	private void SetState(MantaState state)
	{
		if (_curState == state || base.IsDead)
		{
			return;
		}
		_curState = state;
		_changedStateTime = Time.time;
		switch (state)
		{
		case MantaState.Swimming:
			_constantWaterParticles.Play();
			MonoBehaviour.print("playing water");
			_waterSource.Play();
			MakeNoiseSound(MantaNoiseSound.Idle);
			WaterEffects(exitWater: false);
			break;
		case MantaState.Gliding:
			MakeNoiseSound(MantaNoiseSound.Attack);
			if (WaterManager.IsUnderWater(_rig.worldCenterOfMass))
			{
				WaterEffects(exitWater: true);
			}
			break;
		}
		if (state != MantaState.Swimming)
		{
			_constantWaterParticles.Stop();
			_waterSource.Stop();
		}
	}

	private void WaterEffects(bool exitWater)
	{
		if (exitWater)
		{
			VFXManager.Play("WaterExplosion", new Vector3(base.transform.position.x, WaterManager.WaterHeight + 0.15f, base.transform.position.z), Vector3.zero);
		}
		else
		{
			VFXManager.Play("WaterSplash", base.transform.position, Vector3.zero);
		}
		AudioManager.PlayRandomClipAt("BaitLeaveWater_V", 1, 3, base.transform.position, variation: true);
	}

	private void SwimEffects()
	{
		Vector3 position = base.transform.position;
		position.y = WaterManager.GetWaterHeight(base.transform.position);
		_constantWaterParticles.transform.position = position;
	}

	private void Swim()
	{
		Vector3 worldCenterOfMass = _rig.worldCenterOfMass;
		Vector3 vector = _swimDir;
		if (Time.time >= _swimCollisionAvoidUntil && (bool)_target)
		{
			Vector3 vector2 = worldCenterOfMass - _target.Transform.position;
			vector2.y = 0f;
			if (vector2.sqrMagnitude < _swimDistanceSqr)
			{
				vector = ((vector2.sqrMagnitude > 0.0001f) ? vector2 : Vector3.ProjectOnPlane(base.transform.forward, Vector3.up));
			}
			else
			{
				Vector3 vector3 = Vector3.Cross(Vector3.up, vector2);
				float num = Mathf.Max(_swimDistanceSqr, _maxSwimDistanceSqr);
				vector = ((vector2.sqrMagnitude > num) ? (vector3 - vector2) : vector3);
			}
		}
		vector.y = 0f;
		_swimDir.y = 0f;
		if (vector.sqrMagnitude > 0.0001f)
		{
			_swimDir = Vector3.Slerp(_swimDir.normalized, vector.normalized, _swimRotForce * Time.fixedDeltaTime).normalized;
		}
		Vector3 b = _swimDir * _swimForce;
		b.y = Mathf.Clamp(WaterManager.WaterHeight - 1f - worldCenterOfMass.y, 0f - _swimForce, _swimForce);
		_rig.linearVelocity = Vector3.Lerp(_rig.linearVelocity, b, _swimForceCurve.Evaluate(Time.time - _changedStateTime));
		if (_swimDir.sqrMagnitude > 0.0001f)
		{
			Quaternion target = Quaternion.LookRotation(_swimDir, Vector3.up);
			_rig.angularVelocity = DazedUtils.GetAngularVelocityToTarget(_rig.rotation, target) * _swimRotForce;
		}
		if (Time.time - _changedStateTime >= _randomizedSwimTime)
		{
			Jump(fromUnderwater: true);
		}
		else if (!WaterManager.IsUnderWater(_rig.worldCenterOfMass + Vector3.down))
		{
			ServerSetState(MantaState.Attacking);
		}
	}

	protected override void InitializeMovement()
	{
		if (_curState == MantaState.Swimming)
		{
			return;
		}
		if (_curState == MantaState.Gliding)
		{
			if (!_isGrounded)
			{
				return;
			}
			ServerSetState(MantaState.Attacking);
		}
		InitializeMovementBase();
		if (Random.Range(0f, 1f) < _flyChanceOnLand && (_isGrounded || base.IsColliding) && !_justUsedFlyAttack)
		{
			Jump();
			return;
		}
		if ((bool)_target && _target.Transform.position.y > base.transform.position.y + 3f && !_justUsedFlyAttack)
		{
			Jump();
			return;
		}
		_justUsedFlyAttack = false;
		AttackTarget();
	}

	private void Glide()
	{
		Vector3 vector = _target.Transform.position - _rig.worldCenterOfMass;
		float t = Mathf.InverseLerp(_sqrLerpForGlideForceMinMax.x, _sqrLerpForGlideForceMinMax.y, vector.sqrMagnitude);
		float num = _glideForceCurve.Evaluate(Time.time - _changedStateTime);
		if (vector.sqrMagnitude > 0.0001f)
		{
			Quaternion target = Quaternion.LookRotation(vector, Vector3.up);
			_rig.angularVelocity = DazedUtils.GetAngularVelocityToTarget(_rig.rotation, target) * (_glideRotForce * num);
		}
		vector.Normalize();
		vector *= Mathf.Lerp(_glideForceMinMax.x, _glideForceMinMax.y, t);
		vector.y = _rig.linearVelocity.y;
		_rig.linearVelocity = Vector3.Lerp(_rig.linearVelocity, vector, num);
		if (_isGrounded && _rig.linearVelocity.y < 0f)
		{
			ServerSetState(MantaState.Attacking);
		}
	}

	private void Jump(bool fromUnderwater = false)
	{
		SetNewTarget(random: true);
		_justUsedFlyAttack = true;
		ServerSetState(MantaState.Gliding);
		ToggleMotor(to: false);
		Vector3 linearVelocity = Vector3.up * _flyForce;
		if (fromUnderwater)
		{
			linearVelocity *= 1.5f;
		}
		_rig.linearVelocity = linearVelocity;
	}

	private void ToggleMotor(bool to)
	{
		_useMotor = to;
		for (int i = 0; i < _joints.Count; i++)
		{
			_joints[i].useMotor = to;
		}
	}

	private void MakeNoiseSound(MantaNoiseSound noiseType)
	{
		string text = "";
		int max = 1;
		float volumeScale = 0f;
		switch (noiseType)
		{
		case MantaNoiseSound.Attack:
			text = _attackNoise;
			max = _attackNoises;
			volumeScale = _attackVol;
			break;
		case MantaNoiseSound.Idle:
			text = _idleNoise;
			max = _idleNoises;
			volumeScale = _idleVol;
			break;
		case MantaNoiseSound.Hurt:
			text = _hurtNoise;
			max = _hurtNoises;
			volumeScale = _hurtVol;
			break;
		}
		if (!(text == _prevNoise))
		{
			_prevNoise = text;
			AudioClip randomClip = AudioManager.GetRandomClip(text, 1, max);
			if ((bool)randomClip)
			{
				_noiseSource.PlayOneShot(randomClip, volumeScale);
				MonoBehaviour.print("made noise: " + noiseType);
			}
		}
	}

	public override void OnCollision(Collision other)
	{
		switch (_curState)
		{
		case MantaState.Gliding:
			OnLanded();
			break;
		case MantaState.Swimming:
		{
			int num = 1 << other.gameObject.layer;
			if ((GameInfo.LevelLayer.value & num) != 0 && other.contactCount != 0)
			{
				Vector3 zero = Vector3.zero;
				for (int i = 0; i < other.contactCount; i++)
				{
					zero += other.GetContact(i).normal;
				}
				zero.y = 0f;
				if (!(zero.sqrMagnitude <= 0.0001f))
				{
					_swimDir = zero.normalized;
					_swimCollisionAvoidUntil = Time.time + _swimCollisionAvoidTime;
					Vector3 linearVelocity = _swimDir * _swimForce;
					linearVelocity.y = _rig.linearVelocity.y;
					_rig.linearVelocity = linearVelocity;
				}
			}
			break;
		}
		}
		base.OnCollision(other);
	}

	protected override void DamageOnCollision(Player player, Vector3 pos)
	{
		if (_curState == MantaState.Gliding)
		{
			ServerSetState(MantaState.Attacking);
		}
		base.DamageOnCollision(player, pos);
	}

	protected override void OnDeath()
	{
		base.OnDeath();
		_waterSource.Stop();
		if (_constantWaterParticles.isPlaying)
		{
			_constantWaterParticles.Stop();
		}
	}

	private void OnLanded()
	{
		ServerSetState(MantaState.Attacking);
		ToggleMotor(to: false);
	}

	private void OnCurStateChange(byte prev, byte next, bool asServer)
	{
		SetState((MantaState)next);
	}

	protected override void OnHealthChange(int prev, int next, bool asServer)
	{
		base.OnHealthChange(prev, next, asServer);
		if (!asServer)
		{
			_timesTakenDamage++;
			if (_timesTakenDamage >= 20)
			{
				_timesTakenDamage = 0;
				MakeNoiseSound(MantaNoiseSound.Hurt);
			}
		}
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyMantaAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___EarlyMantaAssembly_002DCSharp_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
			_syncedCurState.InitializeEarly(this, 11u, isSyncObject: false);
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateMantaAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___LateMantaAssembly_002DCSharp_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
			_syncedCurState.InitializeLate();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	protected virtual void Awake_UserLogic_Manta_Assembly_002DCSharp_002Edll()
	{
		((Fish)this).Awake();
		_syncedCurState.OnChange += OnCurStateChange;
	}
}
