using System;
using FishNet.Documenting;

namespace FishNet.Serializing;

[APIExclude]
public static class GenericWriter<T>
{
	internal static bool HasCustomSerializer;

	public static Action<Writer, T> Write { get; private set; }

	public static void SetWrite(Action<Writer, T> value)
	{
		if (!HasCustomSerializer)
		{
			bool num = value.Method.Name.StartsWith("GWrite___");
			if (!num && GenericDeltaWriter<T>.HasCustomSerializer)
			{
				GenericDeltaWriter<T>.Write = null;
			}
			HasCustomSerializer = !num;
			Write = value;
		}
	}
}
