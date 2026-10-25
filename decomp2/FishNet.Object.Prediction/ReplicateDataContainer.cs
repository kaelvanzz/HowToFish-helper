using FishNet.CodeGenerating;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;

namespace FishNet.Object.Prediction;

[MakePublic]
public struct ReplicateDataContainer<T> where T : IReplicateData, new()
{
	private enum DataCachingType
	{
		Unset,
		ValueType,
		IResettableReferenceType,
		ReferenceType
	}

	public T Data;

	public bool IsCreated;

	public readonly Channel Channel;

	private static DataCachingType _dataCachingType;

	public bool IsValid { get; private set; }

	public ReplicateDataContainer(T data, Channel channel)
		: this(data, channel, 0u)
	{
	}

	public ReplicateDataContainer(T data, Channel channel, bool isCreated)
		: this(data, channel, 0u, isCreated)
	{
	}

	public ReplicateDataContainer(T data, Channel channel, uint tick, bool isCreated = false)
	{
		Data = data;
		Channel = channel;
		IsCreated = isCreated;
		IsValid = true;
		SetDataTick(tick);
	}

	public void SetDataTick(uint tick)
	{
		SetDataIfNull(ref Data);
		Data.SetTick(tick);
	}

	private void SetDataIfNull(ref T data)
	{
		if (_dataCachingType == DataCachingType.Unset)
		{
			if (typeof(T).IsValueType)
			{
				_dataCachingType = DataCachingType.ValueType;
			}
			else if (typeof(IResettable).IsAssignableFrom(typeof(T)))
			{
				_dataCachingType = DataCachingType.IResettableReferenceType;
			}
			else
			{
				_dataCachingType = DataCachingType.ReferenceType;
			}
		}
		if (_dataCachingType != DataCachingType.ValueType && data == null)
		{
			data = ObjectCaches<T>.Retrieve();
		}
	}

	public void Dispose()
	{
		if (Data != null)
		{
			Data.Dispose();
		}
		IsValid = false;
	}

	public static ReplicateDataContainer<T> GetDefault(uint tick)
	{
		return new ReplicateDataContainer<T>(default(T), Channel.Unreliable, tick);
	}

	public static ReplicateDataContainer<T> GetDefault()
	{
		return GetDefault(0u);
	}
}
