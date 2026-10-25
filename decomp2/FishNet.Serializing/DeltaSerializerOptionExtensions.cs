namespace FishNet.Serializing;

public static class DeltaSerializerOptionExtensions
{
	public static bool FastContains(this DeltaSerializerOption whole, DeltaSerializerOption part)
	{
		return (whole & part) == part;
	}
}
