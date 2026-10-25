using FishNet.Serializing;
using FishNet.Transporting;

namespace FishNet.Object.Delegating;

public delegate void ClientRpcDelegate(PooledReader reader, Channel channel);
