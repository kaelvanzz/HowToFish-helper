using GameKit.Dependencies.Utilities;

namespace FishNet.Serializing.Helping;

internal static class ReservedWritersExtensions
{
	public static void Store(this ReservedLengthWriter rlw)
	{
		ResettableObjectCaches<ReservedLengthWriter>.Store(rlw);
	}

	public static ReservedLengthWriter Retrieve()
	{
		return ResettableObjectCaches<ReservedLengthWriter>.Retrieve();
	}
}
