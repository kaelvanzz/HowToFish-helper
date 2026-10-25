using System;
using UnityEngine;

namespace HTFMod;

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
