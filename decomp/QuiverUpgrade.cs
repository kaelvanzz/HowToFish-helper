using System;
using UnityEngine;

[Serializable]
public class QuiverUpgrade
{
	[SerializeField]
	private float _chargeSpeed = 1f;

	[SerializeField]
	private Currency _currency;

	[SerializeField]
	private int _cost;

	public float ChargeSpeed => _chargeSpeed;

	public Currency Currency => _currency;

	public int Cost => _cost;
}
