using System;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Serializing;
using FishNet.Serializing.Generated;
using FishNet.Transporting;
using UnityEngine;

public class MoneyManager : NetworkBehaviour
{
	public static MoneyManager Instance;

	public readonly SyncVar<int> _money = new SyncVar<int>();

	public readonly SyncVar<int> _euro = new SyncVar<int>();

	private const int _cheatMoney = 99999;

	private const int _cheatEuro = 999;

	private bool NetworkInitialize___EarlyMoneyManagerAssembly_002DCSharp_002Edll_Excuted;

	private bool NetworkInitialize___LateMoneyManagerAssembly_002DCSharp_002Edll_Excuted;

	public static int Money { get; private set; }

	public static int Euro { get; private set; }

	public static event Action OnItemSold;

	public virtual void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_MoneyManager_Assembly_002DCSharp_002Edll();
		NetworkInitialize___Late();
	}

	public override void OnStartServer()
	{
		_money.Value = ((SaveManager.CurServerSave != null) ? SaveManager.CurServerSave.Money : (ClientSettings.CheatsEnabled ? 99999 : 0));
		_euro.Value = ((SaveManager.CurServerSave != null) ? SaveManager.CurServerSave.Euro : (ClientSettings.CheatsEnabled ? 999 : 0));
	}

	public override void OnStartClient()
	{
		Money = _money.Value;
		Euro = _euro.Value;
	}

	public override void OnStopClient()
	{
		Money = 0;
		Euro = 0;
	}

	private void Update()
	{
		if (!ClientSettings.CheatsEnabled || !Player.LocalPlayer || Player.LocalPlayer.BlockInputs)
		{
			return;
		}
		if (Input.GetKeyDown(KeyCode.M))
		{
			if (Input.GetKey(KeyCode.LeftControl))
			{
				AddMoney(999, Player.LocalPlayer, Currency.Euro);
			}
			else
			{
				AddMoney(99999, Player.LocalPlayer);
			}
		}
		if (Input.GetKeyDown(KeyCode.N))
		{
			if (Input.GetKey(KeyCode.LeftControl))
			{
				RemoveMoney(999, Player.LocalPlayer, Currency.Euro);
			}
			else
			{
				RemoveMoney(99999, Player.LocalPlayer);
			}
		}
	}

	public static void AddMoney(int amount, Player player, Currency currency = Currency.Dollar)
	{
		if ((bool)Instance && Instance.IsServerInitialized)
		{
			switch (currency)
			{
			case Currency.Dollar:
				Instance._money.Value += Mathf.Abs(amount);
				break;
			case Currency.Euro:
				Instance._euro.Value += Mathf.Abs(amount);
				break;
			}
			Instance.ObserverMoneySound(increase: true, player);
			Instance.MoneySound(increase: true, player);
		}
	}

	public static void SellItem(Item item)
	{
		if ((bool)Instance && Instance.IsServerInitialized)
		{
			switch (item.Currency)
			{
			case Currency.Dollar:
				Instance._money.Value += item.TotalWorth;
				break;
			case Currency.Euro:
				Instance._euro.Value += item.TotalWorth;
				break;
			}
			Instance.ObserverMoneySound(increase: true, item.LastHolder);
			Instance.MoneySound(increase: true, item.LastHolder);
		}
	}

	public static void RemoveMoney(int amount, Player player, Currency currency = Currency.Dollar)
	{
		if (Instance.IsServerInitialized)
		{
			int value = Mathf.Clamp(((currency == Currency.Dollar) ? Instance._money.Value : Instance._euro.Value) - Mathf.Abs(amount), 0, int.MaxValue);
			switch (currency)
			{
			case Currency.Dollar:
				Instance._money.Value = value;
				break;
			case Currency.Euro:
				Instance._euro.Value = value;
				break;
			}
			Instance.ObserverMoneySound(increase: false, player);
			Instance.MoneySound(increase: false, player);
		}
	}

	[ObserversRpc(ExcludeServer = true)]
	private void ObserverMoneySound(bool increase, Player player)
	{
		RpcWriter___ObserverMoneySound___4168932475(increase, player);
	}

	private void MoneySound(bool increase, Player player)
	{
		if (increase)
		{
			AudioManager.PlayRandomPlayerClip("Sell_0", 1, 3, player, variation: false, AudioDistance.Long, 0.35f);
		}
		else
		{
			AudioManager.PlayRandomPlayerClip("Buy_0", 1, 3, player, variation: false, AudioDistance.Long, 0.35f);
		}
	}

	public static bool CanAfford(int cost, Currency currency = Currency.Dollar)
	{
		return currency switch
		{
			Currency.Dollar => cost <= Money, 
			Currency.Euro => cost <= Euro, 
			_ => cost <= Money, 
		};
	}

	private void OnChangeMoney(int prev, int next, bool asServer)
	{
		if (!asServer)
		{
			MoneyManager.OnItemSold?.Invoke();
			int num = next - Money;
			PlayerUI.SetMoney(next, Mathf.Abs(num), num > 0);
			Money = next;
		}
	}

	private void OnChangeEuro(int prev, int next, bool asServer)
	{
		if (!asServer)
		{
			MoneyManager.OnItemSold?.Invoke();
			int num = next - Euro;
			PlayerUI.SetMoney(next, Mathf.Abs(num), num > 0, Currency.Euro);
			Euro = next;
			AchievementManager.CheckFirstEuroAchievement(num);
		}
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyMoneyManagerAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___EarlyMoneyManagerAssembly_002DCSharp_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
			_euro.InitializeEarly(this, 1u, isSyncObject: false);
			_money.InitializeEarly(this, 0u, isSyncObject: false);
			RegisterObserversRpc(0u, RpcReader___ObserverMoneySound___4168932475);
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateMoneyManagerAssembly_002DCSharp_002Edll_Excuted)
		{
			NetworkInitialize___LateMoneyManagerAssembly_002DCSharp_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
			_euro.InitializeLate();
			_money.InitializeLate();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	private void RpcWriter___ObserverMoneySound___4168932475(bool increase, Player player)
	{
		if (!base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			return;
		}
		Channel channel = Channel.Reliable;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		pooledWriter.WriteBoolean(increase);
		GeneratedWriters___Internal.GWrite___PlayerFishNet_002ESerializing_002EGenerated(pooledWriter, player);
		SendObserversRpc(0u, pooledWriter, channel, DataOrderType.Default, bufferLast: false, excludeServer: true, excludeOwner: false);
		pooledWriter.Store();
	}

	private void RpcLogic___ObserverMoneySound___4168932475(bool P_0, Player P_1)
	{
		MoneySound(P_0, P_1);
	}

	private void RpcReader___ObserverMoneySound___4168932475(PooledReader PooledReader0, Channel channel)
	{
		bool flag = PooledReader0.ReadBoolean();
		Player player = GeneratedReaders___Internal.GRead___PlayerFishNet_002ESerializing_002EGenerateds(PooledReader0);
		if (base.IsClientInitialized)
		{
			RpcLogic___ObserverMoneySound___4168932475(flag, player);
		}
	}

	private void Awake_UserLogic_MoneyManager_Assembly_002DCSharp_002Edll()
	{
		Setter.SetSingleInstance(ref Instance, this);
		_money.OnChange += OnChangeMoney;
		_euro.OnChange += OnChangeEuro;
	}
}
