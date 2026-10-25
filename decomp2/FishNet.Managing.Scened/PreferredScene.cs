namespace FishNet.Managing.Scened;

public struct PreferredScene
{
	public SceneLookupData Client;

	public SceneLookupData Server;

	public PreferredScene(SceneLookupData client, SceneLookupData server)
	{
		Client = client;
		Server = server;
	}

	public PreferredScene(SceneLookupData sld)
	{
		Client = sld;
		Server = sld;
	}
}
