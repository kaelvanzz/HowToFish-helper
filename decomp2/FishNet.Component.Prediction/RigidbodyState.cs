using FishNet.CodeGenerating;
using UnityEngine;
using UnityEngine.Scripting;

namespace FishNet.Component.Prediction;

[UseGlobalCustomSerializer]
[Preserve]
public struct RigidbodyState
{
	public Vector3 Position;

	public Quaternion Rotation;

	public bool IsKinematic;

	public Vector3 Velocity;

	public Vector3 AngularVelocity;

	public RigidbodyState(Rigidbody rb, bool isKinematic)
		: this(rb)
	{
		Position = rb.transform.position;
		Rotation = rb.transform.rotation;
		IsKinematic = isKinematic;
		Velocity = rb.linearVelocity;
		AngularVelocity = rb.angularVelocity;
	}

	public RigidbodyState(Rigidbody rb)
	{
		Position = rb.transform.position;
		Rotation = rb.transform.rotation;
		IsKinematic = rb.isKinematic;
		Velocity = rb.linearVelocity;
		AngularVelocity = rb.angularVelocity;
	}
}
