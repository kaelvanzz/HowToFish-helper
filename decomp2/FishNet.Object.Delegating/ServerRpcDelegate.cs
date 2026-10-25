using FishNet.Connection;
using FishNet.Serializing;
using FishNet.Transporting;

namespace FishNet.Object.Delegating;

public delegate void ServerRpcDelegate(PooledReader reader, Channel channel, NetworkConnection sender);
