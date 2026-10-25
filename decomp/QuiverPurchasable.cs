public class QuiverPurchasable : Purchasable
{
	private string _fullDescription;

	private string _upgrade;

	private string _upgradeDescrption;

	public override void Hover()
	{
		Player localPlayer = Player.LocalPlayer;
		if (!localPlayer)
		{
			return;
		}
		string text = "";
		string text2 = "";
		bool flag = true;
		_upgrade = LocalizationManager.UpgradeQuiverLocalized.GetLocalizedString();
		_upgradeDescrption = LocalizationManager.QuiverDescriptionLocalized.GetLocalizedString();
		if (!localPlayer.Holding.HeldItem || localPlayer.Holding.HeldItem.SyncedHolder != localPlayer || !localPlayer.Holding.HeldItem.Weapon || !localPlayer.Holding.HeldItem.Weapon.UsesArrows)
		{
			flag = false;
			text2 = "\n(" + LocalizationManager.EquipdBowLocalized.GetLocalizedString() + ")";
			_customCanBuy = flag;
			text = _upgrade + " " + text2;
			if (text != _hoverString)
			{
				_isHovering = false;
				_hoverString = text;
			}
			base.Hover();
			return;
		}
		Weapon weapon = localPlayer.Holding.HeldItem.Weapon;
		string text3 = "";
		QuiverUpgrade curQuiverUpgrade = weapon.Attachments.GetCurQuiverUpgrade();
		QuiverUpgrade nextQuiverUpgrade = weapon.Attachments.GetNextQuiverUpgrade();
		if (nextQuiverUpgrade != null)
		{
			flag = true;
			_customCost = nextQuiverUpgrade.Cost;
			_customCurrency = nextQuiverUpgrade.Currency;
			char c = ((_customCurrency == Currency.Dollar) ? '$' : '€');
			text3 = $"\n{c}{_customCost}";
			_fullDescription = $"{_upgradeDescrption}\n{curQuiverUpgrade.ChargeSpeed} -> {nextQuiverUpgrade.ChargeSpeed}";
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

	protected override void OnStartHover()
	{
		base.OnStartHover();
		PlayerUI.ShowInspectInfo(this, _upgrade ?? "", _fullDescription);
	}

	public override void Interact(Player player)
	{
		if (!Server.Instance)
		{
			return;
		}
		base.Interact(player);
		if (!_customCanBuy || !MoneyManager.CanAfford(_customCost, _customCurrency) || !player.Holding.HeldItem || player.Holding.HeldItem.SyncedHolder != player || !player.Holding.HeldItem.Weapon)
		{
			CantBuyEffects();
			return;
		}
		Weapon weapon = player.Holding.HeldItem.Weapon;
		if (weapon.Attachments.GetNextQuiverUpgrade() == null)
		{
			CantBuyEffects();
		}
		else
		{
			Server.Instance.BuyQuiverUpgrade(weapon);
		}
	}
}
