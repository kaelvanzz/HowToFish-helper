namespace FishNet.Object.Helping;

public static class CodegenHelper
{
	public static bool NetworkObject_Deinitializing(NetworkBehaviour nb)
	{
		if (nb == null)
		{
			return true;
		}
		return nb.IsDeinitializing;
	}

	public static bool IsServer(NetworkBehaviour nb)
	{
		if (nb == null)
		{
			return false;
		}
		return nb.IsServerStarted;
	}

	public static bool IsClient(NetworkBehaviour nb)
	{
		if (nb == null)
		{
			return false;
		}
		return nb.IsClientStarted;
	}
}
