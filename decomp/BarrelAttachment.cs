using UnityEngine;

public class BarrelAttachment : Attachment
{
	[SerializeField]
	private Transform _firePoint;

	[SerializeField]
	private ParticleSystem _fireParticle;

	[SerializeField]
	private byte _projectileType;

	[Header("Recoil")]
	[SerializeField]
	[Range(0f, 1f)]
	private float _adsRecoilPosMulti = 1f;

	[SerializeField]
	[Range(0f, 1f)]
	private float _adsRecoilRotMulti = 1f;

	[SerializeField]
	private float _adsRecoilSpringMulti = 1f;

	[SerializeField]
	private Vector2 _screenRecoilAmount;

	[SerializeField]
	private Vector3 _modelRecoilPos;

	[SerializeField]
	private Vector2 _modelRecoilRot;

	[SerializeField]
	private float _weaponRecoilMulti;

	[SerializeField]
	private float _recoilSpringPos;

	[SerializeField]
	private float _recoilDamperPos;

	[SerializeField]
	private float _recoilSpringRot;

	[SerializeField]
	private float _recoilDamperRot;

	[Header("Audio")]
	[SerializeField]
	private float _fireSoundVolume = 1f;

	[SerializeField]
	private string _outsideFireSound;

	[SerializeField]
	private string _insideFireSound;

	[SerializeField]
	private bool _useFireSoundStrenghts;

	[SerializeField]
	private string[] _fireSoundStrengths;

	[SerializeField]
	private int _fireSoundCount;

	[SerializeField]
	private bool _useMediumSoundDistance;

	public Transform FirePoint => _firePoint;

	public byte ProjectileType => _projectileType;

	public int FireSoundCount => _fireSoundCount;

	public float FireSoundVolume => _fireSoundVolume;

	public bool UseMediumSoundDistance => _useMediumSoundDistance;

	public ParticleSystem FireParticle => _fireParticle;

	public float AdsRecoilPosMulti => _adsRecoilPosMulti;

	public float AdsRecoilRotMulti => _adsRecoilRotMulti;

	public float AdsRecoilSpringMulti => _adsRecoilSpringMulti;

	public Vector2 ScreenRecoilAmount => _screenRecoilAmount;

	public Vector3 ModelRecoilPos => _modelRecoilPos;

	public Vector2 ModelRecoilRot => _modelRecoilRot;

	public float WeaponRecoilMulti => _weaponRecoilMulti;

	public float RecoilSpringPos => _recoilSpringPos;

	public float RecoilDamperPos => _recoilDamperPos;

	public float RecoilSpringRot => _recoilSpringRot;

	public float RecoilDamperRot => _recoilDamperRot;

	public string GetFireSound(float strength = 1f)
	{
		if (_useFireSoundStrenghts)
		{
			int num = Mathf.Min(Mathf.FloorToInt(Mathf.Clamp01(strength) * (float)_fireSoundStrengths.Length), _fireSoundStrengths.Length - 1);
			if (!string.IsNullOrEmpty(_fireSoundStrengths[num]))
			{
				return _fireSoundStrengths[num];
			}
		}
		if (!Player.IsInside)
		{
			return _outsideFireSound;
		}
		return _insideFireSound;
	}
}
