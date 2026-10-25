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
public static class GeneratedReaders___Internal
{
	[RuntimeInitializeOnLoadMethod]
	private static void InitializeOnce()
	{
		GenericReader<byte>.SetRead(InstancedExtension___ReadUInt8Unpacked);
		GenericReader<sbyte>.SetRead(InstancedExtension___ReadInt8Unpacked);
		GenericReader<char>.SetRead(InstancedExtension___ReadChar);
		GenericReader<bool>.SetRead(InstancedExtension___ReadBoolean);
		GenericReader<ushort>.SetRead(InstancedExtension___ReadUInt16);
		GenericReader<short>.SetRead(InstancedExtension___ReadInt16);
		GenericReader<uint>.SetRead(InstancedExtension___ReadUInt32);
		GenericReader<int>.SetRead(InstancedExtension___ReadInt32);
		GenericReader<long>.SetRead(InstancedExtension___ReadInt64);
		GenericReader<ulong>.SetRead(InstancedExtension___ReadUInt64);
		GenericReader<float>.SetRead(InstancedExtension___ReadSingle);
		GenericReader<double>.SetRead(InstancedExtension___ReadDouble);
		GenericReader<decimal>.SetRead(InstancedExtension___ReadDecimal);
		GenericReader<string>.SetRead(InstancedExtension___ReadStringAllocated);
		GenericReader<byte[]>.SetRead(InstancedExtension___ReadUInt8ArrayAndSizeAllocated);
		GenericReader<ArraySegment<byte>>.SetRead(InstancedExtension___ReadArraySegmentAndSize);
		GenericReader<Vector2>.SetRead(InstancedExtension___ReadVector2);
		GenericReader<Vector3>.SetRead(InstancedExtension___ReadVector3);
		GenericReader<Vector4>.SetRead(InstancedExtension___ReadVector4);
		GenericReader<Vector2Int>.SetRead(InstancedExtension___ReadVector2Int);
		GenericReader<Vector3Int>.SetRead(InstancedExtension___ReadVector3Int);
		GenericReader<Color>.SetRead(InstancedExtension___ReadColor);
		GenericReader<Color32>.SetRead(InstancedExtension___ReadColor32);
		GenericReader<Quaternion>.SetRead(InstancedExtension___ReadQuaternion32);
		GenericReader<Rect>.SetRead(InstancedExtension___ReadRect);
		GenericReader<Plane>.SetRead(InstancedExtension___ReadPlane);
		GenericReader<Ray>.SetRead(InstancedExtension___ReadRay);
		GenericReader<Ray2D>.SetRead(InstancedExtension___ReadRay2D);
		GenericReader<Matrix4x4>.SetRead(InstancedExtension___ReadMatrix4x4);
		GenericReader<Guid>.SetRead(InstancedExtension___ReadGuid);
		GenericReader<GameObject>.SetRead(InstancedExtension___ReadGameObject);
		GenericReader<Transform>.SetRead(InstancedExtension___ReadTransform);
		GenericReader<NetworkObject>.SetRead(InstancedExtension___ReadNetworkObject);
		GenericReader<NetworkBehaviour>.SetRead(InstancedExtension___ReadNetworkBehaviour);
		GenericReader<DateTime>.SetRead(InstancedExtension___ReadDateTime);
		GenericReader<Channel>.SetRead(InstancedExtension___ReadChannel);
		GenericReader<LayerMask>.SetRead(InstancedExtension___ReadLayerMask);
		GenericReader<NetworkConnection>.SetRead(InstancedExtension___ReadNetworkConnection);
		GenericReader<TransformProperties>.SetRead(InstancedExtension___ReadTransformProperties);
		GenericReader<PredictionRigidbody.EntryData>.SetRead(PredictionigidbodySerializers.ReadDeltaEntryData);
		GenericReader<PredictionRigidbody>.SetRead(PredictionigidbodySerializers.ReadDeltaPredictionRigidbody);
		GenericReader<PredictionRigidbody2D.EntryData>.SetRead(PredictionRigidbody2DSerializers.ReadForceData);
		GenericReader<PredictionRigidbody2D>.SetRead(PredictionRigidbody2DSerializers.ReadPredictionRigidbody2D);
		GenericReader<RigidbodyState>.SetRead(RigidbodyStateSerializers.ReadRigidbodyState);
		GenericReader<Rigidbody2DState>.SetRead(RigidbodyStateSerializers.ReadRigidbody2DState);
		GenericReader<ConnectedClientsBroadcast>.SetRead(ConnectedClientsBroadcastSerializers.ReadConnectedClientsBroadcast);
		GenericReader<PreciseTick>.SetRead(PreciseTickSerializer.ReadPreciseTick);
		GenericReader<ClientConnectionChangeBroadcast>.SetRead(GRead___FishNet_002EManaging_002EServer_002EClientConnectionChangeBroadcastFishNet_002ESerializing_002EGenerateds);
		GenericReader<EmptyStartScenesBroadcast>.SetRead(GRead___FishNet_002EManaging_002EScened_002EEmptyStartScenesBroadcastFishNet_002ESerializing_002EGenerateds);
		GenericReader<LoadScenesBroadcast>.SetRead(GRead___FishNet_002EManaging_002EScened_002ELoadScenesBroadcastFishNet_002ESerializing_002EGenerateds);
		GenericReader<LoadQueueData>.SetRead(GRead___FishNet_002EManaging_002EScened_002ELoadQueueDataFishNet_002ESerializing_002EGenerateds);
		GenericReader<SceneLoadData>.SetRead(GRead___FishNet_002EManaging_002EScened_002ESceneLoadDataFishNet_002ESerializing_002EGenerateds);
		GenericReader<PreferredScene>.SetRead(GRead___FishNet_002EManaging_002EScened_002EPreferredSceneFishNet_002ESerializing_002EGenerateds);
		GenericReader<SceneLookupData>.SetRead(GRead___FishNet_002EManaging_002EScened_002ESceneLookupDataFishNet_002ESerializing_002EGenerateds);
		GenericReader<SceneLookupData[]>.SetRead(GRead___FishNet_002EManaging_002EScened_002ESceneLookupData_005B_005DFishNet_002ESerializing_002EGenerateds);
		GenericReader<NetworkObject[]>.SetRead(GRead___FishNet_002EObject_002ENetworkObject_005B_005DFishNet_002ESerializing_002EGenerateds);
		GenericReader<ReplaceOption>.SetRead(GRead___FishNet_002EManaging_002EScened_002EReplaceOptionFishNet_002ESerializing_002EGenerateds);
		GenericReader<LoadParams>.SetRead(GRead___FishNet_002EManaging_002EScened_002ELoadParamsFishNet_002ESerializing_002EGenerateds);
		GenericReader<LoadOptions>.SetRead(GRead___FishNet_002EManaging_002EScened_002ELoadOptionsFishNet_002ESerializing_002EGenerateds);
		GenericReader<string[]>.SetRead(GRead___System_002EString_005B_005DFishNet_002ESerializing_002EGenerateds);
		GenericReader<UnloadScenesBroadcast>.SetRead(GRead___FishNet_002EManaging_002EScened_002EUnloadScenesBroadcastFishNet_002ESerializing_002EGenerateds);
		GenericReader<UnloadQueueData>.SetRead(GRead___FishNet_002EManaging_002EScened_002EUnloadQueueDataFishNet_002ESerializing_002EGenerateds);
		GenericReader<SceneUnloadData>.SetRead(GRead___FishNet_002EManaging_002EScened_002ESceneUnloadDataFishNet_002ESerializing_002EGenerateds);
		GenericReader<UnloadParams>.SetRead(GRead___FishNet_002EManaging_002EScened_002EUnloadParamsFishNet_002ESerializing_002EGenerateds);
		GenericReader<UnloadOptions>.SetRead(GRead___FishNet_002EManaging_002EScened_002EUnloadOptionsFishNet_002ESerializing_002EGenerateds);
		GenericReader<UnloadOptions.ServerUnloadMode>.SetRead(GRead___FishNet_002EManaging_002EScened_002EUnloadOptions_002FServerUnloadModeFishNet_002ESerializing_002EGenerateds);
		GenericReader<ClientScenesLoadedBroadcast>.SetRead(GRead___FishNet_002EManaging_002EScened_002EClientScenesLoadedBroadcastFishNet_002ESerializing_002EGenerateds);
		GenericReader<SynchronizedProperty>.SetRead(GRead___FishNet_002EComponent_002ETransforming_002ESynchronizedPropertyFishNet_002ESerializing_002EGenerateds);
	}

	public static byte InstancedExtension___ReadUInt8Unpacked(this Reader reader)
	{
		return reader.ReadUInt8Unpacked();
	}

	public static sbyte InstancedExtension___ReadInt8Unpacked(this Reader reader)
	{
		return reader.ReadInt8Unpacked();
	}

	public static char InstancedExtension___ReadChar(this Reader reader)
	{
		return reader.ReadChar();
	}

	public static bool InstancedExtension___ReadBoolean(this Reader reader)
	{
		return reader.ReadBoolean();
	}

	public static ushort InstancedExtension___ReadUInt16(this Reader reader)
	{
		return reader.ReadUInt16();
	}

	public static short InstancedExtension___ReadInt16(this Reader reader)
	{
		return reader.ReadInt16();
	}

	public static uint InstancedExtension___ReadUInt32(this Reader reader)
	{
		return reader.ReadUInt32();
	}

	public static int InstancedExtension___ReadInt32(this Reader reader)
	{
		return reader.ReadInt32();
	}

	public static long InstancedExtension___ReadInt64(this Reader reader)
	{
		return reader.ReadInt64();
	}

	public static ulong InstancedExtension___ReadUInt64(this Reader reader)
	{
		return reader.ReadUInt64();
	}

	public static float InstancedExtension___ReadSingle(this Reader reader)
	{
		return reader.ReadSingle();
	}

	public static double InstancedExtension___ReadDouble(this Reader reader)
	{
		return reader.ReadDouble();
	}

	public static decimal InstancedExtension___ReadDecimal(this Reader reader)
	{
		return reader.ReadDecimal();
	}

	public static string InstancedExtension___ReadStringAllocated(this Reader reader)
	{
		return reader.ReadStringAllocated();
	}

	public static byte[] InstancedExtension___ReadUInt8ArrayAndSizeAllocated(this Reader reader)
	{
		return reader.ReadUInt8ArrayAndSizeAllocated();
	}

	public static ArraySegment<byte> InstancedExtension___ReadArraySegmentAndSize(this Reader reader)
	{
		return reader.ReadArraySegmentAndSize();
	}

	public static Vector2 InstancedExtension___ReadVector2(this Reader reader)
	{
		return reader.ReadVector2();
	}

	public static Vector3 InstancedExtension___ReadVector3(this Reader reader)
	{
		return reader.ReadVector3();
	}

	public static Vector4 InstancedExtension___ReadVector4(this Reader reader)
	{
		return reader.ReadVector4();
	}

	public static Vector2Int InstancedExtension___ReadVector2Int(this Reader reader)
	{
		return reader.ReadVector2Int();
	}

	public static Vector3Int InstancedExtension___ReadVector3Int(this Reader reader)
	{
		return reader.ReadVector3Int();
	}

	public static Color InstancedExtension___ReadColor(this Reader reader)
	{
		return reader.ReadColor();
	}

	public static Color32 InstancedExtension___ReadColor32(this Reader reader)
	{
		return reader.ReadColor32();
	}

	public static Quaternion InstancedExtension___ReadQuaternion32(this Reader reader)
	{
		return reader.ReadQuaternion32();
	}

	public static Rect InstancedExtension___ReadRect(this Reader reader)
	{
		return reader.ReadRect();
	}

	public static Plane InstancedExtension___ReadPlane(this Reader reader)
	{
		return reader.ReadPlane();
	}

	public static Ray InstancedExtension___ReadRay(this Reader reader)
	{
		return reader.ReadRay();
	}

	public static Ray2D InstancedExtension___ReadRay2D(this Reader reader)
	{
		return reader.ReadRay2D();
	}

	public static Matrix4x4 InstancedExtension___ReadMatrix4x4(this Reader reader)
	{
		return reader.ReadMatrix4x4();
	}

	public static Guid InstancedExtension___ReadGuid(this Reader reader)
	{
		return reader.ReadGuid();
	}

	public static GameObject InstancedExtension___ReadGameObject(this Reader reader)
	{
		return reader.ReadGameObject();
	}

	public static Transform InstancedExtension___ReadTransform(this Reader reader)
	{
		return reader.ReadTransform();
	}

	public static NetworkObject InstancedExtension___ReadNetworkObject(this Reader reader)
	{
		return reader.ReadNetworkObject();
	}

	public static NetworkBehaviour InstancedExtension___ReadNetworkBehaviour(this Reader reader)
	{
		return reader.ReadNetworkBehaviour();
	}

	public static DateTime InstancedExtension___ReadDateTime(this Reader reader)
	{
		return reader.ReadDateTime();
	}

	public static Channel InstancedExtension___ReadChannel(this Reader reader)
	{
		return reader.ReadChannel();
	}

	public static LayerMask InstancedExtension___ReadLayerMask(this Reader reader)
	{
		return reader.ReadLayerMask();
	}

	public static NetworkConnection InstancedExtension___ReadNetworkConnection(this Reader reader)
	{
		return reader.ReadNetworkConnection();
	}

	public static TransformProperties InstancedExtension___ReadTransformProperties(this Reader reader)
	{
		return reader.ReadTransformProperties();
	}

	public static ClientConnectionChangeBroadcast GRead___FishNet_002EManaging_002EServer_002EClientConnectionChangeBroadcastFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		ClientConnectionChangeBroadcast result = default(ClientConnectionChangeBroadcast);
		result.Connected = InstancedExtension___ReadBoolean(reader);
		result.Id = InstancedExtension___ReadInt32(reader);
		return result;
	}

	public static EmptyStartScenesBroadcast GRead___FishNet_002EManaging_002EScened_002EEmptyStartScenesBroadcastFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		return default(EmptyStartScenesBroadcast);
	}

	public static LoadScenesBroadcast GRead___FishNet_002EManaging_002EScened_002ELoadScenesBroadcastFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		LoadScenesBroadcast result = default(LoadScenesBroadcast);
		result.QueueData = GRead___FishNet_002EManaging_002EScened_002ELoadQueueDataFishNet_002ESerializing_002EGenerateds(reader);
		return result;
	}

	public static LoadQueueData GRead___FishNet_002EManaging_002EScened_002ELoadQueueDataFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		if (InstancedExtension___ReadBoolean(reader))
		{
			return null;
		}
		LoadQueueData loadQueueData = new LoadQueueData();
		loadQueueData.SceneLoadData = GRead___FishNet_002EManaging_002EScened_002ESceneLoadDataFishNet_002ESerializing_002EGenerateds(reader);
		loadQueueData.GlobalScenes = GRead___System_002EString_005B_005DFishNet_002ESerializing_002EGenerateds(reader);
		return loadQueueData;
	}

	public static SceneLoadData GRead___FishNet_002EManaging_002EScened_002ESceneLoadDataFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		if (InstancedExtension___ReadBoolean(reader))
		{
			return null;
		}
		SceneLoadData sceneLoadData = new SceneLoadData();
		sceneLoadData.PreferredActiveScene = GRead___FishNet_002EManaging_002EScened_002EPreferredSceneFishNet_002ESerializing_002EGenerateds(reader);
		sceneLoadData.SceneLookupDatas = GRead___FishNet_002EManaging_002EScened_002ESceneLookupData_005B_005DFishNet_002ESerializing_002EGenerateds(reader);
		sceneLoadData.MovedNetworkObjects = GRead___FishNet_002EObject_002ENetworkObject_005B_005DFishNet_002ESerializing_002EGenerateds(reader);
		sceneLoadData.ReplaceScenes = GRead___FishNet_002EManaging_002EScened_002EReplaceOptionFishNet_002ESerializing_002EGenerateds(reader);
		sceneLoadData.Params = GRead___FishNet_002EManaging_002EScened_002ELoadParamsFishNet_002ESerializing_002EGenerateds(reader);
		sceneLoadData.Options = GRead___FishNet_002EManaging_002EScened_002ELoadOptionsFishNet_002ESerializing_002EGenerateds(reader);
		return sceneLoadData;
	}

	public static PreferredScene GRead___FishNet_002EManaging_002EScened_002EPreferredSceneFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		PreferredScene result = default(PreferredScene);
		result.Client = GRead___FishNet_002EManaging_002EScened_002ESceneLookupDataFishNet_002ESerializing_002EGenerateds(reader);
		result.Server = GRead___FishNet_002EManaging_002EScened_002ESceneLookupDataFishNet_002ESerializing_002EGenerateds(reader);
		return result;
	}

	public static SceneLookupData GRead___FishNet_002EManaging_002EScened_002ESceneLookupDataFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		if (InstancedExtension___ReadBoolean(reader))
		{
			return null;
		}
		SceneLookupData sceneLookupData = new SceneLookupData();
		sceneLookupData.Handle = InstancedExtension___ReadInt32(reader);
		sceneLookupData.Name = InstancedExtension___ReadStringAllocated(reader);
		return sceneLookupData;
	}

	public static SceneLookupData[] GRead___FishNet_002EManaging_002EScened_002ESceneLookupData_005B_005DFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		return reader.ReadArrayAllocated<SceneLookupData>();
	}

	public static NetworkObject[] GRead___FishNet_002EObject_002ENetworkObject_005B_005DFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		return reader.ReadArrayAllocated<NetworkObject>();
	}

	public static ReplaceOption GRead___FishNet_002EManaging_002EScened_002EReplaceOptionFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		return (ReplaceOption)InstancedExtension___ReadUInt8Unpacked(reader);
	}

	public static LoadParams GRead___FishNet_002EManaging_002EScened_002ELoadParamsFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		if (InstancedExtension___ReadBoolean(reader))
		{
			return null;
		}
		LoadParams loadParams = new LoadParams();
		loadParams.ClientParams = InstancedExtension___ReadUInt8ArrayAndSizeAllocated(reader);
		return loadParams;
	}

	public static LoadOptions GRead___FishNet_002EManaging_002EScened_002ELoadOptionsFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		if (InstancedExtension___ReadBoolean(reader))
		{
			return null;
		}
		LoadOptions loadOptions = new LoadOptions();
		loadOptions.ReloadScenes = InstancedExtension___ReadBoolean(reader);
		loadOptions.Addressables = InstancedExtension___ReadBoolean(reader);
		return loadOptions;
	}

	public static string[] GRead___System_002EString_005B_005DFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		return reader.ReadArrayAllocated<string>();
	}

	public static UnloadScenesBroadcast GRead___FishNet_002EManaging_002EScened_002EUnloadScenesBroadcastFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		UnloadScenesBroadcast result = default(UnloadScenesBroadcast);
		result.QueueData = GRead___FishNet_002EManaging_002EScened_002EUnloadQueueDataFishNet_002ESerializing_002EGenerateds(reader);
		return result;
	}

	public static UnloadQueueData GRead___FishNet_002EManaging_002EScened_002EUnloadQueueDataFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		if (InstancedExtension___ReadBoolean(reader))
		{
			return null;
		}
		UnloadQueueData unloadQueueData = new UnloadQueueData();
		unloadQueueData.SceneUnloadData = GRead___FishNet_002EManaging_002EScened_002ESceneUnloadDataFishNet_002ESerializing_002EGenerateds(reader);
		unloadQueueData.GlobalScenes = GRead___System_002EString_005B_005DFishNet_002ESerializing_002EGenerateds(reader);
		return unloadQueueData;
	}

	public static SceneUnloadData GRead___FishNet_002EManaging_002EScened_002ESceneUnloadDataFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		if (InstancedExtension___ReadBoolean(reader))
		{
			return null;
		}
		SceneUnloadData sceneUnloadData = new SceneUnloadData();
		sceneUnloadData.PreferredActiveScene = GRead___FishNet_002EManaging_002EScened_002EPreferredSceneFishNet_002ESerializing_002EGenerateds(reader);
		sceneUnloadData.SceneLookupDatas = GRead___FishNet_002EManaging_002EScened_002ESceneLookupData_005B_005DFishNet_002ESerializing_002EGenerateds(reader);
		sceneUnloadData.Params = GRead___FishNet_002EManaging_002EScened_002EUnloadParamsFishNet_002ESerializing_002EGenerateds(reader);
		sceneUnloadData.Options = GRead___FishNet_002EManaging_002EScened_002EUnloadOptionsFishNet_002ESerializing_002EGenerateds(reader);
		return sceneUnloadData;
	}

	public static UnloadParams GRead___FishNet_002EManaging_002EScened_002EUnloadParamsFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		if (InstancedExtension___ReadBoolean(reader))
		{
			return null;
		}
		UnloadParams unloadParams = new UnloadParams();
		unloadParams.ClientParams = InstancedExtension___ReadUInt8ArrayAndSizeAllocated(reader);
		return unloadParams;
	}

	public static UnloadOptions GRead___FishNet_002EManaging_002EScened_002EUnloadOptionsFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		if (InstancedExtension___ReadBoolean(reader))
		{
			return null;
		}
		UnloadOptions unloadOptions = new UnloadOptions();
		unloadOptions.Mode = GRead___FishNet_002EManaging_002EScened_002EUnloadOptions_002FServerUnloadModeFishNet_002ESerializing_002EGenerateds(reader);
		unloadOptions.Addressables = InstancedExtension___ReadBoolean(reader);
		return unloadOptions;
	}

	public static UnloadOptions.ServerUnloadMode GRead___FishNet_002EManaging_002EScened_002EUnloadOptions_002FServerUnloadModeFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		return (UnloadOptions.ServerUnloadMode)InstancedExtension___ReadInt32(reader);
	}

	public static ClientScenesLoadedBroadcast GRead___FishNet_002EManaging_002EScened_002EClientScenesLoadedBroadcastFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		ClientScenesLoadedBroadcast result = default(ClientScenesLoadedBroadcast);
		result.SceneLookupDatas = GRead___FishNet_002EManaging_002EScened_002ESceneLookupData_005B_005DFishNet_002ESerializing_002EGenerateds(reader);
		return result;
	}

	public static SynchronizedProperty GRead___FishNet_002EComponent_002ETransforming_002ESynchronizedPropertyFishNet_002ESerializing_002EGenerateds(Reader reader)
	{
		return (SynchronizedProperty)InstancedExtension___ReadUInt8Unpacked(reader);
	}
}
