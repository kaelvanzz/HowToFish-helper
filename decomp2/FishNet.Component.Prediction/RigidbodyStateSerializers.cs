using FishNet.Serializing;
using UnityEngine.Scripting;

namespace FishNet.Component.Prediction;

[Preserve]
public static class RigidbodyStateSerializers
{
	public static void WriteRigidbodyState(this Writer writer, RigidbodyState value)
	{
		writer.WriteVector3(value.Position);
		writer.WriteQuaternion32(value.Rotation);
		writer.WriteBoolean(value.IsKinematic);
		if (!value.IsKinematic)
		{
			writer.WriteVector3(value.Velocity);
			writer.WriteVector3(value.AngularVelocity);
		}
	}

	public static RigidbodyState ReadRigidbodyState(this Reader reader)
	{
		RigidbodyState rigidbodyState = default(RigidbodyState);
		rigidbodyState.Position = reader.ReadVector3();
		rigidbodyState.Rotation = reader.ReadQuaternion32();
		rigidbodyState.IsKinematic = reader.ReadBoolean();
		RigidbodyState result = rigidbodyState;
		if (!result.IsKinematic)
		{
			result.Velocity = reader.ReadVector3();
			result.AngularVelocity = reader.ReadVector3();
		}
		return result;
	}

	public static void WriteRigidbody2DState(this Writer writer, Rigidbody2DState value)
	{
		writer.WriteVector3(value.Position);
		writer.WriteQuaternion32(value.Rotation);
		writer.WriteBoolean(value.Simulated);
		writer.WriteBoolean(value.IsKinematic);
		if (value.Simulated)
		{
			writer.WriteVector2(value.Velocity);
			writer.WriteSingle(value.AngularVelocity);
		}
	}

	public static Rigidbody2DState ReadRigidbody2DState(this Reader reader)
	{
		Rigidbody2DState rigidbody2DState = default(Rigidbody2DState);
		rigidbody2DState.Position = reader.ReadVector3();
		rigidbody2DState.Rotation = reader.ReadQuaternion32();
		rigidbody2DState.Simulated = reader.ReadBoolean();
		rigidbody2DState.IsKinematic = reader.ReadBoolean();
		Rigidbody2DState result = rigidbody2DState;
		if (result.Simulated)
		{
			result.Velocity = reader.ReadVector2();
			result.AngularVelocity = reader.ReadSingle();
		}
		return result;
	}
}
