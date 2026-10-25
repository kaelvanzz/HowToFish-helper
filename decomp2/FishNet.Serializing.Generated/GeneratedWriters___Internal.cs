using System;
using System.Runtime.InteropServices;
using FishNet.Component.Prediction;
using FishNet.Component.Transforming;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;

namespace FishNet.Serializing.Generated;

[StructLayout(LayoutKind.Auto, CharSet = CharSet.Auto)]
public static class GeneratedWriters___Internal
{
	[RuntimeInitializeOnLoadMethod]
	private static void InitializeOnce()
	{
		GenericWriter<byte>.SetWrite(InstancedExtension___WriteUInt8Unpacked);
		GenericWriter<sbyte>.SetWrite(InstancedExtension___WriteInt8Unpacked);
		GenericWriter<char>.SetWrite(InstancedExtension___WriteChar);
		GenericWriter<bool>.SetWrite(InstancedExtension___WriteBoolean);
		GenericWriter<ushort>.SetWrite(InstancedExtension___WriteUInt16);
		GenericWriter<short>.SetWrite(InstancedExtension___WriteInt16);
		GenericWriter<int>.SetWrite(InstancedExtension___WriteInt32);
		GenericWriter<uint>.SetWrite(InstancedExtension___WriteUInt32);
		GenericWriter<ulong>.SetWrite(InstancedExtension___WriteUInt64);
		GenericWriter<long>.SetWrite(InstancedExtension___WriteInt64);
		GenericWriter<float>.SetWrite(InstancedExtension___WriteSingle);
		GenericWriter<double>.SetWrite(InstancedExtension___WriteDouble);
		GenericWriter<decimal>.SetWrite(InstancedExtension___WriteDecimal);
		GenericWriter<string>.SetWrite(InstancedExtension___WriteString);
		GenericWriter<ArraySegment<byte>>.SetWrite(InstancedExtension___WriteArraySegmentAndSize);
		GenericWriter<Vector2>.SetWrite(InstancedExtension___WriteVector2);
		GenericWriter<Vector3>.SetWrite(InstancedExtension___WriteVector3);
		GenericWriter<Vector4>.SetWrite(InstancedExtension___WriteVector4);
		GenericWriter<Vector2Int>.SetWrite(InstancedExtension___WriteVector2Int);
		GenericWriter<Vector3Int>.SetWrite(InstancedExtension___WriteVector3Int);
		GenericWriter<Color>.SetWrite(InstancedExtension___WriteColor);
		GenericWriter<Color32>.SetWrite(InstancedExtension___WriteColor32);
		GenericWriter<Quaternion>.SetWrite(InstancedExtension___WriteQuaternion32);
		GenericWriter<Rect>.SetWrite(InstancedExtension___WriteRect);
		GenericWriter<Plane>.SetWrite(InstancedExtension___WritePlane);
		GenericWriter<Ray>.SetWrite(InstancedExtension___WriteRay);
		GenericWriter<Ray2D>.SetWrite(InstancedExtension___WriteRay2D);
		GenericWriter<Matrix4x4>.SetWrite(InstancedExtension___WriteMatrix4x4);
		GenericWriter<Guid>.SetWrite(InstancedExtension___WriteGuidAllocated);
		GenericWriter<GameObject>.SetWrite(InstancedExtension___WriteGameObject);
		GenericWriter<Transform>.SetWrite(InstancedExtension___WriteTransform);
		GenericWriter<NetworkObject>.SetWrite(InstancedExtension___WriteNetworkObject);
		GenericWriter<NetworkBehaviour>.SetWrite(InstancedExtension___WriteNetworkBehaviour);
		GenericWriter<DateTime>.SetWrite(InstancedExtension___WriteDateTime);
		GenericWriter<Channel>.SetWrite(InstancedExtension___WriteChannel);
		GenericWriter<LayerMask>.SetWrite(InstancedExtension___WriteLayerMask);
		GenericWriter<NetworkConnection>.SetWrite(InstancedExtension___WriteNetworkConnection);
		GenericWriter<TransformProperties>.SetWrite(InstancedExtension___WriteTransformProperties);
		GenericWriter<PredictionRigidbody.EntryData>.SetWrite(PredictionigidbodySerializers.WriteEntryData);
		GenericWriter<PredictionRigidbody>.SetWrite(PredictionigidbodySerializers.WritePredictionRigidbody);
		GenericWriter<PredictionRigidbody2D.EntryData>.SetWrite(PredictionRigidbody2DSerializers.WriteForceData);
		GenericWriter<PredictionRigidbody2D>.SetWrite(PredictionRigidbody2DSerializers.WritePredictionRigidbody2D);
		GenericWriter<RigidbodyState>.SetWrite(RigidbodyStateSerializers.WriteRigidbodyState);
		GenericWriter<Rigidbody2DState>.SetWrite(RigidbodyStateSerializers.WriteRigidbody2DState);
		GenericWriter<ConnectedClientsBroadcast>.SetWrite(ConnectedClientsBroadcastSerializers.WriteConnectedClientsBroadcast);
		GenericWriter<PreciseTick>.SetWrite(PreciseTickSerializer.WritePreciseTick);
		GenericWriter<ClientConnectionChangeBroadcast>.SetWrite(GWrite___FishNet_002EManaging_002EServer_002EClientConnectionChangeBroadcastFishNet_002ESerializing_002EGenerated);
		GenericWriter<EmptyStartScenesBroadcast>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002EEmptyStartScenesBroadcastFishNet_002ESerializing_002EGenerated);
		GenericWriter<LoadScenesBroadcast>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002ELoadScenesBroadcastFishNet_002ESerializing_002EGenerated);
		GenericWriter<LoadQueueData>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002ELoadQueueDataFishNet_002ESerializing_002EGenerated);
		GenericWriter<SceneLoadData>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002ESceneLoadDataFishNet_002ESerializing_002EGenerated);
		GenericWriter<PreferredScene>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002EPreferredSceneFishNet_002ESerializing_002EGenerated);
		GenericWriter<SceneLookupData>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002ESceneLookupDataFishNet_002ESerializing_002EGenerated);
		GenericWriter<SceneLookupData[]>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002ESceneLookupData_005B_005DFishNet_002ESerializing_002EGenerated);
		GenericWriter<NetworkObject[]>.SetWrite(GWrite___FishNet_002EObject_002ENetworkObject_005B_005DFishNet_002ESerializing_002EGenerated);
		GenericWriter<ReplaceOption>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002EReplaceOptionFishNet_002ESerializing_002EGenerated);
		GenericWriter<LoadParams>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002ELoadParamsFishNet_002ESerializing_002EGenerated);
		GenericWriter<byte[]>.SetWrite(GWrite___System_002EByte_005B_005DFishNet_002ESerializing_002EGenerated);
		GenericWriter<LoadOptions>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002ELoadOptionsFishNet_002ESerializing_002EGenerated);
		GenericWriter<string[]>.SetWrite(GWrite___System_002EString_005B_005DFishNet_002ESerializing_002EGenerated);
		GenericWriter<UnloadScenesBroadcast>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002EUnloadScenesBroadcastFishNet_002ESerializing_002EGenerated);
		GenericWriter<UnloadQueueData>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002EUnloadQueueDataFishNet_002ESerializing_002EGenerated);
		GenericWriter<SceneUnloadData>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002ESceneUnloadDataFishNet_002ESerializing_002EGenerated);
		GenericWriter<UnloadParams>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002EUnloadParamsFishNet_002ESerializing_002EGenerated);
		GenericWriter<UnloadOptions>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002EUnloadOptionsFishNet_002ESerializing_002EGenerated);
		GenericWriter<UnloadOptions.ServerUnloadMode>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002EUnloadOptions_002FServerUnloadModeFishNet_002ESerializing_002EGenerated);
		GenericWriter<ClientScenesLoadedBroadcast>.SetWrite(GWrite___FishNet_002EManaging_002EScened_002EClientScenesLoadedBroadcastFishNet_002ESerializing_002EGenerated);
		GenericWriter<SynchronizedProperty>.SetWrite(GWrite___FishNet_002EComponent_002ETransforming_002ESynchronizedPropertyFishNet_002ESerializing_002EGenerated);
	}

	public static void InstancedExtension___WriteUInt8Unpacked(this Writer writer, byte value1)
	{
		writer.WriteUInt8Unpacked(value1);
	}

	public static void InstancedExtension___WriteInt8Unpacked(this Writer writer, sbyte value1)
	{
		writer.WriteInt8Unpacked(value1);
	}

	public static void InstancedExtension___WriteChar(this Writer writer, char value1)
	{
		writer.WriteChar(value1);
	}

	public static void InstancedExtension___WriteBoolean(this Writer writer, bool value1)
	{
		writer.WriteBoolean(value1);
	}

	public static void InstancedExtension___WriteUInt16(this Writer writer, ushort value1)
	{
		writer.WriteUInt16(value1);
	}

	public static void InstancedExtension___WriteInt16(this Writer writer, short value1)
	{
		writer.WriteInt16(value1);
	}

	public static void InstancedExtension___WriteInt32(this Writer writer, int value1)
	{
		writer.WriteInt32(value1);
	}

	public static void InstancedExtension___WriteUInt32(this Writer writer, uint value1)
	{
		writer.WriteUInt32(value1);
	}

	public static void InstancedExtension___WriteUInt64(this Writer writer, ulong value1)
	{
		writer.WriteUInt64(value1);
	}

	public static void InstancedExtension___WriteInt64(this Writer writer, long value1)
	{
		writer.WriteInt64(value1);
	}

	public static void InstancedExtension___WriteSingle(this Writer writer, float value1)
	{
		writer.WriteSingle(value1);
	}

	public static void InstancedExtension___WriteDouble(this Writer writer, double value1)
	{
		writer.WriteDouble(value1);
	}

	public static void InstancedExtension___WriteDecimal(this Writer writer, decimal value1)
	{
		writer.WriteDecimal(value1);
	}

	public static void InstancedExtension___WriteString(this Writer writer, string value1)
	{
		writer.WriteString(value1);
	}

	public static void InstancedExtension___WriteArraySegmentAndSize(this Writer writer, ArraySegment<byte> value1)
	{
		writer.WriteArraySegmentAndSize(value1);
	}

	public static void InstancedExtension___WriteVector2(this Writer writer, Vector2 value1)
	{
		writer.WriteVector2(value1);
	}

	public static void InstancedExtension___WriteVector3(this Writer writer, Vector3 value1)
	{
		writer.WriteVector3(value1);
	}

	public static void InstancedExtension___WriteVector4(this Writer writer, Vector4 value1)
	{
		writer.WriteVector4(value1);
	}

	public static void InstancedExtension___WriteVector2Int(this Writer writer, Vector2Int value1)
	{
		writer.WriteVector2Int(value1);
	}

	public static void InstancedExtension___WriteVector3Int(this Writer writer, Vector3Int value1)
	{
		writer.WriteVector3Int(value1);
	}

	public static void InstancedExtension___WriteColor(this Writer writer, Color value1)
	{
		writer.WriteColor(value1);
	}

	public static void InstancedExtension___WriteColor32(this Writer writer, Color32 value1)
	{
		writer.WriteColor32(value1);
	}

	public static void InstancedExtension___WriteQuaternion32(this Writer writer, Quaternion value1)
	{
		writer.WriteQuaternion32(value1);
	}

	public static void InstancedExtension___WriteRect(this Writer writer, Rect value1)
	{
		writer.WriteRect(value1);
	}

	public static void InstancedExtension___WritePlane(this Writer writer, Plane value1)
	{
		writer.WritePlane(value1);
	}

	public static void InstancedExtension___WriteRay(this Writer writer, Ray value1)
	{
		writer.WriteRay(value1);
	}

	public static void InstancedExtension___WriteRay2D(this Writer writer, Ray2D value1)
	{
		writer.WriteRay2D(value1);
	}

	public static void InstancedExtension___WriteMatrix4x4(this Writer writer, Matrix4x4 value1)
	{
		writer.WriteMatrix4x4(value1);
	}

	public static void InstancedExtension___WriteGuidAllocated(this Writer writer, Guid value1)
	{
		writer.WriteGuidAllocated(value1);
	}

	public static void InstancedExtension___WriteGameObject(this Writer writer, GameObject go1)
	{
		writer.WriteGameObject(go1);
	}

	public static void InstancedExtension___WriteTransform(this Writer writer, Transform t1)
	{
		writer.WriteTransform(t1);
	}

	public static void InstancedExtension___WriteNetworkObject(this Writer writer, NetworkObject nob1)
	{
		writer.WriteNetworkObject(nob1);
	}

	public static void InstancedExtension___WriteNetworkBehaviour(this Writer writer, NetworkBehaviour nb1)
	{
		writer.WriteNetworkBehaviour(nb1);
	}

	public static void InstancedExtension___WriteDateTime(this Writer writer, DateTime dt1)
	{
		writer.WriteDateTime(dt1);
	}

	public static void InstancedExtension___WriteChannel(this Writer writer, Channel channel1)
	{
		writer.WriteChannel(channel1);
	}

	public static void InstancedExtension___WriteLayerMask(this Writer writer, LayerMask value1)
	{
		writer.WriteLayerMask(value1);
	}

	public static void InstancedExtension___WriteNetworkConnection(this Writer writer, NetworkConnection connection1)
	{
		writer.WriteNetworkConnection(connection1);
	}

	public static void InstancedExtension___WriteTransformProperties(this Writer writer, TransformProperties value1)
	{
		writer.WriteTransformProperties(value1);
	}

	public static void GWrite___FishNet_002EManaging_002EServer_002EClientConnectionChangeBroadcastFishNet_002ESerializing_002EGenerated(this Writer writer, ClientConnectionChangeBroadcast value)
	{
		InstancedExtension___WriteBoolean(writer, value.Connected);
		InstancedExtension___WriteInt32(writer, value.Id);
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002EEmptyStartScenesBroadcastFishNet_002ESerializing_002EGenerated(this Writer writer, EmptyStartScenesBroadcast value)
	{
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002ELoadScenesBroadcastFishNet_002ESerializing_002EGenerated(this Writer writer, LoadScenesBroadcast value)
	{
		GWrite___FishNet_002EManaging_002EScened_002ELoadQueueDataFishNet_002ESerializing_002EGenerated(writer, value.QueueData);
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002ELoadQueueDataFishNet_002ESerializing_002EGenerated(this Writer writer, LoadQueueData value)
	{
		if (value == null)
		{
			InstancedExtension___WriteBoolean(writer, value1: true);
			return;
		}
		InstancedExtension___WriteBoolean(writer, value1: false);
		GWrite___FishNet_002EManaging_002EScened_002ESceneLoadDataFishNet_002ESerializing_002EGenerated(writer, value.SceneLoadData);
		GWrite___System_002EString_005B_005DFishNet_002ESerializing_002EGenerated(writer, value.GlobalScenes);
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002ESceneLoadDataFishNet_002ESerializing_002EGenerated(this Writer writer, SceneLoadData value)
	{
		if (value == null)
		{
			InstancedExtension___WriteBoolean(writer, value1: true);
			return;
		}
		InstancedExtension___WriteBoolean(writer, value1: false);
		GWrite___FishNet_002EManaging_002EScened_002EPreferredSceneFishNet_002ESerializing_002EGenerated(writer, value.PreferredActiveScene);
		GWrite___FishNet_002EManaging_002EScened_002ESceneLookupData_005B_005DFishNet_002ESerializing_002EGenerated(writer, value.SceneLookupDatas);
		GWrite___FishNet_002EObject_002ENetworkObject_005B_005DFishNet_002ESerializing_002EGenerated(writer, value.MovedNetworkObjects);
		GWrite___FishNet_002EManaging_002EScened_002EReplaceOptionFishNet_002ESerializing_002EGenerated(writer, value.ReplaceScenes);
		GWrite___FishNet_002EManaging_002EScened_002ELoadParamsFishNet_002ESerializing_002EGenerated(writer, value.Params);
		GWrite___FishNet_002EManaging_002EScened_002ELoadOptionsFishNet_002ESerializing_002EGenerated(writer, value.Options);
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002EPreferredSceneFishNet_002ESerializing_002EGenerated(this Writer writer, PreferredScene value)
	{
		GWrite___FishNet_002EManaging_002EScened_002ESceneLookupDataFishNet_002ESerializing_002EGenerated(writer, value.Client);
		GWrite___FishNet_002EManaging_002EScened_002ESceneLookupDataFishNet_002ESerializing_002EGenerated(writer, value.Server);
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002ESceneLookupDataFishNet_002ESerializing_002EGenerated(this Writer writer, SceneLookupData value)
	{
		if ((object)value == null)
		{
			InstancedExtension___WriteBoolean(writer, value1: true);
			return;
		}
		InstancedExtension___WriteBoolean(writer, value1: false);
		InstancedExtension___WriteInt32(writer, value.Handle);
		InstancedExtension___WriteString(writer, value.Name);
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002ESceneLookupData_005B_005DFishNet_002ESerializing_002EGenerated(this Writer writer, SceneLookupData[] value)
	{
		writer.WriteArray(value);
	}

	public static void GWrite___FishNet_002EObject_002ENetworkObject_005B_005DFishNet_002ESerializing_002EGenerated(this Writer writer, NetworkObject[] value)
	{
		writer.WriteArray(value);
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002EReplaceOptionFishNet_002ESerializing_002EGenerated(this Writer writer, ReplaceOption value)
	{
		InstancedExtension___WriteUInt8Unpacked(writer, (byte)value);
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002ELoadParamsFishNet_002ESerializing_002EGenerated(this Writer writer, LoadParams value)
	{
		if (value == null)
		{
			InstancedExtension___WriteBoolean(writer, value1: true);
			return;
		}
		InstancedExtension___WriteBoolean(writer, value1: false);
		GWrite___System_002EByte_005B_005DFishNet_002ESerializing_002EGenerated(writer, value.ClientParams);
	}

	public static void GWrite___System_002EByte_005B_005DFishNet_002ESerializing_002EGenerated(this Writer writer, byte[] value)
	{
		writer.WriteArray(value);
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002ELoadOptionsFishNet_002ESerializing_002EGenerated(this Writer writer, LoadOptions value)
	{
		if (value == null)
		{
			InstancedExtension___WriteBoolean(writer, value1: true);
			return;
		}
		InstancedExtension___WriteBoolean(writer, value1: false);
		InstancedExtension___WriteBoolean(writer, value.ReloadScenes);
		InstancedExtension___WriteBoolean(writer, value.Addressables);
	}

	public static void GWrite___System_002EString_005B_005DFishNet_002ESerializing_002EGenerated(this Writer writer, string[] value)
	{
		writer.WriteArray(value);
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002EUnloadScenesBroadcastFishNet_002ESerializing_002EGenerated(this Writer writer, UnloadScenesBroadcast value)
	{
		GWrite___FishNet_002EManaging_002EScened_002EUnloadQueueDataFishNet_002ESerializing_002EGenerated(writer, value.QueueData);
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002EUnloadQueueDataFishNet_002ESerializing_002EGenerated(this Writer writer, UnloadQueueData value)
	{
		if (value == null)
		{
			InstancedExtension___WriteBoolean(writer, value1: true);
			return;
		}
		InstancedExtension___WriteBoolean(writer, value1: false);
		GWrite___FishNet_002EManaging_002EScened_002ESceneUnloadDataFishNet_002ESerializing_002EGenerated(writer, value.SceneUnloadData);
		GWrite___System_002EString_005B_005DFishNet_002ESerializing_002EGenerated(writer, value.GlobalScenes);
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002ESceneUnloadDataFishNet_002ESerializing_002EGenerated(this Writer writer, SceneUnloadData value)
	{
		if (value == null)
		{
			InstancedExtension___WriteBoolean(writer, value1: true);
			return;
		}
		InstancedExtension___WriteBoolean(writer, value1: false);
		GWrite___FishNet_002EManaging_002EScened_002EPreferredSceneFishNet_002ESerializing_002EGenerated(writer, value.PreferredActiveScene);
		GWrite___FishNet_002EManaging_002EScened_002ESceneLookupData_005B_005DFishNet_002ESerializing_002EGenerated(writer, value.SceneLookupDatas);
		GWrite___FishNet_002EManaging_002EScened_002EUnloadParamsFishNet_002ESerializing_002EGenerated(writer, value.Params);
		GWrite___FishNet_002EManaging_002EScened_002EUnloadOptionsFishNet_002ESerializing_002EGenerated(writer, value.Options);
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002EUnloadParamsFishNet_002ESerializing_002EGenerated(this Writer writer, UnloadParams value)
	{
		if (value == null)
		{
			InstancedExtension___WriteBoolean(writer, value1: true);
			return;
		}
		InstancedExtension___WriteBoolean(writer, value1: false);
		GWrite___System_002EByte_005B_005DFishNet_002ESerializing_002EGenerated(writer, value.ClientParams);
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002EUnloadOptionsFishNet_002ESerializing_002EGenerated(this Writer writer, UnloadOptions value)
	{
		if (value == null)
		{
			InstancedExtension___WriteBoolean(writer, value1: true);
			return;
		}
		InstancedExtension___WriteBoolean(writer, value1: false);
		GWrite___FishNet_002EManaging_002EScened_002EUnloadOptions_002FServerUnloadModeFishNet_002ESerializing_002EGenerated(writer, value.Mode);
		InstancedExtension___WriteBoolean(writer, value.Addressables);
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002EUnloadOptions_002FServerUnloadModeFishNet_002ESerializing_002EGenerated(this Writer writer, UnloadOptions.ServerUnloadMode value)
	{
		InstancedExtension___WriteInt32(writer, (int)value);
	}

	public static void GWrite___FishNet_002EManaging_002EScened_002EClientScenesLoadedBroadcastFishNet_002ESerializing_002EGenerated(this Writer writer, ClientScenesLoadedBroadcast value)
	{
		GWrite___FishNet_002EManaging_002EScened_002ESceneLookupData_005B_005DFishNet_002ESerializing_002EGenerated(writer, value.SceneLookupDatas);
	}

	public static void GWrite___FishNet_002EComponent_002ETransforming_002ESynchronizedPropertyFishNet_002ESerializing_002EGenerated(this Writer writer, SynchronizedProperty value)
	{
		InstancedExtension___WriteUInt8Unpacked(writer, (byte)value);
	}
}
