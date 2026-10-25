using UnityEngine;

namespace HTFMod;

/// <summary>
/// Creates the persistent cheat-controller object on the Unity main thread.
/// Both entry points (BepInEx plugin Awake and the legacy CLI-injector Loader) call this.
/// </summary>
public static class ModCore
{
    public static CheatController Controller { get; private set; }

    public static void Initialize()
    {
        if (Controller != null)
            return;

        var go = new GameObject("HTF_ModRoot");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        Controller = go.AddComponent<CheatController>();
    }
}
