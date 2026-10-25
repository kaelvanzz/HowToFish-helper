using System;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened;

public class LoadOptions
{
	[NonSerialized]
	public bool AutomaticallyUnload = true;

	[NonSerialized]
	public bool AllowStacking;

	[NonSerialized]
	public LocalPhysicsMode LocalPhysics;

	[Obsolete("This feature is not functional yet but will be at a later release.")]
	public bool ReloadScenes;

	public bool Addressables;
}
