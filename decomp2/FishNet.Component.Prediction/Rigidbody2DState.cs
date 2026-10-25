using FishNet.CodeGenerating;
using UnityEngine;
using UnityEngine.Scripting;

namespace FishNet.Component.Prediction;

[UseGlobalCustomSerializer]
[Preserve]
public struct Rigidbody2DState
{
	public Vector3 Position;

	public Quaternion Rotation;

	public Vector2 Velocity;

	public float AngularVelocity;

	public bool Simulated;

	public bool IsKinematic;

	public Rigidbody2DState(Rigidbody2D rb, bool simulated)
	{
		Position = rb.transform.position;
		Rotation = rb.transform.rotation;
		Velocity = rb.linearVelocity;
		AngularVelocity = rb.angularVelocity;
		Simulated = simulated;
		IsKinematic = rb.isKinematic;
	}

	public Rigidbody2DState(Rigidbody2D rb)
	{
		Position = rb.transform.position;
		Rotation = rb.transform.rotation;
		Velocity = rb.linearVelocity;
		AngularVelocity = rb.angularVelocity;
		Simulated = rb.simulated;
		IsKinematic = rb.isKinematic;
	}
}
