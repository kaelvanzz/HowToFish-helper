public class WishbonePurchasable : Purchasable
{
	private string _fullDescription;

	private string _upgrade;

	private string _upgradeDescrption;

	public override void Hover()
	{
		Player localPlayer = Player.LocalPlayer;
		if ((bool)localPlayer)
		{
			string text = "";
			string text2 = "";
			bool flag = true;
			_upgrade = LocalizationManager.WishboneLocalized.GetLocalizedString();
			_upgradeDescrption = LocalizationManager.WishboneDescriptionLocalized.GetLocalizedString();
			string text3 = "";
			WishboneUpgrade curWishboneUpgrade = localPlayer.Inventory.GetCurWishboneUpgrade();
			WishboneUpgrade nextWishboneUpgrade = localPlayer.Inventory.GetNextWishboneUpgrade();
			if (nextWishboneUpgrade != null)
			{
				flag = true;
				_customCost = nextWishboneUpgrade.Cost;
				_customCurrency = nextWishboneUpgrade.Currency;
				char c = ((_customCurrency == Currency.Dollar) ? '$' : '€');
				text3 = $"\n{c}{_customCost}";
				int num = 10 + curWishboneUpgrade.ShinyCreatureChance;
				int num2 = 10 + nextWishboneUpgrade.ShinyCreatureChance;
				_fullDescription = $"{_upgradeDescrption}\n{num}% -> {num2}%";
			}
			else
			{
				flag = false;
				text2 = "(" + LocalizationManager.MaxedOnIslandLocalized.GetLocalizedString() + ")";
				_fullDescription = LocalizationManager.MaxedOnIslandLocalized.GetLocalizedString() ?? "";
			}
			_customCanBuy = flag;
			text = ((!_isFree) ? (_upgrade + " " + text2 + text3 + "\n" + SpriteManager.GetPickUpInput()) : (_upgrade + "\n" + SpriteManager.GetPickUpInput()));
			if (text != _hoverString)
			{
				_isHovering = false;
				_hoverString = text;
			}
			base.Hover();
		}
	}

	protected override void OnStartHover()
	{
		base.OnStartHover();
		PlayerUI.ShowInspectInfo(this, _upgrade ?? "", _fullDescription);
	}

	public override void Interact(Player player)
	{
		if ((bool)Server.Instance)
		{
			base.Interact(player);
			if (!_customCanBuy || !MoneyManager.CanAfford(_customCost, _customCurrency))
			{
				CantBuyEffects();
			}
			else if (player.Inventory.GetNextWishboneUpgrade() == null)
			{
				CantBuyEffects();
			}
			else
			{
				Server.Instance.BuyWishboneUpgrade(player);
			}
		}
	}
}
