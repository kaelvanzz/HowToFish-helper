using System.Runtime.InteropServices;

namespace FishNet.Serializing.Helping;

[StructLayout(LayoutKind.Explicit)]
internal struct UIntDouble
{
	[FieldOffset(0)]
	public double DoubleValue;

	[FieldOffset(0)]
	public ulong LongValue;
}
