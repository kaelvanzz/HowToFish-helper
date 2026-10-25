namespace FishNet.Object;

public static class TransformPropertiesOptionExtensions
{
	public static bool FastContains(this TransformPropertiesFlag whole, TransformPropertiesFlag part)
	{
		return (whole & part) == part;
	}
}
