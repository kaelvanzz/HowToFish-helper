using FishNet.Connection;
using FishNet.Documenting;
using FishNet.Serializing;
using FishNet.Transporting;

namespace FishNet.Object.Prediction.Delegating;

[APIExclude]
public delegate void ReplicateRpcDelegate(PooledReader reader, NetworkConnection sender, Channel channel);
