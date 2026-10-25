namespace FishNet.Object;

internal static class GetNetworkObjectOptionExtensions
{
	public static bool FastContains(this GetNetworkObjectOption whole, GetNetworkObjectOption part)
	{
		return (whole & part) == part;
	}
}
