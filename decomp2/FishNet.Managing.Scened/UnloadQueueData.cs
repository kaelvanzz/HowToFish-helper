using System;
using FishNet.Connection;

namespace FishNet.Managing.Scened;

public class UnloadQueueData
{
	[NonSerialized]
	public readonly SceneScopeType ScopeType;

	[NonSerialized]
	public NetworkConnection[] Connections;

	public SceneUnloadData SceneUnloadData;

	public string[] GlobalScenes = new string[0];

	[NonSerialized]
	public readonly bool AsServer;

	public UnloadQueueData()
	{
	}

	internal UnloadQueueData(SceneScopeType scopeType, NetworkConnection[] conns, SceneUnloadData sceneUnloadData, string[] globalScenes, bool asServer)
	{
		ScopeType = scopeType;
		Connections = conns;
		SceneUnloadData = sceneUnloadData;
		GlobalScenes = globalScenes;
		AsServer = asServer;
	}
}
