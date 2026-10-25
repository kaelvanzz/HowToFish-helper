using System;
using UnityEngine;

[Serializable]
public class WishboneUpgrade
{
	[SerializeField]
	private int _shinyCreatureChance;

	[SerializeField]
	private Currency _currency;

	[SerializeField]
	private int _cost;

	public int ShinyCreatureChance => _shinyCreatureChance;

	public Currency Currency => _currency;

	public int Cost => _cost;
}
