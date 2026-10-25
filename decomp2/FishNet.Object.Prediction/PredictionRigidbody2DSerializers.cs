using System.Collections.Generic;
using FishNet.Component.Prediction;
using FishNet.Managing;
using FishNet.Serializing;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Object.Prediction;

public static class PredictionRigidbody2DSerializers
{
	public static void WriteForceData(this Writer w, PredictionRigidbody2D.EntryData value)
	{
		PredictionRigidbody2D.ForceApplicationType type = value.Type;
		w.WriteUInt8Unpacked((byte)type);
		PredictionRigidbody2D.AllForceData data = value.Data;
		switch (type)
		{
		case PredictionRigidbody2D.ForceApplicationType.AddForce:
		case PredictionRigidbody2D.ForceApplicationType.AddRelativeForce:
			w.WriteVector3(data.Vector3Force);
			w.WriteInt32((byte)data.Mode);
			break;
		case PredictionRigidbody2D.ForceApplicationType.AddTorque:
			w.WriteSingle(data.FloatForce);
			w.WriteInt32((byte)data.Mode);
			break;
		case PredictionRigidbody2D.ForceApplicationType.AddForceAtPosition:
			w.WriteVector3(data.Vector3Force);
			w.WriteVector3(data.Position);
			w.WriteInt32((byte)data.Mode);
			break;
		default:
			NetworkManagerExtensions.LogError($"ForceApplicationType of {type} is not supported.");
			break;
		}
	}

	public static PredictionRigidbody2D.EntryData ReadForceData(this Reader r)
	{
		PredictionRigidbody2D.EntryData result = default(PredictionRigidbody2D.EntryData);
		PredictionRigidbody2D.ForceApplicationType forceApplicationType = (result.Type = (PredictionRigidbody2D.ForceApplicationType)r.ReadUInt8Unpacked());
		PredictionRigidbody2D.AllForceData allForceData = default(PredictionRigidbody2D.AllForceData);
		switch (forceApplicationType)
		{
		case PredictionRigidbody2D.ForceApplicationType.AddForce:
		case PredictionRigidbody2D.ForceApplicationType.AddRelativeForce:
			allForceData.Vector3Force = r.ReadVector3();
			allForceData.Mode = (ForceMode2D)r.ReadUInt8Unpacked();
			return result;
		case PredictionRigidbody2D.ForceApplicationType.AddTorque:
			allForceData.FloatForce = r.ReadSingle();
			allForceData.Mode = (ForceMode2D)r.ReadUInt8Unpacked();
			return result;
		case PredictionRigidbody2D.ForceApplicationType.AddForceAtPosition:
			allForceData.Vector3Force = r.ReadVector3();
			allForceData.Position = r.ReadVector3();
			allForceData.Mode = (ForceMode2D)r.ReadUInt8Unpacked();
			return result;
		default:
			NetworkManagerExtensions.LogError($"ForceApplicationType of {forceApplicationType} is not supported.");
			return result;
		}
	}

	public static void WritePredictionRigidbody2D(this Writer w, PredictionRigidbody2D pr)
	{
		w.WriteRigidbody2DState(pr.Rigidbody2D.GetState());
		w.WriteList(pr.GetPendingForces());
	}

	public static PredictionRigidbody2D ReadPredictionRigidbody2D(this Reader r)
	{
		List<PredictionRigidbody2D.EntryData> collection = CollectionCaches<PredictionRigidbody2D.EntryData>.RetrieveList();
		Rigidbody2DState rs = r.ReadRigidbody2DState();
		r.ReadList(ref collection);
		PredictionRigidbody2D predictionRigidbody2D = ResettableObjectCaches<PredictionRigidbody2D>.Retrieve();
		predictionRigidbody2D.SetReconcileData(rs, collection);
		predictionRigidbody2D.SetPendingForces(collection);
		return predictionRigidbody2D;
	}
}
