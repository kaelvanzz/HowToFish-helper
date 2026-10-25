using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Serializing;
using FishNet.Serializing.Generated;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Component.Ownership;

public class PredictedOwner : NetworkBehaviour
{
	[Tooltip("True if to enable this component.")]
	[SerializeField]
	private bool _allowTakeOwnership = true;

	public readonly SyncVar<bool> _allowTakeOwnershipSyncVar = new SyncVar<bool>();

	private bool NetworkInitialize___EarlyFishNet_002EComponent_002EOwnership_002EPredictedOwnerFishNet_002ERuntime_002Edll_Excuted;

	private bool NetworkInitialize___LateFishNet_002EComponent_002EOwnership_002EPredictedOwnerFishNet_002ERuntime_002Edll_Excuted;

	public bool TakingOwnership { get; private set; }

	public NetworkConnection PreviousOwner { get; private set; } = NetworkManager.EmptyConnection;


	[Server]
	public void SetAllowTakeOwnership(bool value)
	{
		if (GetIsNetworked() && !base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
		}
		else
		{
			_allowTakeOwnershipSyncVar.Value = value;
		}
	}

	public virtual void Awake()
	{
		NetworkInitialize___Early();
		Awake_UserLogic_FishNet_002EComponent_002EOwnership_002EPredictedOwner_FishNet_002ERuntime_002Edll();
		NetworkInitialize___Late();
	}

	private void _allowTakeOwnershipSyncVar_OnChange(bool prev, bool next, bool asServer)
	{
		if (asServer || !base.IsHostStarted)
		{
			_allowTakeOwnership = next;
		}
	}

	public override void OnOwnershipClient(NetworkConnection prevOwner)
	{
		TakingOwnership = false;
		PreviousOwner = base.Owner;
	}

	[Client]
	[Obsolete("Use TakeOwnership(bool).")]
	public virtual void TakeOwnership()
	{
		if (GetIsNetworked() && !base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
		}
		else
		{
			TakeOwnership(includeNested: true);
		}
	}

	public virtual void TakeOwnership(bool includeNested)
	{
		if (_allowTakeOwnershipSyncVar.Value && !base.IsOwner)
		{
			NetworkConnection connection = base.ClientManager.Connection;
			TakingOwnership = true;
			if (!base.IsServerStarted)
			{
				base.NetworkObject.SetLocalOwnership(connection, includeNested);
				ServerTakeOwnership(includeNested);
			}
			else
			{
				OnTakeOwnership(connection, includeNested);
			}
		}
	}

	[ServerRpc(RequireOwnership = false)]
	private void ServerTakeOwnership(bool includeNested, NetworkConnection caller = null)
	{
		RpcWriter___ServerTakeOwnership___3179012907(includeNested, caller);
	}

	[Server]
	[Obsolete("Use OnTakeOwnership(bool).")]
	protected virtual void OnTakeOwnership(NetworkConnection caller)
	{
		if (GetIsNetworked() && !base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
		}
		else
		{
			OnTakeOwnership(caller, recursive: false);
		}
	}

	[Server]
	protected virtual void OnTakeOwnership(NetworkConnection caller, bool recursive)
	{
		if (GetIsNetworked() && !base.IsServerInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because server is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
		}
		else
		{
			if (!caller.IsActive || !_allowTakeOwnershipSyncVar.Value || caller == base.Owner)
			{
				return;
			}
			GiveOwnership(caller);
			if (!recursive)
			{
				return;
			}
			List<NetworkObject> networkObjects = base.NetworkObject.GetNetworkObjects(GetNetworkObjectOption.AllNestedRecursive);
			foreach (NetworkObject item in networkObjects)
			{
				PredictedOwner predictedOwner = item.PredictedOwner;
				if (predictedOwner != null)
				{
					predictedOwner.OnTakeOwnership(caller, recursive: true);
				}
			}
			CollectionCaches<NetworkObject>.Store(networkObjects);
		}
	}

	public override void NetworkInitialize___Early()
	{
		if (!NetworkInitialize___EarlyFishNet_002EComponent_002EOwnership_002EPredictedOwnerFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___EarlyFishNet_002EComponent_002EOwnership_002EPredictedOwnerFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Early();
			_allowTakeOwnershipSyncVar.InitializeEarly(this, 0u, isSyncObject: false);
			RegisterServerRpc(0u, RpcReader___ServerTakeOwnership___3179012907);
		}
	}

	public override void NetworkInitialize___Late()
	{
		if (!NetworkInitialize___LateFishNet_002EComponent_002EOwnership_002EPredictedOwnerFishNet_002ERuntime_002Edll_Excuted)
		{
			NetworkInitialize___LateFishNet_002EComponent_002EOwnership_002EPredictedOwnerFishNet_002ERuntime_002Edll_Excuted = true;
			base.NetworkInitialize___Late();
			_allowTakeOwnershipSyncVar.InitializeLate();
		}
	}

	public override void NetworkInitializeIfDisabled()
	{
		NetworkInitialize___Early();
		NetworkInitialize___Late();
	}

	private void RpcWriter___ServerTakeOwnership___3179012907(bool includeNested, NetworkConnection caller = null)
	{
		if (!base.IsClientInitialized)
		{
			NetworkManager networkManager = base.NetworkManager;
			networkManager.LogWarning("Cannot complete action because client is not active. This may also occur if the object is not yet initialized, has deinitialized, or if it does not contain a NetworkObject component.");
			return;
		}
		Channel channel = Channel.Reliable;
		PooledWriter pooledWriter = WriterPool.Retrieve();
		GeneratedWriters___Internal.InstancedExtension___WriteBoolean(pooledWriter, includeNested);
		SendServerRpc(0u, pooledWriter, channel, DataOrderType.Default);
		pooledWriter.Store();
	}

	private void RpcLogic___ServerTakeOwnership___3179012907(bool P_0, NetworkConnection P_1)
	{
		OnTakeOwnership(P_1, P_0);
	}

	private void RpcReader___ServerTakeOwnership___3179012907(PooledReader PooledReader0, Channel channel, NetworkConnection conn)
	{
		bool flag = GeneratedReaders___Internal.InstancedExtension___ReadBoolean(PooledReader0);
		if (base.IsServerInitialized)
		{
			RpcLogic___ServerTakeOwnership___3179012907(flag, conn);
		}
	}

	protected virtual void Awake_UserLogic_FishNet_002EComponent_002EOwnership_002EPredictedOwner_FishNet_002ERuntime_002Edll()
	{
		_allowTakeOwnershipSyncVar.Value = _allowTakeOwnership;
		_allowTakeOwnershipSyncVar.UpdateSendRate(0f);
		_allowTakeOwnershipSyncVar.OnChange += _allowTakeOwnershipSyncVar_OnChange;
	}
}
