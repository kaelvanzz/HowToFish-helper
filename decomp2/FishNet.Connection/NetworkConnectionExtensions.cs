namespace FishNet.Connection;

public static class NetworkConnectionExtensions
{
	public static bool IsValid(this NetworkConnection c)
	{
		if (c == null)
		{
			return false;
		}
		return c.IsValid;
	}
}
