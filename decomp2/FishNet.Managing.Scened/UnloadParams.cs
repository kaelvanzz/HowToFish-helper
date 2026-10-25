using System;

namespace FishNet.Managing.Scened;

public class UnloadParams
{
	[NonSerialized]
	public object[] ServerParams = new object[0];

	public byte[] ClientParams = new byte[0];
}
