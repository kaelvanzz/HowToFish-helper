namespace FishNet.Serializing;

public static class DeltaTypeExtensions
{
	public static bool FastContains(this UDeltaPrecisionType whole, UDeltaPrecisionType part)
	{
		return (whole & part) == part;
	}

	public static bool FastContains(this UDeltaPrecisionType whole, UDeltaPrecisionType part, int shift)
	{
		return FastContains((int)whole, (int)part, shift);
	}

	public static bool FastContains(this DeltaVector3Type whole, DeltaVector3Type part)
	{
		return (whole & part) == part;
	}

	public static bool FastContains(this DeltaVector3Type whole, DeltaVector3Type part, int shift)
	{
		return FastContains((int)whole, (int)part, shift);
	}

	public static bool FastContains(this DeltaVector2Type whole, DeltaVector2Type part)
	{
		return (whole & part) == part;
	}

	private static bool FastContains(int whole, int part, int shift)
	{
		int num = part >> shift;
		return (whole & num) == num;
	}
}
