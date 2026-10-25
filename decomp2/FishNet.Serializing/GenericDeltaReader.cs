using System;
using FishNet.Documenting;

namespace FishNet.Serializing;

[APIExclude]
public static class GenericDeltaReader<T>
{
	internal static bool HasCustomSerializer;

	public static Func<Reader, T, T> Read { get; internal set; }

	public static void SetRead(Func<Reader, T, T> value)
	{
		if (!HasCustomSerializer)
		{
			bool flag = value.Method.Name.StartsWith("GRead___");
			if (!flag || !GenericReader<T>.HasCustomSerializer)
			{
				HasCustomSerializer = !flag;
				Read = value;
			}
		}
	}
}
