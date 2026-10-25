using FishNet.Connection;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened;

public struct ClientPresenceChangeEventArgs
{
	public Scene Scene;

	public NetworkConnection Connection;

	public bool Added;

	internal ClientPresenceChangeEventArgs(Scene scene, NetworkConnection conn, bool added)
	{
		Scene = scene;
		Connection = conn;
		Added = added;
	}
}
