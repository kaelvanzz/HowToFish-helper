using System;

namespace FishNet.Serializing;

[Serializable]
internal class TransformPackingData
{
	public AutoPackType Position = AutoPackType.Packed;

	public AutoPackType Rotation = AutoPackType.Packed;

	public AutoPackType Scale = AutoPackType.Packed;
}
