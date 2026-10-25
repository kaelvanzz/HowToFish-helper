using FishNet.Documenting;
using FishNet.Transporting;

namespace FishNet.Object.Prediction.Delegating;

[APIExclude]
public delegate void ReconcileUserLogicDelegate<T>(T data, Channel channel);
