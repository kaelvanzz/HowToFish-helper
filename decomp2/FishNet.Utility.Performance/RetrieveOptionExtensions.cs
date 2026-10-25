namespace FishNet.Utility.Performance;

public static class RetrieveOptionExtensions
{
	public static bool FastContains(this ObjectPoolRetrieveOption whole, ObjectPoolRetrieveOption part)
	{
		return (whole & part) == part;
	}
}
