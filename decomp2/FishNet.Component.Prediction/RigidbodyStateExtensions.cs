using UnityEngine;
using UnityEngine.Scripting;

namespace FishNet.Component.Prediction;

[Preserve]
public static class RigidbodyStateExtensions
{
	public static RigidbodyState GetState(this Rigidbody rb)
	{
		return new RigidbodyState(rb);
	}

	public static void SetState(this Rigidbody rb, RigidbodyState state)
	{
		Transform transform = rb.transform;
		transform.position = state.Position;
		transform.rotation = state.Rotation;
		rb.linearVelocity = state.Velocity;
		rb.angularVelocity = state.AngularVelocity;
	}

	public static Rigidbody2DState GetState(this Rigidbody2D rb)
	{
		return new Rigidbody2DState(rb);
	}

	public static void SetState(this Rigidbody2D rb, Rigidbody2DState state)
	{
		Transform transform = rb.transform;
		transform.position = state.Position;
		transform.rotation = state.Rotation;
		rb.linearVelocity = state.Velocity;
		rb.angularVelocity = state.AngularVelocity;
	}
}
