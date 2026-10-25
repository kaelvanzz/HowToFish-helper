namespace FishNet.Object.Synchronizing;

internal static class SyncTypeWriteFlagExtensions
{
	public static bool FastContains(this SyncTypeWriteFlag whole, SyncTypeWriteFlag part)
	{
		return (whole & part) == part;
	}
}
