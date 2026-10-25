using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MoneyUI : MonoBehaviour
{
	[Header("Dollar")]
	[SerializeField]
	private RectTransform _moneyTextHolder;

	[SerializeField]
	private TextMeshProUGUI _moneyText;

	[Header("Euro")]
	[SerializeField]
	private CanvasGroup _euroTextGroup;

	[SerializeField]
	private RectTransform _euroTextHolder;

	[SerializeField]
	private TextMeshProUGUI _euroText;

	[Header("General")]
	[SerializeField]
	private int _totalMoneyTexts = 10;

	[SerializeField]
	private float _moneyTextMoveTime = 0.25f;

	[SerializeField]
	private float _moneyTextLifeTime = 3f;

	[SerializeField]
	private float _moneyTextDefaultOffset;

	[SerializeField]
	private float _moneyTextRandomOffset = 25f;

	[SerializeField]
	private float _moneyTextUpPos = 0.5f;

	[SerializeField]
	private Vector2 _moneyTextRotMinMax = new Vector2(5f, 45f);

	private List<TextMeshProUGUI> _moneyTexts = new List<TextMeshProUGUI>();

	private int _curMoneyText;

	private int _prevMoney;

	private int _curMoney;

	private int _targetMoney;

	private int _prevEuro;

	private int _curEuro;

	private int _targetEuro;

	public void InitializeLocal()
	{
		if (MoneyManager.Euro == 0)
		{
			_euroTextGroup.alpha = 0f;
		}
		SetMoney(MoneyManager.Money, MoneyManager.Money, gainedMoney: true);
		SetMoney(MoneyManager.Euro, MoneyManager.Euro, gainedMoney: true, Currency.Euro);
		for (int i = 0; i < _totalMoneyTexts; i++)
		{
			TextMeshProUGUI textMeshProUGUI = UnityEngine.Object.Instantiate(PlayerUI.CanvasTextPrefab, PlayerUI.FXCanvasTrans);
			textMeshProUGUI.transform.localScale = Vector3.zero;
			textMeshProUGUI.transform.localRotation = Quaternion.Euler(0f, 0f, 0f - UnityEngine.Random.Range(_moneyTextRotMinMax.x, _moneyTextRotMinMax.y));
			textMeshProUGUI.fontSize = 32f;
			_moneyTexts.Add(textMeshProUGUI);
		}
	}

	public void SetMoney(int to, int diff, bool gainedMoney, Currency currency = Currency.Dollar)
	{
		switch (currency)
		{
		case Currency.Dollar:
			_prevMoney = _curMoney;
			_targetMoney = to;
			break;
		case Currency.Euro:
			_prevEuro = _curEuro;
			_targetEuro = to;
			break;
		}
		RectTransform rectTransform = ((currency == Currency.Dollar) ? _moneyTextHolder : _euroTextHolder);
		LeanTween.cancel(rectTransform);
		LeanTween.value(rectTransform.gameObject, 0f, 1f, 1f).setEase(LeanTweenType.easeOutQuart).setOnUpdate((currency == Currency.Dollar) ? new Action<float>(UpdateVisualMoney) : new Action<float>(UpdateVisualEuro));
		if (currency == Currency.Euro)
		{
			LeanTween.value(_euroTextGroup.gameObject, _euroTextGroup.alpha, (to != 0) ? 1 : 0, 1f).setEase(LeanTweenType.easeInOutQuad).setOnUpdate(UpdateEuroAlpha);
		}
		if (_moneyTexts.Count != 0 && !Player.LocalPlayer.Dying.IsDead)
		{
			TextMeshProUGUI text = _moneyTexts[_curMoneyText];
			float moneyTextRandomOffset = _moneyTextRandomOffset;
			Vector3 vector = new Vector3(UnityEngine.Random.Range(0f - moneyTextRandomOffset, moneyTextRandomOffset), UnityEngine.Random.Range(0f - moneyTextRandomOffset, moneyTextRandomOffset), 0f);
			Vector3 vector2 = rectTransform.localPosition + Vector3.right * rectTransform.sizeDelta.x / 2f + Vector3.up * _moneyTextDefaultOffset + vector;
			text.transform.localPosition = vector2;
			string arg = ((currency == Currency.Dollar) ? "$" : "€");
			string text2 = $"+{arg}{diff}";
			if (!gainedMoney)
			{
				text2 = $"-{arg}{diff}";
			}
			text.text = text2;
			text.color = (gainedMoney ? GameInfo.GreenColor : GameInfo.RedColor);
			text.transform.localScale = Vector3.one;
			LeanTween.cancel(text.gameObject);
			LeanTween.moveLocal(text.gameObject, vector2 + text.transform.up * _moneyTextUpPos, _moneyTextMoveTime).setEase(LeanTweenType.easeOutBack);
			LeanTween.value(text.gameObject, 0f, 1f, _moneyTextLifeTime).setOnComplete((Action)delegate
			{
				RemoveMoneyText(text);
			});
			_curMoneyText++;
			if (_curMoneyText >= _moneyTexts.Count)
			{
				_curMoneyText = 0;
			}
		}
	}

	private void UpdateVisualMoney(float t)
	{
		_curMoney = (int)Mathf.LerpUnclamped(_prevMoney, _targetMoney, t);
		_moneyText.text = $"${_curMoney}";
		LayoutRebuilder.ForceRebuildLayoutImmediate(_moneyText.rectTransform);
		Vector2 sizeDelta = _moneyTextHolder.sizeDelta;
		sizeDelta.x = _moneyText.rectTransform.sizeDelta.x + 10f;
		_moneyTextHolder.sizeDelta = sizeDelta;
	}

	private void UpdateVisualEuro(float t)
	{
		_curEuro = (int)Mathf.LerpUnclamped(_prevEuro, _targetEuro, t);
		_euroText.text = $"€{_curEuro}";
		LayoutRebuilder.ForceRebuildLayoutImmediate(_euroText.rectTransform);
		Vector2 sizeDelta = _euroTextHolder.sizeDelta;
		sizeDelta.x = _euroText.rectTransform.sizeDelta.x + 10f;
		_euroTextHolder.sizeDelta = sizeDelta;
	}

	private void UpdateEuroAlpha(float to)
	{
		_euroTextGroup.alpha = to;
	}

	private void RemoveMoneyText(TextMeshProUGUI text)
	{
		LeanTween.cancel(text.gameObject);
		LeanTween.scale(text.gameObject, Vector3.zero, _moneyTextMoveTime).setEase(LeanTweenType.easeInBack);
	}
}
