namespace FishNet.Serializing.Helping;

internal static class QuaternionDeltaPrecisionFlagExtensions
{
	internal static bool FastContains(this QuaternionDeltaPrecisionFlag whole, QuaternionDeltaPrecisionFlag part)
	{
		return (whole & part) == part;
	}
}
