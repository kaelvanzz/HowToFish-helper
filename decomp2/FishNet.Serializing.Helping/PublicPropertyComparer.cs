using System;

namespace FishNet.Serializing.Helping;

public class PublicPropertyComparer<T>
{
	public static Func<T, bool> IsDefault { get; set; }

	public static Func<T, T, bool> Compare { get; set; }
}
