using System;
using System.Collections.Generic;
using FishNet.Managing;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Transporting.Multipass;

[AddComponentMenu("FishNet/Transport/Multipass")]
public class Multipass : Transport
{
	public struct ClientTransportData : IEquatable<ClientTransportData>
	{
		public int TransportIndex;

		public int TransportId;

		public int MultipassId;

		private int _hashCode;

		public ClientTransportData(int transportIndex, int transportId, int multipassId)
		{
			TransportIndex = transportIndex;
			TransportId = transportId;
			MultipassId = multipassId;
			_hashCode = (transportIndex, transportId, multipassId).GetHashCode();
		}

		public bool Equals(ClientTransportData other)
		{
			return _hashCode == other._hashCode;
		}
	}

	[Tooltip("While true server actions such as starting or stopping the server will run on all transport.")]
	public bool GlobalServerActions = true;

	private Transport _clientTransport;

	[Tooltip("Transports to use.")]
	[SerializeField]
	private List<Transport> _transports = new List<Transport>();

	private readonly ClientTransportData INVALID_CLIENTTRANSPORTDATA = new ClientTransportData(int.MinValue, int.MinValue, int.MinValue);

	private Dictionary<int, ClientTransportData> _multpassIdLookup = new Dictionary<int, ClientTransportData>();

	private List<Dictionary<int, ClientTransportData>> _transportIdLookup = new List<Dictionary<int, ClientTransportData>>();

	private Queue<int> _availableMultipassIds = new Queue<int>();

	private int _lastAvailableMultipassId;

	[HideInInspector]
	public Transport ClientTransport
	{
		get
		{
			if (_clientTransport == null)
			{
				if (_transports.Count != 0)
				{
					_clientTransport = _transports[0];
				}
				if (_clientTransport == null)
				{
					base.NetworkManager.LogError("ClientTransport in Multipass could not be set to the first transport. This can occur if no trnasports are specified or if the first entry is null.");
				}
				else
				{
					base.NetworkManager.LogError($"ClientTransport in Multipass is being automatically set to {_clientTransport.GetType()}. For production use SetClientTransport before attempting to access the ClientTransport.");
				}
			}
			return _clientTransport;
		}
		private set
		{
			_clientTransport = value;
		}
	}

	public IReadOnlyList<Transport> Transports => _transports;

	public override event Action<ClientConnectionStateArgs> OnClientConnectionState;

	public override event Action<ServerConnectionStateArgs> OnServerConnectionState;

	public override event Action<RemoteConnectionStateArgs> OnRemoteConnectionState;

	public override event Action<ClientReceivedDataArgs> OnClientReceivedData;

	public override event Action<ServerReceivedDataArgs> OnServerReceivedData;

	public override void Initialize(NetworkManager networkManager, int transportIndex)
	{
		base.Initialize(networkManager, transportIndex);
		for (int i = 0; i < _transports.Count; i++)
		{
			if (_transports[i] == null)
			{
				base.NetworkManager.LogWarning($"Transports contains a null entry on index {i}.");
				_transports.RemoveAt(i);
				i--;
			}
		}
		if (_transports.Count == 0)
		{
			base.NetworkManager.LogError("No transports are set within Multipass.");
			return;
		}
		for (int j = 0; j < _transports.Count; j++)
		{
			Dictionary<int, ClientTransportData> item = new Dictionary<int, ClientTransportData>();
			_transportIdLookup.Add(item);
			_transports[j].Initialize(networkManager, j);
			_transports[j].OnClientConnectionState += Multipass_OnClientConnectionState;
			_transports[j].OnServerConnectionState += Multipass_OnServerConnectionState;
			_transports[j].OnRemoteConnectionState += Multipass_OnRemoteConnectionState;
			_transports[j].OnClientReceivedData += Multipass_OnClientReceivedData;
			_transports[j].OnServerReceivedData += Multipass_OnServerReceivedData;
		}
	}

	private void OnDestroy()
	{
		foreach (Transport transport in _transports)
		{
			transport.Shutdown();
		}
		ResetLookupCollections();
	}

	private void ResetLookupCollections()
	{
		_multpassIdLookup.Clear();
		for (int i = 0; i < _transportIdLookup.Count; i++)
		{
			_transportIdLookup[i].Clear();
		}
	}

	private void TryResetClientIds(bool force)
	{
		if (!force)
		{
			foreach (Transport transport in _transports)
			{
				if (transport.GetConnectionState(server: true) == LocalConnectionState.Started)
				{
					return;
				}
			}
		}
		ResetLookupCollections();
		CreateAvailableIds(reset: true);
	}

	private ClientTransportData GetDataFromTransportId(int transportIndex, int transportId, bool log)
	{
		if (_transportIdLookup[transportIndex].TryGetValueIL2CPP(transportId, out var value))
		{
			return value;
		}
		if (log)
		{
			base.NetworkManager.LogError($"Multipass connectionId could not be found for transportIndex {transportIndex}, transportId of {transportId}.");
		}
		return INVALID_CLIENTTRANSPORTDATA;
	}

	private ClientTransportData GetDataFromMultipassId(int multipassId)
	{
		if (_multpassIdLookup.TryGetValueIL2CPP(multipassId, out var value))
		{
			return value;
		}
		base.NetworkManager.LogError($"TransportIdData could not be found for Multipass connectionId of {multipassId}.");
		return INVALID_CLIENTTRANSPORTDATA;
	}

	public override string GetConnectionAddress(int multipassId)
	{
		ClientTransportData dataFromMultipassId = GetDataFromMultipassId(multipassId);
		if (dataFromMultipassId.Equals(INVALID_CLIENTTRANSPORTDATA))
		{
			return string.Empty;
		}
		return _transports[dataFromMultipassId.TransportIndex].GetConnectionAddress(dataFromMultipassId.TransportId);
	}

	public override LocalConnectionState GetConnectionState(bool server)
	{
		if (server)
		{
			base.NetworkManager.LogError("This method is not supported for server. Use GetConnectionState(server, transportIndex) instead.");
			return LocalConnectionState.Stopped;
		}
		if (IsClientTransportSetWithError("GetConnectionState"))
		{
			return GetConnectionState(server, ClientTransport.Index);
		}
		return LocalConnectionState.Stopped;
	}

	public LocalConnectionState GetConnectionState(bool server, int transportIndex)
	{
		if (!IndexInRange(transportIndex, error: true))
		{
			return LocalConnectionState.Stopped;
		}
		return _transports[transportIndex].GetConnectionState(server);
	}

	public override RemoteConnectionState GetConnectionState(int multipassId)
	{
		ClientTransportData dataFromMultipassId = GetDataFromMultipassId(multipassId);
		if (dataFromMultipassId.Equals(INVALID_CLIENTTRANSPORTDATA))
		{
			return RemoteConnectionState.Stopped;
		}
		return _transports[dataFromMultipassId.TransportIndex].GetConnectionState(dataFromMultipassId.TransportId);
	}

	public RemoteConnectionState GetConnectionState(int connectionId, int index)
	{
		if (!IndexInRange(index, error: true))
		{
			return RemoteConnectionState.Stopped;
		}
		return _transports[index].GetConnectionState(connectionId);
	}

	private void Multipass_OnClientConnectionState(ClientConnectionStateArgs connectionStateArgs)
	{
		OnClientConnectionState?.Invoke(connectionStateArgs);
	}

	private void Multipass_OnServerConnectionState(ServerConnectionStateArgs connectionStateArgs)
	{
		OnServerConnectionState?.Invoke(connectionStateArgs);
		TryResetClientIds(force: false);
	}

	private void Multipass_OnRemoteConnectionState(RemoteConnectionStateArgs connectionStateArgs)
	{
		int transportIndex = connectionStateArgs.TransportIndex;
		int connectionId = connectionStateArgs.ConnectionId;
		Dictionary<int, ClientTransportData> dictionary = _transportIdLookup[transportIndex];
		if (connectionStateArgs.ConnectionState == RemoteConnectionState.Started)
		{
			if (_availableMultipassIds.Count == 0 && !CreateAvailableIds(reset: false))
			{
				base.NetworkManager.Log($"There are no more available connectionIds to use. Connection {connectionId} has been kicked.");
				_transports[transportIndex].StopConnection(connectionId, immediately: true);
				return;
			}
			int num = _availableMultipassIds.Dequeue();
			ClientTransportData value = (dictionary[connectionId] = new ClientTransportData(transportIndex, connectionId, num));
			_multpassIdLookup[num] = value;
			connectionStateArgs.ConnectionId = num;
			OnRemoteConnectionState?.Invoke(connectionStateArgs);
			return;
		}
		bool log = _transports[transportIndex].GetConnectionState(server: true) == LocalConnectionState.Started;
		ClientTransportData dataFromTransportId = GetDataFromTransportId(transportIndex, connectionId, log);
		if (!dataFromTransportId.Equals(INVALID_CLIENTTRANSPORTDATA))
		{
			_availableMultipassIds.Enqueue(dataFromTransportId.MultipassId);
			dictionary.Remove(connectionId);
			_multpassIdLookup.Remove(dataFromTransportId.MultipassId);
			connectionStateArgs.ConnectionId = dataFromTransportId.MultipassId;
			OnRemoteConnectionState?.Invoke(connectionStateArgs);
		}
	}

	public override void IterateIncoming(bool asServer)
	{
		foreach (Transport transport in _transports)
		{
			transport.IterateIncoming(asServer);
		}
	}

	public override void IterateOutgoing(bool asServer)
	{
		foreach (Transport transport in _transports)
		{
			transport.IterateOutgoing(asServer);
		}
	}

	private void Multipass_OnClientReceivedData(ClientReceivedDataArgs receivedDataArgs)
	{
		OnClientReceivedData?.Invoke(receivedDataArgs);
	}

	private void Multipass_OnServerReceivedData(ServerReceivedDataArgs receivedDataArgs)
	{
		ClientTransportData dataFromTransportId = GetDataFromTransportId(receivedDataArgs.TransportIndex, receivedDataArgs.ConnectionId, log: true);
		if (!dataFromTransportId.Equals(INVALID_CLIENTTRANSPORTDATA))
		{
			receivedDataArgs.ConnectionId = dataFromTransportId.MultipassId;
			OnServerReceivedData?.Invoke(receivedDataArgs);
		}
	}

	public override void SendToServer(byte channelId, ArraySegment<byte> segment)
	{
		if (ClientTransport != null)
		{
			ClientTransport.SendToServer(channelId, segment);
		}
	}

	public override void SendToClient(byte channelId, ArraySegment<byte> segment, int multipassId)
	{
		ClientTransportData dataFromMultipassId = GetDataFromMultipassId(multipassId);
		if (!dataFromMultipassId.Equals(INVALID_CLIENTTRANSPORTDATA))
		{
			_transports[dataFromMultipassId.TransportIndex].SendToClient(channelId, segment, dataFromMultipassId.TransportId);
		}
	}

	public void SendToClient(byte channelId, ArraySegment<byte> segment, int transportId, int transportIndex)
	{
		_transports[transportIndex].SendToClient(channelId, segment, transportId);
	}

	private bool UseGlobalServerActionsWithError(string methodText)
	{
		if (!GlobalServerActions)
		{
			base.NetworkManager.LogError("Method " + methodText + " is not supported while GlobalServerActions is false.");
			return false;
		}
		return true;
	}

	private bool IsClientTransportSetWithError(string methodText)
	{
		if (ClientTransport == null)
		{
			base.NetworkManager.LogError("ClientTransport is not set. Use SetClientTransport before calling " + methodText + ".");
			return false;
		}
		return true;
	}

	private bool CreateAvailableIds(bool reset)
	{
		if (reset)
		{
			_lastAvailableMultipassId = 0;
			_availableMultipassIds.Clear();
		}
		int num = 0;
		while (_lastAvailableMultipassId <= 2147483646 && num < 1000)
		{
			num++;
			_availableMultipassIds.Enqueue(_lastAvailableMultipassId);
			_lastAvailableMultipassId++;
		}
		return num > 0;
	}

	public void SetClientTransport<T>()
	{
		int clientTransport = -1;
		for (int i = 0; i < _transports.Count; i++)
		{
			if (_transports[i].GetType() == typeof(T))
			{
				clientTransport = i;
				break;
			}
		}
		SetClientTransport(clientTransport);
	}

	public void SetClientTransport(Type type)
	{
		int clientTransport = -1;
		for (int i = 0; i < _transports.Count; i++)
		{
			if (_transports[i].GetType() == type)
			{
				clientTransport = i;
				break;
			}
		}
		SetClientTransport(clientTransport);
	}

	public void SetClientTransport(Transport transport)
	{
		int clientTransport = -1;
		for (int i = 0; i < _transports.Count; i++)
		{
			if (_transports[i] == transport)
			{
				clientTransport = i;
				break;
			}
		}
		SetClientTransport(clientTransport);
	}

	public void SetClientTransport(int index)
	{
		if (IndexInRange(index, error: true))
		{
			ClientTransport = _transports[index];
		}
	}

	public Transport GetTransport(int index)
	{
		if (!IndexInRange(index, error: true))
		{
			return null;
		}
		return _transports[index];
	}

	public T GetTransport<T>()
	{
		foreach (Transport transport in _transports)
		{
			if (transport.GetType() == typeof(T))
			{
				return (T)(object)transport;
			}
		}
		return default(T);
	}

	public override bool IsLocalTransport(int connectionId)
	{
		using (List<Transport>.Enumerator enumerator = _transports.GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				return enumerator.Current.IsLocalTransport(connectionId);
			}
		}
		return false;
	}

	public bool IsLocalTransport(int transportId, int connectionId)
	{
		if (!IndexInRange(transportId, error: true))
		{
			return false;
		}
		return _transports[transportId].IsLocalTransport(connectionId);
	}

	public override int GetMaximumClients()
	{
		base.NetworkManager.LogError("This method is not supported. Use GetMaximumClients(transportIndex) instead.");
		return -1;
	}

	public int GetMaximumClients(int transportIndex)
	{
		if (!IndexInRange(transportIndex, error: true))
		{
			return -1;
		}
		return _transports[transportIndex].GetMaximumClients();
	}

	public override void SetMaximumClients(int value)
	{
		foreach (Transport transport in _transports)
		{
			transport.SetMaximumClients(value);
		}
	}

	public void SetMaximumClients(int value, int transportIndex)
	{
		if (IndexInRange(transportIndex, error: true))
		{
			_transports[transportIndex].SetMaximumClients(value);
		}
	}

	public override void SetClientAddress(string address)
	{
		foreach (Transport transport in _transports)
		{
			transport.SetClientAddress(address);
		}
	}

	public void SetClientAddress(string address, int index)
	{
		if (IndexInRange(index, error: true))
		{
			_transports[index].SetClientAddress(address);
		}
	}

	public override void SetServerBindAddress(string address, IPAddressType addressType)
	{
		foreach (Transport transport in _transports)
		{
			transport.SetServerBindAddress(address, addressType);
		}
	}

	public void SetServerBindAddress(string address, IPAddressType addressType, int index)
	{
		if (IndexInRange(index, error: true))
		{
			_transports[index].SetServerBindAddress(address, addressType);
		}
	}

	public override void SetPort(ushort port)
	{
		foreach (Transport transport in _transports)
		{
			transport.SetPort(port);
		}
	}

	public void SetPort(ushort port, int index)
	{
		if (IndexInRange(index, error: true))
		{
			_transports[index].SetPort(port);
		}
	}

	public override ushort GetPort()
	{
		using (List<Transport>.Enumerator enumerator = _transports.GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				return enumerator.Current.GetPort();
			}
		}
		return base.GetPort();
	}

	public override bool StartConnection(bool server)
	{
		if (server)
		{
			if (!UseGlobalServerActionsWithError("StartConnection"))
			{
				return false;
			}
			bool result = true;
			for (int i = 0; i < _transports.Count; i++)
			{
				if (!StartConnection(server: true, i))
				{
					result = false;
				}
			}
			return result;
		}
		if (IsClientTransportSetWithError("StartConnection"))
		{
			return StartConnection(server: false, ClientTransport.Index);
		}
		return false;
	}

	public bool StartConnection(bool server, int index)
	{
		if (server)
		{
			return StartServer(index);
		}
		if (IsClientTransportSetWithError("StartConnection"))
		{
			return StartClient();
		}
		return false;
	}

	public override bool StopConnection(bool server)
	{
		if (server)
		{
			if (!UseGlobalServerActionsWithError("StopConnection"))
			{
				return false;
			}
			bool result = true;
			for (int i = 0; i < _transports.Count; i++)
			{
				if (!StopConnection(server: true, i))
				{
					result = false;
				}
			}
			return result;
		}
		if (IsClientTransportSetWithError("StopConnection"))
		{
			return StopConnection(server: false, ClientTransport.Index);
		}
		return false;
	}

	public bool StopConnection(bool server, int index)
	{
		if (server)
		{
			return StopServer(index);
		}
		if (IsClientTransportSetWithError("StopConnection"))
		{
			return StopClient();
		}
		return false;
	}

	public override bool StopConnection(int connectionId, bool immediately)
	{
		return StopClient(connectionId, immediately);
	}

	public bool StopServerConnection(bool sendDisconnectMessage, int transportIndex)
	{
		if (sendDisconnectMessage)
		{
			Dictionary<int, ClientTransportData> dictionary = _transportIdLookup[transportIndex];
			int[] array = new int[dictionary.Count];
			int num = 0;
			foreach (ClientTransportData value in dictionary.Values)
			{
				array[num++] = value.MultipassId;
			}
			base.NetworkManager.ServerManager.SendDisconnectMessages(array);
			_transports[transportIndex].IterateOutgoing(asServer: true);
		}
		return StopConnection(server: true, transportIndex);
	}

	public override void Shutdown()
	{
		foreach (Transport transport in _transports)
		{
			transport.StopConnection(server: false);
			transport.StopConnection(server: true);
		}
	}

	private bool StartServer(int index)
	{
		if (!IndexInRange(index, error: true))
		{
			return false;
		}
		return _transports[index].StartConnection(server: true);
	}

	private bool StopServer(int index)
	{
		if (!IndexInRange(index, error: true))
		{
			return false;
		}
		return _transports[index].StopConnection(server: true);
	}

	private bool StartClient()
	{
		return ClientTransport.StartConnection(server: false);
	}

	private bool StopClient()
	{
		return ClientTransport.StopConnection(server: false);
	}

	private bool StopClient(int multipassId, bool immediately)
	{
		ClientTransportData dataFromMultipassId = GetDataFromMultipassId(multipassId);
		if (dataFromMultipassId.Equals(INVALID_CLIENTTRANSPORTDATA))
		{
			return false;
		}
		return _transports[dataFromMultipassId.TransportIndex].StopConnection(dataFromMultipassId.TransportId, immediately);
	}

	public override int GetMTU(byte channel)
	{
		return GetMTU(channel, 0);
	}

	public int GetMTU(byte channel, int index)
	{
		if (!IndexInRange(index, error: true))
		{
			return -1;
		}
		return _transports[index].GetMTU(channel);
	}

	private bool IndexInRange(int index, bool error)
	{
		if (index >= _transports.Count || index < 0)
		{
			if (error)
			{
				base.NetworkManager.LogError($"Index of {index} is out of Transports range.");
			}
			return false;
		}
		return true;
	}

	public override void HandleServerConnectionState(ServerConnectionStateArgs connectionStateArgs)
	{
	}

	public override void HandleRemoteConnectionState(RemoteConnectionStateArgs connectionStateArgs)
	{
	}

	public override void HandleClientReceivedDataArgs(ClientReceivedDataArgs receivedDataArgs)
	{
	}

	public override void HandleServerReceivedDataArgs(ServerReceivedDataArgs receivedDataArgs)
	{
	}

	public override void HandleClientConnectionState(ClientConnectionStateArgs connectionStateArgs)
	{
	}
}
