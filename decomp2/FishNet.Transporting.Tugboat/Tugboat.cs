using System;
using FishNet.Managing;
using FishNet.Transporting.Tugboat.Client;
using FishNet.Transporting.Tugboat.Server;
using LiteNetLib;
using LiteNetLib.Layers;
using UnityEngine;

namespace FishNet.Transporting.Tugboat;

[DisallowMultipleComponent]
[AddComponentMenu("FishNet/Transport/Tugboat")]
public class Tugboat : Transport
{
	[Tooltip("True to stop local server and client sockets using a new thread.")]
	[SerializeField]
	private bool _stopSocketsOnThread;

	[Tooltip("While true, forces sockets to send data directly to interface without routing.")]
	[SerializeField]
	private bool _dontRoute;

	[Tooltip("Allows the same address and port to be used multiple times by the server. This can be useful if you wish to launch multiple builds or server instances on the same machine using the same configuration.")]
	[SerializeField]
	private bool _reuseAddress;

	[Tooltip("Maximum transmission unit for the unreliable channel.")]
	[Range(576f, 1023f)]
	[SerializeField]
	private int _unreliableMtu = 1023;

	[Tooltip("IPv4 Address to bind server to.")]
	[SerializeField]
	private string _ipv4BindAddress;

	[Tooltip("Enable IPv6, Server listens on IPv4 and IPv6 address")]
	[SerializeField]
	private bool _enableIpv6 = true;

	[Tooltip("IPv6 Address to bind server to.")]
	[SerializeField]
	private string _ipv6BindAddress;

	[Tooltip("Port to use.")]
	[SerializeField]
	private ushort _port = 7770;

	[Tooltip("Maximum number of players which may be connected at once.")]
	[Range(1f, 9999f)]
	[SerializeField]
	private int _maximumClients = 4095;

	[Tooltip("Address to connect.")]
	[SerializeField]
	private string _clientAddress = "localhost";

	private PacketLayerBase _packetLayer;

	public ServerSocket ServerSocket = new ServerSocket();

	public ClientSocket ClientSocket = new ClientSocket();

	private int _clientTimeout = 1800;

	private int _serverTimeout = 1800;

	private const ushort MAX_TIMEOUT_SECONDS = 1800;

	private const int MINIMUM_UDP_MTU = 576;

	private const int MAXIMUM_UDP_MTU = 1023;

	internal bool StopSocketsOnThread => _stopSocketsOnThread;

	internal bool DontRoute => _dontRoute;

	internal bool ReuseAddress => _reuseAddress;

	public override event Action<ClientConnectionStateArgs> OnClientConnectionState;

	public override event Action<ServerConnectionStateArgs> OnServerConnectionState;

	public override event Action<RemoteConnectionStateArgs> OnRemoteConnectionState;

	public override event Action<ClientReceivedDataArgs> OnClientReceivedData;

	public override event Action<ServerReceivedDataArgs> OnServerReceivedData;

	~Tugboat()
	{
		Shutdown();
	}

	public override void Initialize(NetworkManager networkManager, int transportIndex)
	{
		base.Initialize(networkManager, transportIndex);
		networkManager.TimeManager.OnUpdate += TimeManager_OnUpdate;
	}

	protected void OnDestroy()
	{
		Shutdown();
		if (base.NetworkManager != null)
		{
			base.NetworkManager.TimeManager.OnUpdate -= TimeManager_OnUpdate;
		}
	}

	public override string GetConnectionAddress(int connectionId)
	{
		return ServerSocket.GetConnectionAddress(connectionId);
	}

	public override LocalConnectionState GetConnectionState(bool server)
	{
		if (server)
		{
			return ServerSocket.GetConnectionState();
		}
		return ClientSocket.GetConnectionState();
	}

	public override RemoteConnectionState GetConnectionState(int connectionId)
	{
		return ServerSocket.GetConnectionState(connectionId);
	}

	public override void HandleClientConnectionState(ClientConnectionStateArgs connectionStateArgs)
	{
		OnClientConnectionState?.Invoke(connectionStateArgs);
	}

	public override void HandleServerConnectionState(ServerConnectionStateArgs connectionStateArgs)
	{
		OnServerConnectionState?.Invoke(connectionStateArgs);
	}

	public override void HandleRemoteConnectionState(RemoteConnectionStateArgs connectionStateArgs)
	{
		OnRemoteConnectionState?.Invoke(connectionStateArgs);
	}

	private void TimeManager_OnUpdate()
	{
		ServerSocket?.PollSocket();
		ClientSocket?.PollSocket();
	}

	public override void IterateIncoming(bool asServer)
	{
		if (asServer)
		{
			ServerSocket.IterateIncoming();
		}
		else
		{
			ClientSocket.IterateIncoming();
		}
	}

	public override void IterateOutgoing(bool asServer)
	{
		if (asServer)
		{
			ServerSocket.IterateOutgoing();
		}
		else
		{
			ClientSocket.IterateOutgoing();
		}
	}

	public override void SendToServer(byte channelId, ArraySegment<byte> segment)
	{
		SanitizeChannel(ref channelId);
		ClientSocket.SendToServer(channelId, segment);
	}

	public override void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
	{
		SanitizeChannel(ref channelId);
		ServerSocket.SendToClient(channelId, segment, connectionId);
	}

	public override void HandleClientReceivedDataArgs(ClientReceivedDataArgs receivedDataArgs)
	{
		OnClientReceivedData?.Invoke(receivedDataArgs);
	}

	public override void HandleServerReceivedDataArgs(ServerReceivedDataArgs receivedDataArgs)
	{
		OnServerReceivedData?.Invoke(receivedDataArgs);
	}

	public override float GetPacketLoss(bool asServer)
	{
		NetManager netManager = ((asServer && ServerSocket != null) ? ServerSocket.NetManager : ((asServer || ClientSocket == null) ? null : ClientSocket.NetManager));
		if (netManager == null)
		{
			return 0f;
		}
		return netManager.Statistics.PacketLossPercent;
	}

	public void SetPacketLayer(PacketLayerBase packetLayer)
	{
		_packetLayer = packetLayer;
		if (GetConnectionState(server: true) != LocalConnectionState.Stopped)
		{
			base.NetworkManager.LogWarning("PacketLayer is set but will not be applied until the server stops.");
		}
		if (GetConnectionState(server: false) != LocalConnectionState.Stopped)
		{
			base.NetworkManager.LogWarning("PacketLayer is set but will not be applied until the client stops.");
		}
		InitializeSocket(asServer: true);
		InitializeSocket(asServer: false);
	}

	public override float GetTimeout(bool asServer)
	{
		return 1800f;
	}

	public override void SetTimeout(float value, bool asServer)
	{
		int num = (int)Math.Ceiling(value);
		if (asServer)
		{
			_serverTimeout = num;
		}
		else
		{
			_clientTimeout = num;
		}
		UpdateTimeout();
	}

	public override int GetMaximumClients()
	{
		return ServerSocket.GetMaximumClients();
	}

	public override void SetMaximumClients(int value)
	{
		_maximumClients = value;
		ServerSocket.SetMaximumClients(value);
	}

	public override void SetClientAddress(string address)
	{
		_clientAddress = address;
	}

	public override string GetClientAddress()
	{
		return _clientAddress;
	}

	public override void SetServerBindAddress(string address, IPAddressType addressType)
	{
		if (addressType == IPAddressType.IPv4)
		{
			_ipv4BindAddress = address;
		}
		else
		{
			_ipv6BindAddress = address;
		}
	}

	public override string GetServerBindAddress(IPAddressType addressType)
	{
		if (addressType == IPAddressType.IPv4)
		{
			return _ipv4BindAddress;
		}
		return _ipv6BindAddress;
	}

	public override void SetPort(ushort port)
	{
		_port = port;
	}

	public override ushort GetPort()
	{
		ushort? num = ServerSocket?.GetPort();
		if (num.HasValue)
		{
			return num.Value;
		}
		num = ClientSocket?.GetPort();
		if (num.HasValue)
		{
			return num.Value;
		}
		return _port;
	}

	public override bool StartConnection(bool server)
	{
		if (server)
		{
			return StartServer();
		}
		return StartClient(_clientAddress);
	}

	public override bool StopConnection(bool server)
	{
		if (server)
		{
			return StopServer();
		}
		return StopClient();
	}

	public override bool StopConnection(int connectionId, bool immediately)
	{
		return ServerSocket.StopConnection(connectionId);
	}

	public override void Shutdown()
	{
		StopConnection(server: false);
		StopConnection(server: true);
	}

	private void InitializeSocket(bool asServer)
	{
		if (asServer)
		{
			ServerSocket.Initialize(this, _unreliableMtu, _packetLayer, _enableIpv6);
		}
		else
		{
			ClientSocket.Initialize(this, _unreliableMtu, _packetLayer);
		}
	}

	private bool StartServer()
	{
		InitializeSocket(asServer: true);
		UpdateTimeout();
		return ServerSocket.StartConnection(_port, _maximumClients, _ipv4BindAddress, _ipv6BindAddress);
	}

	private bool StopServer()
	{
		if (ServerSocket == null)
		{
			return false;
		}
		return ServerSocket.StopConnection();
	}

	private bool StartClient(string address)
	{
		InitializeSocket(asServer: false);
		UpdateTimeout();
		return ClientSocket.StartConnection(address, _port);
	}

	private void UpdateTimeout()
	{
		ClientSocket.UpdateTimeout(_clientTimeout);
		ServerSocket.UpdateTimeout(_serverTimeout);
	}

	private bool StopClient()
	{
		if (ClientSocket == null)
		{
			return false;
		}
		return ClientSocket.StopConnection();
	}

	private void SanitizeChannel(ref byte channelId)
	{
		if (channelId < 0 || channelId >= 2)
		{
			base.NetworkManager.LogWarning($"Channel of {channelId} is out of range of supported channels. Channel will be defaulted to reliable.");
			channelId = 0;
		}
	}

	public override int GetMTU(byte channel)
	{
		return _unreliableMtu;
	}
}
