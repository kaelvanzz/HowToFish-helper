using FishNet.Serializing;

namespace FishNet.Object.Delegating;

public delegate bool SyncVarReadDelegate(PooledReader reader, byte index, bool asServer);
