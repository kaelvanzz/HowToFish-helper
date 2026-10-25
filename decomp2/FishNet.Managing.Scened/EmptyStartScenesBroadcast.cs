using System.Runtime.InteropServices;
using FishNet.Broadcast;

namespace FishNet.Managing.Scened;

[StructLayout(LayoutKind.Sequential, Size = 1)]
public struct EmptyStartScenesBroadcast : IBroadcast
{
}
