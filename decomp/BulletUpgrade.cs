using System;
using UnityEngine;

[Serializable]
public class BulletUpgrade
{
	[SerializeField]
	private int _damage;

	[SerializeField]
	private Currency _currency;

	[SerializeField]
	private int _cost;

	public int Damage => _damage;

	public Currency Currency => _currency;

	public int Cost => _cost;
}
