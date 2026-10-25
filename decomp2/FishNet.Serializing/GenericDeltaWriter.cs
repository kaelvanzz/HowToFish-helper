using System;
using FishNet.Documenting;

namespace FishNet.Serializing;

[APIExclude]
public static class GenericDeltaWriter<T>
{
	internal static bool HasCustomSerializer;

	public static Func<Writer, T, T, DeltaSerializerOption, bool> Write { get; internal set; }

	public static void SetWrite(Func<Writer, T, T, DeltaSerializerOption, bool> value)
	{
		if (!HasCustomSerializer)
		{
			bool flag = value.Method.Name.StartsWith("GWrite___");
			if (!flag || !GenericWriter<T>.HasCustomSerializer)
			{
				HasCustomSerializer = !flag;
				Write = value;
			}
		}
	}
}
