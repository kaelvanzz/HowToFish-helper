using System.Collections.Generic;
using FishNet.CodeGenerating;
using FishNet.Component.Prediction;
using FishNet.Managing;
using FishNet.Serializing;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.Scripting;

namespace FishNet.Object.Prediction;

[Preserve]
[DefaultWriter]
public static class PredictionigidbodySerializers
{
	[DefaultWriter]
	public static void WriteEntryData(this Writer w, PredictionRigidbody.EntryData value)
	{
		PredictionRigidbody.ForceApplicationType type = value.Type;
		w.WriteUInt8Unpacked((byte)type);
		PredictionRigidbody.AllForceData data = value.Data;
		switch (type)
		{
		case PredictionRigidbody.ForceApplicationType.AddForce:
		case PredictionRigidbody.ForceApplicationType.AddRelativeForce:
		case PredictionRigidbody.ForceApplicationType.AddTorque:
		case PredictionRigidbody.ForceApplicationType.AddRelativeTorque:
			w.WriteVector3(data.Vector3Force);
			w.WriteInt32((byte)data.Mode);
			break;
		case PredictionRigidbody.ForceApplicationType.AddExplosiveForce:
			w.WriteSingle(data.FloatForce);
			w.WriteVector3(data.Position);
			w.WriteSingle(data.Radius);
			w.WriteSingle(data.UpwardsModifier);
			w.WriteInt32((byte)data.Mode);
			break;
		case PredictionRigidbody.ForceApplicationType.AddForceAtPosition:
			w.WriteVector3(data.Vector3Force);
			w.WriteVector3(data.Position);
			w.WriteInt32((byte)data.Mode);
			break;
		default:
			NetworkManagerExtensions.LogError($"ForceApplicationType of {type} is not supported.");
			break;
		}
	}

	[DefaultReader]
	public static PredictionRigidbody.EntryData ReadEntryData(this Reader r)
	{
		PredictionRigidbody.EntryData result = default(PredictionRigidbody.EntryData);
		PredictionRigidbody.ForceApplicationType forceApplicationType = (result.Type = (PredictionRigidbody.ForceApplicationType)r.ReadUInt8Unpacked());
		PredictionRigidbody.AllForceData data = default(PredictionRigidbody.AllForceData);
		switch (forceApplicationType)
		{
		case PredictionRigidbody.ForceApplicationType.AddForce:
		case PredictionRigidbody.ForceApplicationType.AddRelativeForce:
		case PredictionRigidbody.ForceApplicationType.AddTorque:
		case PredictionRigidbody.ForceApplicationType.AddRelativeTorque:
			data.Vector3Force = r.ReadVector3();
			data.Mode = (ForceMode)r.ReadInt32();
			break;
		case PredictionRigidbody.ForceApplicationType.AddExplosiveForce:
			data.FloatForce = r.ReadSingle();
			data.Position = r.ReadVector3();
			data.Radius = r.ReadSingle();
			data.UpwardsModifier = r.ReadSingle();
			data.Mode = (ForceMode)r.ReadInt32();
			break;
		case PredictionRigidbody.ForceApplicationType.AddForceAtPosition:
			data.Vector3Force = r.ReadVector3();
			data.Position = r.ReadVector3();
			data.Mode = (ForceMode)r.ReadInt32();
			break;
		default:
			NetworkManagerExtensions.LogError($"ForceApplicationType of {forceApplicationType} is not supported.");
			break;
		}
		result.Data = data;
		return result;
	}

	[DefaultWriter]
	public static void WritePredictionRigidbody(this Writer w, PredictionRigidbody pr)
	{
		w.WriteRigidbodyState(pr.Rigidbody.GetState());
		w.WriteList(pr.GetPendingForces());
	}

	[DefaultReader]
	public static PredictionRigidbody ReadPredictionRigidbody(this Reader r)
	{
		List<PredictionRigidbody.EntryData> collection = CollectionCaches<PredictionRigidbody.EntryData>.RetrieveList();
		RigidbodyState rs = r.ReadRigidbodyState();
		r.ReadList(ref collection);
		PredictionRigidbody predictionRigidbody = ResettableObjectCaches<PredictionRigidbody>.Retrieve();
		predictionRigidbody.SetReconcileData(rs, collection);
		return predictionRigidbody;
	}

	[DefaultDeltaWriter]
	public static bool WriteDeltaEntryData(this Writer w, PredictionRigidbody.EntryData value)
	{
		w.WriteEntryData(value);
		return true;
	}

	[DefaultDeltaReader]
	public static PredictionRigidbody.EntryData ReadDeltaEntryData(this Reader r)
	{
		return r.ReadEntryData();
	}

	[DefaultDeltaWriter]
	public static bool WriteDeltaPredictionRigidbody(this Writer w, PredictionRigidbody pr)
	{
		w.WritePredictionRigidbody(pr);
		return true;
	}

	[DefaultDeltaReader]
	public static PredictionRigidbody ReadDeltaPredictionRigidbody(this Reader r)
	{
		return r.ReadPredictionRigidbody();
	}
}
