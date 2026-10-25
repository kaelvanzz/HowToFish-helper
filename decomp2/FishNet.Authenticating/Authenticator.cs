using System;
using FishNet.Connection;
using FishNet.Managing;
using UnityEngine;

namespace FishNet.Authenticating;

public abstract class Authenticator : MonoBehaviour
{
	public bool Initialized { get; private set; }

	protected NetworkManager NetworkManager { get; private set; }

	public abstract event Action<NetworkConnection, bool> OnAuthenticationResult;

	public virtual void InitializeOnce(NetworkManager networkManager)
	{
		NetworkManager = networkManager;
		Initialized = true;
	}

	public virtual void OnRemoteConnection(NetworkConnection connection)
	{
	}
}
