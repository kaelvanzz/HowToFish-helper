using FishNet.Documenting;
using FishNet.Serializing;

namespace FishNet.Object.Prediction;

[APIExclude]
public struct LocalReconcile<T> where T : IReconcileData
{
	public uint Tick;

	public PooledWriter Writer;

	public T Data;

	public void Initialize(uint tick, T data)
	{
		Tick = tick;
		Data = data;
		Writer = WriterPool.Retrieve();
		Writer.Write(data);
	}

	public void Dispose()
	{
		Data.Dispose();
		if (Writer != null)
		{
			WriterPool.Store(Writer);
		}
	}
}
