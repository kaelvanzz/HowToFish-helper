using GameKit.Dependencies.Utilities;

namespace FishNet.Broadcast.Helping;

internal static class BroadcastExtensions
{
	internal static ushort GetKey<T>()
	{
		return typeof(T).FullName.GetStableHashU16();
	}
}
