namespace FishNet.Managing.Scened;

public class UnloadOptions
{
	public enum ServerUnloadMode
	{
		UnloadUnused,
		KeepUnused
	}

	public ServerUnloadMode Mode;

	public bool Addressables;
}
