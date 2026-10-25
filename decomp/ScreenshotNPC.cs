using UnityEngine;
using UnityEngine.Serialization;

[ExecuteInEditMode]
public class ScreenshotNPC : MonoBehaviour
{
	[Header("Colors")]
	[FormerlySerializedAs("_skinColor")]
	[SerializeField]
	private Vector3 _localSkinColor;

	[SerializeField]
	private Vector3 _localHatColor;

	[SerializeField]
	private Vector3 _localHatColor2;

	[SerializeField]
	private Vector3 _localHatColor3;

	[SerializeField]
	private Vector3 _localOutfitColor;

	[SerializeField]
	private Vector3 _localOutfitColor2;

	[SerializeField]
	private Vector3 _localOutfitColor3;

	[SerializeField]
	private Vector3 _localAccessoryColor;

	[SerializeField]
	private Vector3 _localAccessoryColor2;

	[SerializeField]
	private Vector3 _localAccessoryColor3;

	[Header("Renderers")]
	[SerializeField]
	private SkinnedMeshRenderer _head;

	[SerializeField]
	private SkinnedMeshRenderer _hat;

	[SerializeField]
	private SkinnedMeshRenderer _outfit;

	[SerializeField]
	private SkinnedMeshRenderer _accessory;

	[SerializeField]
	private SkinnedMeshRenderer _handLeft;

	[SerializeField]
	private SkinnedMeshRenderer _handRight;

	private Vector3 _shaderSkinColor;

	private Vector3 _shaderHatColor;

	private Vector3 _shaderHatColor2;

	private Vector3 _shaderHatColor3;

	private Vector3 _shaderOutfitColor;

	private Vector3 _shaderOutfitColor2;

	private Vector3 _shaderOutfitColor3;

	private Vector3 _shaderAccessoryColor;

	private Vector3 _shaderAccessoryColor2;

	private Vector3 _shaderAccessoryColor3;

	private bool _colorsSet;

	private void OnEnable()
	{
		_colorsSet = false;
	}

	private void OnValidate()
	{
		_colorsSet = false;
	}

	private void Update()
	{
		if (!_colorsSet || ColorsChanged())
		{
			SetColors();
		}
	}

	private bool ColorsChanged()
	{
		if (!(_localSkinColor != _shaderSkinColor) && !(_localHatColor != _shaderHatColor) && !(_localHatColor2 != _shaderHatColor2) && !(_localHatColor3 != _shaderHatColor3) && !(_localOutfitColor != _shaderOutfitColor) && !(_localOutfitColor2 != _shaderOutfitColor2) && !(_localOutfitColor3 != _shaderOutfitColor3) && !(_localAccessoryColor != _shaderAccessoryColor) && !(_localAccessoryColor2 != _shaderAccessoryColor2))
		{
			return _localAccessoryColor3 != _shaderAccessoryColor3;
		}
		return true;
	}

	private void SetColors()
	{
		_colorsSet = true;
		_shaderSkinColor = _localSkinColor;
		_shaderHatColor = _localHatColor;
		_shaderHatColor2 = _localHatColor2;
		_shaderHatColor3 = _localHatColor3;
		_shaderOutfitColor = _localOutfitColor;
		_shaderOutfitColor2 = _localOutfitColor2;
		_shaderOutfitColor3 = _localOutfitColor3;
		_shaderAccessoryColor = _localAccessoryColor;
		_shaderAccessoryColor2 = _localAccessoryColor2;
		_shaderAccessoryColor3 = _localAccessoryColor3;
		if ((bool)_head)
		{
			ShaderManager.SetPlayerColorsEditMode(_head, _localSkinColor);
		}
		if ((bool)_hat)
		{
			ShaderManager.SetPlayerColorsEditMode(_hat, _localSkinColor, _localHatColor, _localHatColor2, _localHatColor3);
		}
		if ((bool)_outfit)
		{
			ShaderManager.SetPlayerColorsEditMode(_outfit, _localSkinColor, _localOutfitColor, _localOutfitColor2, _localOutfitColor3);
		}
		if ((bool)_accessory)
		{
			ShaderManager.SetPlayerColorsEditMode(_accessory, _localSkinColor, _localAccessoryColor, _localAccessoryColor2, _localAccessoryColor3);
		}
		if ((bool)_handLeft)
		{
			ShaderManager.SetPlayerColorsEditMode(_handLeft, _localSkinColor);
		}
		if ((bool)_handRight)
		{
			ShaderManager.SetPlayerColorsEditMode(_handRight, _localSkinColor);
		}
	}
}
