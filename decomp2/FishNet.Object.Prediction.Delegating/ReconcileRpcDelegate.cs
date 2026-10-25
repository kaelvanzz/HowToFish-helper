using FishNet.Documenting;
using FishNet.Serializing;
using FishNet.Transporting;

namespace FishNet.Object.Prediction.Delegating;

[APIExclude]
public delegate void ReconcileRpcDelegate(PooledReader reader, Channel channel);
