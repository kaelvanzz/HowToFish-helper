using FishNet.Documenting;
using FishNet.Transporting;

namespace FishNet.Object.Prediction.Delegating;

[APIExclude]
public delegate void ReplicateUserLogicDelegate<T>(T data, ReplicateState state, Channel channel);
