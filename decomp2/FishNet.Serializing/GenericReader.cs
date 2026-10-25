using System;
using FishNet.Documenting;

namespace FishNet.Serializing;

[APIExclude]
public static class GenericReader<T>
{
	internal static bool HasCustomSerializer;

	public static Func<Reader, T> Read { get; set; }

	public static void SetRead(Func<Reader, T> value)
	{
		if (!HasCustomSerializer)
		{
			bool num = value.Method.Name.StartsWith("GRead___");
			if (!num && GenericDeltaReader<T>.HasCustomSerializer)
			{
				GenericDeltaReader<T>.Read = null;
			}
			HasCustomSerializer = !num;
			Read = value;
		}
	}
}
