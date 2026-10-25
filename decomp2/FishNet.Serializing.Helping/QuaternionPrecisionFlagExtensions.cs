namespace FishNet.Serializing.Helping;

internal static class QuaternionPrecisionFlagExtensions
{
	internal static bool FastContains(this QuaternionPrecisionFlag whole, QuaternionPrecisionFlag part)
	{
		return (whole & part) == part;
	}
}
