using FishNet.Documenting;

namespace FishNet.Managing.Object;

[APIExclude]
internal static class SpawnTypeExtensions
{
	public static bool FastContains(this SpawnType whole, SpawnType part)
	{
		return (whole & part) == part;
	}
}
