using System;

namespace FishNet.Managing.Scened;

public class LoadParams
{
	[NonSerialized]
	public object[] ServerParams = new object[0];

	public byte[] ClientParams = new byte[0];
}
