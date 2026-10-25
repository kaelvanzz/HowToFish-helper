using System;
using FishNet.Connection;

namespace FishNet.Managing.Scened;

public class LoadQueueData
{
	[NonSerialized]
	public SceneScopeType ScopeType;

	[NonSerialized]
	public NetworkConnection[] Connections = new NetworkConnection[0];

	public SceneLoadData SceneLoadData;

	public string[] GlobalScenes = new string[0];

	[NonSerialized]
	public readonly bool AsServer;

	public LoadQueueData()
	{
	}

	internal LoadQueueData(SceneScopeType scopeType, NetworkConnection[] conns, SceneLoadData sceneLoadData, string[] globalScenes, bool asServer)
	{
		ScopeType = scopeType;
		Connections = conns;
		SceneLoadData = sceneLoadData;
		GlobalScenes = globalScenes;
		AsServer = asServer;
	}
}
