using System;
using UnityEngine;

namespace FishNet.Managing.Transporting;

public abstract class IntermediateLayer : MonoBehaviour
{
	public TransportManager TransportManager { get; private set; }

	public abstract ArraySegment<byte> HandleIncoming(ArraySegment<byte> src, bool fromServer);

	public abstract ArraySegment<byte> HandleOutgoing(ArraySegment<byte> src, bool toServer);

	public virtual void InitializeOnce(TransportManager manager)
	{
		TransportManager = manager;
	}
}
