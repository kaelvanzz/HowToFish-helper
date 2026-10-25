namespace FishNet.Component.Transforming;

public static class SnappedAxesExtensions
{
	public static bool FastContains(this SnappedAxes whole, SnappedAxes part)
	{
		return (whole & part) == part;
	}
}
