using System;
using System.Collections.Generic;
using FishNet.CodeGenerating;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using GameKit.Dependencies.Utilities.Types;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;

namespace FishNet.Serializing;

public class Writer
{
	[Flags]
	internal enum UnsignedVector3DeltaFlag
	{
		Unset = 0,
		More = 1,
		X1 = 2,
		NextXIsLarger = 4,
		Y1 = 8,
		NextYIsLarger = 0x10,
		Z1 = 0x20,
		NextZIsLarger = 0x40,
		X2 = 0x100,
		X4 = 0x200,
		Y2 = 0x400,
		Y4 = 0x800,
		Z2 = 0x1000,
		Z4 = 0x2000
	}

	private ReservedLengthWriter _reservedLengthWriter = new ReservedLengthWriter();

	private const double LARGEST_DELTA_PRECISION_INT8 = 0.127;

	private const double LARGEST_DELTA_PRECISION_INT16 = 32.767;

	private const double LARGEST_DELTA_PRECISION_INT32 = 2147483.647;

	private const double LARGEST_DELTA_PRECISION_INT64 = 9223372036854776.0;

	private const double LARGEST_DELTA_PRECISION_UINT8 = 0.255;

	private const double LARGEST_DELTA_PRECISION_UINT16 = 65.535;

	private const double LARGEST_DELTA_PRECISION_UINT32 = 4294967.295;

	private const double LARGEST_DELTA_PRECISION_UINT64 = 18446744073709550.0;

	internal const double DOUBLE_ACCURACY = 1000.0;

	internal const double DOUBLE_ACCURACY_PRECISION = 0.001;

	internal const decimal DECIMAL_ACCURACY = 1000m;

	internal const float QUATERNION_PRECISION = 0.0001f;

	public int Position;

	public int Length;

	public NetworkManager NetworkManager;

	private byte[] _buffer = new byte[64];

	internal const byte REPLICATE_DEFAULT_BYTE = 0;

	internal const byte REPLICATE_DUPLICATE_BYTE = 1;

	internal const byte REPLICATE_UNIQUE_BYTE = 2;

	internal const byte REPLICATE_REPEATING_BYTE = 3;

	internal const byte REPLICATE_ALL_DEFAULT_BYTE = 4;

	public const int UNSET_COLLECTION_SIZE_VALUE = -1;

	public int Capacity => _buffer.Length;

	public void WriteSubStream(SubStream value)
	{
		if (!value.Initialized)
		{
			WriteInt32(-1);
			return;
		}
		PooledWriter writer = value.GetWriter();
		WriteInt32(writer.Length);
		WriteUInt8Array(writer.GetBuffer(), 0, writer.Length);
	}

	public void Writebool2(bool2 value)
	{
		byte b = 0;
		if (value.x)
		{
			b = (byte)(b | 1u);
		}
		if (value.y)
		{
			b = (byte)(b | 2u);
		}
		WriteUInt8Unpacked(b);
	}

	public void Writebool3(bool3 value)
	{
		byte b = 0;
		if (value.x)
		{
			b = (byte)(b | 1u);
		}
		if (value.y)
		{
			b = (byte)(b | 2u);
		}
		if (value.z)
		{
			b = (byte)(b | 4u);
		}
		WriteUInt8Unpacked(b);
	}

	public void Writebool4(bool4 value)
	{
		byte b = 0;
		if (value.x)
		{
			b = (byte)(b | 1u);
		}
		if (value.y)
		{
			b = (byte)(b | 2u);
		}
		if (value.z)
		{
			b = (byte)(b | 4u);
		}
		if (value.w)
		{
			b = (byte)(b | 8u);
		}
		WriteUInt8Unpacked(b);
	}

	public void Writebool2x2(bool2x2 value)
	{
		byte b = 0;
		if (value.c0.x)
		{
			b = (byte)(b | 1u);
		}
		if (value.c0.y)
		{
			b = (byte)(b | 2u);
		}
		if (value.c1.x)
		{
			b = (byte)(b | 4u);
		}
		if (value.c1.y)
		{
			b = (byte)(b | 8u);
		}
		WriteUInt8Unpacked(b);
	}

	public void Writebool2x3(bool2x3 value)
	{
		byte b = 0;
		if (value.c0.x)
		{
			b = (byte)(b | 1u);
		}
		if (value.c0.y)
		{
			b = (byte)(b | 2u);
		}
		if (value.c1.x)
		{
			b = (byte)(b | 4u);
		}
		if (value.c1.y)
		{
			b = (byte)(b | 8u);
		}
		if (value.c2.x)
		{
			b = (byte)(b | 0x10u);
		}
		if (value.c2.y)
		{
			b = (byte)(b | 0x20u);
		}
		WriteUInt8Unpacked(b);
	}

	public void Writebool2x4(bool2x4 value)
	{
		byte b = 0;
		if (value.c0.x)
		{
			b = (byte)(b | 1u);
		}
		if (value.c0.y)
		{
			b = (byte)(b | 2u);
		}
		if (value.c1.x)
		{
			b = (byte)(b | 4u);
		}
		if (value.c1.y)
		{
			b = (byte)(b | 8u);
		}
		if (value.c2.x)
		{
			b = (byte)(b | 0x10u);
		}
		if (value.c2.y)
		{
			b = (byte)(b | 0x20u);
		}
		if (value.c3.x)
		{
			b = (byte)(b | 0x40u);
		}
		if (value.c3.y)
		{
			b = (byte)(b | 0x80u);
		}
		WriteUInt8Unpacked(b);
	}

	public void Writebool3x2(bool3x2 value)
	{
		byte b = 0;
		if (value.c0.x)
		{
			b = (byte)(b | 1u);
		}
		if (value.c0.y)
		{
			b = (byte)(b | 2u);
		}
		if (value.c0.z)
		{
			b = (byte)(b | 4u);
		}
		if (value.c1.x)
		{
			b = (byte)(b | 8u);
		}
		if (value.c1.y)
		{
			b = (byte)(b | 0x10u);
		}
		if (value.c1.z)
		{
			b = (byte)(b | 0x20u);
		}
		WriteUInt8Unpacked(b);
	}

	public void Writebool3x3(bool3x3 value)
	{
		ushort num = 0;
		if (value.c0.x)
		{
			num = (ushort)(num | 1u);
		}
		if (value.c0.y)
		{
			num = (ushort)(num | 2u);
		}
		if (value.c0.z)
		{
			num = (ushort)(num | 4u);
		}
		if (value.c1.x)
		{
			num = (ushort)(num | 8u);
		}
		if (value.c1.y)
		{
			num = (ushort)(num | 0x10u);
		}
		if (value.c1.z)
		{
			num = (ushort)(num | 0x20u);
		}
		if (value.c2.x)
		{
			num = (ushort)(num | 0x40u);
		}
		if (value.c2.y)
		{
			num = (ushort)(num | 0x80u);
		}
		if (value.c2.z)
		{
			num = (ushort)(num | 0x100u);
		}
		WriteUInt16(num);
	}

	public void Writebool3x4(bool3x4 value)
	{
		ushort num = 0;
		if (value.c0.x)
		{
			num = (ushort)(num | 1u);
		}
		if (value.c0.y)
		{
			num = (ushort)(num | 2u);
		}
		if (value.c0.z)
		{
			num = (ushort)(num | 4u);
		}
		if (value.c1.x)
		{
			num = (ushort)(num | 8u);
		}
		if (value.c1.y)
		{
			num = (ushort)(num | 0x10u);
		}
		if (value.c1.z)
		{
			num = (ushort)(num | 0x20u);
		}
		if (value.c2.x)
		{
			num = (ushort)(num | 0x40u);
		}
		if (value.c2.y)
		{
			num = (ushort)(num | 0x80u);
		}
		if (value.c2.z)
		{
			num = (ushort)(num | 0x100u);
		}
		if (value.c3.x)
		{
			num = (ushort)(num | 0x200u);
		}
		if (value.c3.y)
		{
			num = (ushort)(num | 0x400u);
		}
		if (value.c3.z)
		{
			num = (ushort)(num | 0x800u);
		}
		WriteUInt16(num);
	}

	public void Writebool4x2(bool4x2 value)
	{
		byte b = 0;
		if (value.c0.x)
		{
			b = (byte)(b | 1u);
		}
		if (value.c0.y)
		{
			b = (byte)(b | 2u);
		}
		if (value.c0.z)
		{
			b = (byte)(b | 4u);
		}
		if (value.c0.w)
		{
			b = (byte)(b | 8u);
		}
		if (value.c1.x)
		{
			b = (byte)(b | 0x10u);
		}
		if (value.c1.y)
		{
			b = (byte)(b | 0x20u);
		}
		if (value.c1.z)
		{
			b = (byte)(b | 0x40u);
		}
		if (value.c1.w)
		{
			b = (byte)(b | 0x80u);
		}
		WriteUInt8Unpacked(b);
	}

	public void Writebool4x3(bool4x3 value)
	{
		ushort num = 0;
		if (value.c0.x)
		{
			num = (ushort)(num | 1u);
		}
		if (value.c0.y)
		{
			num = (ushort)(num | 2u);
		}
		if (value.c0.z)
		{
			num = (ushort)(num | 4u);
		}
		if (value.c0.w)
		{
			num = (ushort)(num | 8u);
		}
		if (value.c1.x)
		{
			num = (ushort)(num | 0x10u);
		}
		if (value.c1.y)
		{
			num = (ushort)(num | 0x20u);
		}
		if (value.c1.z)
		{
			num = (ushort)(num | 0x40u);
		}
		if (value.c1.w)
		{
			num = (ushort)(num | 0x80u);
		}
		if (value.c2.x)
		{
			num = (ushort)(num | 0x100u);
		}
		if (value.c2.y)
		{
			num = (ushort)(num | 0x200u);
		}
		if (value.c2.z)
		{
			num = (ushort)(num | 0x400u);
		}
		if (value.c2.w)
		{
			num = (ushort)(num | 0x800u);
		}
		WriteUInt16(num);
	}

	public void Writebool4x4(bool4x4 value)
	{
		ushort num = 0;
		if (value.c0.x)
		{
			num = (ushort)(num | 1u);
		}
		if (value.c0.y)
		{
			num = (ushort)(num | 2u);
		}
		if (value.c0.z)
		{
			num = (ushort)(num | 4u);
		}
		if (value.c0.w)
		{
			num = (ushort)(num | 8u);
		}
		if (value.c1.x)
		{
			num = (ushort)(num | 0x10u);
		}
		if (value.c1.y)
		{
			num = (ushort)(num | 0x20u);
		}
		if (value.c1.z)
		{
			num = (ushort)(num | 0x40u);
		}
		if (value.c1.w)
		{
			num = (ushort)(num | 0x80u);
		}
		if (value.c2.x)
		{
			num = (ushort)(num | 0x100u);
		}
		if (value.c2.y)
		{
			num = (ushort)(num | 0x200u);
		}
		if (value.c2.z)
		{
			num = (ushort)(num | 0x400u);
		}
		if (value.c2.w)
		{
			num = (ushort)(num | 0x800u);
		}
		if (value.c3.x)
		{
			num = (ushort)(num | 0x1000u);
		}
		if (value.c3.y)
		{
			num = (ushort)(num | 0x2000u);
		}
		if (value.c3.z)
		{
			num = (ushort)(num | 0x4000u);
		}
		if (value.c3.w)
		{
			num = (ushort)(num | 0x8000u);
		}
		WriteUInt16(num);
	}

	public void Writedouble2(double2 value)
	{
		WriteDouble(value.x);
		WriteDouble(value.y);
	}

	public void Writedouble3(double3 value)
	{
		WriteDouble(value.x);
		WriteDouble(value.y);
		WriteDouble(value.z);
	}

	public void Writedouble4(double4 value)
	{
		WriteDouble(value.x);
		WriteDouble(value.y);
		WriteDouble(value.z);
		WriteDouble(value.w);
	}

	public void Writedouble2x2(double2x2 value)
	{
		Writedouble2(value.c0);
		Writedouble2(value.c1);
	}

	public void Writedouble2x3(double2x3 value)
	{
		Writedouble2(value.c0);
		Writedouble2(value.c1);
		Writedouble2(value.c2);
	}

	public void Writedouble2x4(double2x4 value)
	{
		Writedouble2(value.c0);
		Writedouble2(value.c1);
		Writedouble2(value.c2);
		Writedouble2(value.c3);
	}

	public void Writedouble3x2(double3x2 value)
	{
		Writedouble3(value.c0);
		Writedouble3(value.c1);
	}

	public void Writedouble4x2(double4x2 value)
	{
		Writedouble4(value.c0);
		Writedouble4(value.c1);
	}

	public void Writedouble3x4(double3x4 value)
	{
		Writedouble3(value.c0);
		Writedouble3(value.c1);
		Writedouble3(value.c2);
		Writedouble3(value.c3);
	}

	public void Writedouble4x3(double4x3 value)
	{
		Writedouble4(value.c0);
		Writedouble4(value.c1);
		Writedouble4(value.c2);
	}

	public void Writedouble3x3(double3x3 value)
	{
		Writedouble3(value.c0);
		Writedouble3(value.c1);
		Writedouble3(value.c2);
	}

	public void Writedouble4x4(double4x4 value)
	{
		Writedouble4(value.c0);
		Writedouble4(value.c1);
		Writedouble4(value.c2);
		Writedouble4(value.c3);
	}

	public void Writefloat2(float2 value)
	{
		WriteSingle(value.x);
		WriteSingle(value.y);
	}

	public void Writefloat3(float3 value)
	{
		WriteSingle(value.x);
		WriteSingle(value.y);
		WriteSingle(value.z);
	}

	public void Writefloat4(float4 value)
	{
		WriteSingle(value.x);
		WriteSingle(value.y);
		WriteSingle(value.z);
		WriteSingle(value.w);
	}

	public void Writefloat2x2(float2x2 value)
	{
		Writefloat2(value.c0);
		Writefloat2(value.c1);
	}

	public void Writefloat2x3(float2x3 value)
	{
		Writefloat2(value.c0);
		Writefloat2(value.c1);
		Writefloat2(value.c2);
	}

	public void Writefloat2x4(float2x4 value)
	{
		Writefloat2(value.c0);
		Writefloat2(value.c1);
		Writefloat2(value.c2);
		Writefloat2(value.c3);
	}

	public void Writefloat3x2(float3x2 value)
	{
		Writefloat3(value.c0);
		Writefloat3(value.c1);
	}

	public void Writefloat3x3(float3x3 value)
	{
		Writefloat3(value.c0);
		Writefloat3(value.c1);
		Writefloat3(value.c2);
	}

	public void Writefloat3x4(float3x4 value)
	{
		Writefloat3(value.c0);
		Writefloat3(value.c1);
		Writefloat3(value.c2);
		Writefloat3(value.c3);
	}

	public void Writefloat4x2(float4x2 value)
	{
		Writefloat4(value.c0);
		Writefloat4(value.c1);
	}

	public void Writefloat4x3(float4x3 value)
	{
		Writefloat4(value.c0);
		Writefloat4(value.c1);
		Writefloat4(value.c2);
	}

	public void Writefloat4x4(float4x4 value)
	{
		Writefloat4(value.c0);
		Writefloat4(value.c1);
		Writefloat4(value.c2);
		Writefloat4(value.c3);
	}

	public void Writehalf(half value)
	{
		WriteUInt16(value.value);
	}

	public void Writehalf2(half2 value)
	{
		WriteUInt16(value.x.value);
		WriteUInt16(value.y.value);
	}

	public void Writehalf3(half3 value)
	{
		WriteUInt16(value.x.value);
		WriteUInt16(value.y.value);
		WriteUInt16(value.z.value);
	}

	public void Writehalf4(half4 value)
	{
		WriteUInt16(value.x.value);
		WriteUInt16(value.y.value);
		WriteUInt16(value.z.value);
		WriteUInt16(value.w.value);
	}

	public void Writeint2(int2 value)
	{
		WriteInt32(value.x);
		WriteInt32(value.y);
	}

	public void Writeint3(int3 value)
	{
		WriteInt32(value.x);
		WriteInt32(value.y);
		WriteInt32(value.z);
	}

	public void Writeint4(int4 value)
	{
		WriteInt32(value.x);
		WriteInt32(value.y);
		WriteInt32(value.z);
		WriteInt32(value.w);
	}

	public void Writeint2x2(int2x2 value)
	{
		Writeint2(value.c0);
		Writeint2(value.c1);
	}

	public void Writeint2x3(int2x3 value)
	{
		Writeint2(value.c0);
		Writeint2(value.c1);
		Writeint2(value.c2);
	}

	public void Writeint2x4(int2x4 value)
	{
		Writeint2(value.c0);
		Writeint2(value.c1);
		Writeint2(value.c2);
		Writeint2(value.c3);
	}

	public void Writeint3x2(int3x2 value)
	{
		Writeint3(value.c0);
		Writeint3(value.c1);
	}

	public void Writeint3x3(int3x3 value)
	{
		Writeint3(value.c0);
		Writeint3(value.c1);
		Writeint3(value.c2);
	}

	public void Writeint3x4(int3x4 value)
	{
		Writeint3(value.c0);
		Writeint3(value.c1);
		Writeint3(value.c2);
		Writeint3(value.c3);
	}

	public void Writeint4x2(int4x2 value)
	{
		Writeint4(value.c0);
		Writeint4(value.c1);
	}

	public void Writeint4x3(int4x3 value)
	{
		Writeint4(value.c0);
		Writeint4(value.c1);
		Writeint4(value.c2);
	}

	public void Writeint4x4(int4x4 value)
	{
		Writeint4(value.c0);
		Writeint4(value.c1);
		Writeint4(value.c2);
		Writeint4(value.c3);
	}

	public void Writequaternion(quaternion value)
	{
		Writefloat4(value.value);
	}

	public void Writerandom(Unity.Mathematics.Random random)
	{
		WriteUInt32(random.state);
	}

	public void WriteRigidTransform(RigidTransform value)
	{
		Writequaternion(value.rot);
		Writefloat3(value.pos);
	}

	public void WriteAffineTransform(AffineTransform value)
	{
		Writefloat3x3(value.rs);
		Writefloat3(value.t);
	}

	public void ReadMinMaxAABB(MinMaxAABB minMaxAABB)
	{
		Writefloat3(minMaxAABB.Min);
		Writefloat3(minMaxAABB.Max);
	}

	public void Writeuint2(uint2 value)
	{
		WriteUInt32(value.x);
		WriteUInt32(value.y);
	}

	public void Writeuint3(uint3 value)
	{
		WriteUInt32(value.x);
		WriteUInt32(value.y);
		WriteUInt32(value.z);
	}

	public void Writeuint4(uint4 value)
	{
		WriteUInt32(value.x);
		WriteUInt32(value.y);
		WriteUInt32(value.z);
		WriteUInt32(value.w);
	}

	public void Writeuint2x2(uint2x2 value)
	{
		Writeuint2(value.c0);
		Writeuint2(value.c1);
	}

	public void Writeuint2x3(uint2x3 value)
	{
		Writeuint2(value.c0);
		Writeuint2(value.c1);
		Writeuint2(value.c2);
	}

	public void Writeuint2x4(uint2x4 value)
	{
		Writeuint2(value.c0);
		Writeuint2(value.c1);
		Writeuint2(value.c2);
		Writeuint2(value.c3);
	}

	public void Writeuint3x2(uint3x2 value)
	{
		Writeuint3(value.c0);
		Writeuint3(value.c1);
	}

	public void Writeuint3x3(uint3x3 value)
	{
		Writeuint3(value.c0);
		Writeuint3(value.c1);
		Writeuint3(value.c2);
	}

	public void Writeuint3x4(uint3x4 value)
	{
		Writeuint3(value.c0);
		Writeuint3(value.c1);
		Writeuint3(value.c2);
		Writeuint3(value.c3);
	}

	public void Writeuint4x2(uint4x2 value)
	{
		Writeuint4(value.c0);
		Writeuint4(value.c1);
	}

	public void Writeuint4x3(uint4x3 value)
	{
		Writeuint4(value.c0);
		Writeuint4(value.c1);
		Writeuint4(value.c2);
	}

	public void Writeuint4x4(uint4x4 value)
	{
		Writeuint4(value.c0);
		Writeuint4(value.c1);
		Writeuint4(value.c2);
		Writeuint4(value.c3);
	}

	[DefaultDeltaWriter]
	public bool WriteDeltaBoolean(bool valueA, bool valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		if (valueA == valueB && option == DeltaSerializerOption.Unset)
		{
			return false;
		}
		WriteBoolean(valueB);
		return true;
	}

	[DefaultDeltaWriter]
	public bool WriteDeltaInt8(sbyte valueA, sbyte valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		return WriteDifference8_16_32(valueA, valueB, option);
	}

	[DefaultDeltaWriter]
	public bool WriteDeltaUInt8(byte valueA, byte valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		return WriteDifference8_16_32(valueA, valueB, option);
	}

	[DefaultDeltaWriter]
	public bool WriteDeltaInt16(short valueA, short valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		return WriteDifference8_16_32(valueA, valueB, option);
	}

	[DefaultDeltaWriter]
	public bool WriteDeltaUInt16(ushort valueA, ushort valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		return WriteDifference8_16_32(valueA, valueB, option);
	}

	[DefaultDeltaWriter]
	public bool WriteDeltaInt32(int valueA, int valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		return WriteDifference8_16_32(valueA, valueB, option);
	}

	[DefaultDeltaWriter]
	public bool WriteDeltaUInt32(uint valueA, uint valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		return WriteDifference8_16_32(valueA, valueB, option);
	}

	[DefaultDeltaWriter]
	public bool WriteDeltaInt64(long valueA, long valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		return WriteDeltaUInt64((ulong)valueA, (ulong)valueB, option);
	}

	[DefaultDeltaWriter]
	public bool WriteDeltaUInt64(ulong valueA, ulong valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		if (valueA == valueB && option == DeltaSerializerOption.Unset)
		{
			return false;
		}
		bool flag = valueB > valueA;
		ulong value = (flag ? (valueB - valueA) : (valueA - valueB));
		WriteBoolean(flag);
		WriteUnsignedPackedWhole(value);
		return true;
	}

	private bool WriteDifference8_16_32(long valueA, long valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		if (valueA == valueB && option == DeltaSerializerOption.Unset)
		{
			return false;
		}
		long value = valueB - valueA;
		WriteSignedPackedWhole(value);
		return true;
	}

	[DefaultDeltaWriter]
	public bool WriteUDeltaSingle(float valueA, float valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		float unsignedDifference;
		UDeltaPrecisionType uDeltaPrecisionType = GetUDeltaPrecisionType(valueA, valueB, out unsignedDifference);
		if (uDeltaPrecisionType == UDeltaPrecisionType.Unset && option == DeltaSerializerOption.Unset)
		{
			return false;
		}
		WriteUInt8Unpacked((byte)uDeltaPrecisionType);
		WriteDeltaSingle(uDeltaPrecisionType, unsignedDifference, unsigned: true);
		return true;
	}

	private void WriteDeltaSingle(UDeltaPrecisionType dpt, float value, bool unsigned)
	{
		if (dpt.FastContains(UDeltaPrecisionType.UInt8))
		{
			if (unsigned)
			{
				WriteUInt8Unpacked((byte)System.Math.Floor((double)value * 1000.0));
			}
			else
			{
				WriteInt8Unpacked((sbyte)System.Math.Floor((double)value * 1000.0));
			}
		}
		else if (dpt.FastContains(UDeltaPrecisionType.UInt16))
		{
			if (unsigned)
			{
				WriteUInt16Unpacked((ushort)System.Math.Floor((double)value * 1000.0));
			}
			else
			{
				WriteInt16Unpacked((short)System.Math.Floor((double)value * 1000.0));
			}
		}
		else
		{
			WriteSingleUnpacked(value);
		}
	}

	public UDeltaPrecisionType GetSDeltaPrecisionType(float valueA, float valueB, out float signedDifference)
	{
		signedDifference = valueB - valueA;
		float positiveValue = ((signedDifference < 0f) ? (signedDifference * -1f) : signedDifference);
		return GetDeltaPrecisionType(positiveValue, unsigned: false);
	}

	public UDeltaPrecisionType GetUDeltaPrecisionType(float valueA, float valueB, out float unsignedDifference)
	{
		bool num = valueB > valueA;
		if (num)
		{
			unsignedDifference = valueB - valueA;
		}
		else
		{
			unsignedDifference = valueA - valueB;
		}
		UDeltaPrecisionType uDeltaPrecisionType = GetDeltaPrecisionType(unsignedDifference, unsigned: true);
		if (num && uDeltaPrecisionType != 0)
		{
			uDeltaPrecisionType |= UDeltaPrecisionType.NextValueIsLarger;
		}
		return uDeltaPrecisionType;
	}

	public UDeltaPrecisionType GetDeltaPrecisionType(float positiveValue, bool unsigned)
	{
		if (unsigned)
		{
			if (positiveValue < 65.535f)
			{
				if (!(positiveValue < 0.001f))
				{
					if (positiveValue < 0.255f)
					{
						return UDeltaPrecisionType.UInt8;
					}
					return UDeltaPrecisionType.UInt16;
				}
				return UDeltaPrecisionType.Unset;
			}
			if (positiveValue < 13493039f / MathF.PI)
			{
				return UDeltaPrecisionType.UInt32;
			}
			return UDeltaPrecisionType.Unset;
		}
		if (positiveValue < 32.767f)
		{
			if (!(positiveValue < 0.0005f))
			{
				if (positiveValue < 0.127f)
				{
					return UDeltaPrecisionType.UInt8;
				}
				return UDeltaPrecisionType.UInt16;
			}
			return UDeltaPrecisionType.Unset;
		}
		if (positiveValue < 5837466f / MathF.E)
		{
			return UDeltaPrecisionType.UInt32;
		}
		return UDeltaPrecisionType.Unset;
	}

	[DefaultDeltaWriter]
	public bool WriteUDeltaDouble(double valueA, double valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		double unsignedDifference;
		UDeltaPrecisionType uDeltaPrecisionType = GetUDeltaPrecisionType(valueA, valueB, out unsignedDifference);
		if (uDeltaPrecisionType == UDeltaPrecisionType.Unset && option == DeltaSerializerOption.Unset)
		{
			return false;
		}
		WriteUInt8Unpacked((byte)uDeltaPrecisionType);
		WriteDeltaDouble(uDeltaPrecisionType, unsignedDifference, unsigned: true);
		return true;
	}

	private void WriteDeltaDouble(UDeltaPrecisionType dpt, double value, bool unsigned)
	{
		if (dpt.FastContains(UDeltaPrecisionType.UInt8))
		{
			if (unsigned)
			{
				WriteUInt8Unpacked((byte)System.Math.Floor(value * 1000.0));
			}
			else
			{
				WriteInt8Unpacked((sbyte)System.Math.Floor(value * 1000.0));
			}
		}
		else if (dpt.FastContains(UDeltaPrecisionType.UInt16))
		{
			if (unsigned)
			{
				WriteUInt16Unpacked((ushort)System.Math.Floor(value * 1000.0));
			}
			else
			{
				WriteInt16Unpacked((short)System.Math.Floor(value * 1000.0));
			}
		}
		else if (dpt.FastContains(UDeltaPrecisionType.UInt32))
		{
			if (unsigned)
			{
				WriteUInt32Unpacked((uint)System.Math.Floor(value * 1000.0));
			}
			else
			{
				WriteInt32Unpacked((int)System.Math.Floor(value * 1000.0));
			}
		}
		else if (dpt.FastContains(UDeltaPrecisionType.Unset))
		{
			WriteDoubleUnpacked(value);
		}
		else
		{
			NetworkManagerExtensions.LogError($"Unhandled precision type of {dpt}.");
		}
	}

	public UDeltaPrecisionType GetSDeltaPrecisionType(double valueA, double valueB, out double signedDifference)
	{
		signedDifference = valueB - valueA;
		double positiveValue = ((signedDifference < 0.0) ? (signedDifference * -1.0) : signedDifference);
		return GetDeltaPrecisionType(positiveValue, unsigned: false);
	}

	public UDeltaPrecisionType GetUDeltaPrecisionType(double valueA, double valueB, out double unsignedDifference)
	{
		bool num = valueB > valueA;
		if (num)
		{
			unsignedDifference = valueB - valueA;
		}
		else
		{
			unsignedDifference = valueA - valueB;
		}
		UDeltaPrecisionType uDeltaPrecisionType = GetDeltaPrecisionType(unsignedDifference, unsigned: true);
		if (num && uDeltaPrecisionType != 0)
		{
			uDeltaPrecisionType |= UDeltaPrecisionType.NextValueIsLarger;
		}
		return uDeltaPrecisionType;
	}

	public UDeltaPrecisionType GetDeltaPrecisionType(double positiveValue, bool unsigned)
	{
		if (unsigned)
		{
			if (positiveValue < 65.535)
			{
				if (positiveValue < 0.255)
				{
					return UDeltaPrecisionType.UInt8;
				}
				return UDeltaPrecisionType.UInt16;
			}
			if (positiveValue < 4294967.295)
			{
				return UDeltaPrecisionType.UInt32;
			}
			return UDeltaPrecisionType.Unset;
		}
		if (positiveValue < 32.767)
		{
			if (positiveValue < 0.127)
			{
				return UDeltaPrecisionType.UInt8;
			}
			return UDeltaPrecisionType.UInt16;
		}
		if (positiveValue < 2147483.647)
		{
			return UDeltaPrecisionType.UInt32;
		}
		return UDeltaPrecisionType.Unset;
	}

	[DefaultDeltaWriter]
	public bool WriteUDeltaDecimal(decimal valueA, decimal valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		decimal unsignedDifference;
		UDeltaPrecisionType uDeltaPrecisionType = GetUDeltaPrecisionType(valueA, valueB, out unsignedDifference);
		if (uDeltaPrecisionType == UDeltaPrecisionType.Unset && option == DeltaSerializerOption.Unset)
		{
			return false;
		}
		WriteUInt8Unpacked((byte)uDeltaPrecisionType);
		WriteDeltaDecimal(uDeltaPrecisionType, unsignedDifference, unsigned: true);
		return true;
	}

	private void WriteDeltaDecimal(UDeltaPrecisionType dpt, decimal value, bool unsigned)
	{
		if (dpt.FastContains(UDeltaPrecisionType.UInt8))
		{
			if (unsigned)
			{
				WriteUInt8Unpacked((byte)System.Math.Floor(value * 1000m));
			}
			else
			{
				WriteInt8Unpacked((sbyte)System.Math.Floor(value * 1000m));
			}
		}
		else if (dpt.FastContains(UDeltaPrecisionType.UInt16))
		{
			if (unsigned)
			{
				WriteUInt16Unpacked((ushort)System.Math.Floor(value * 1000m));
			}
			else
			{
				WriteInt16Unpacked((short)System.Math.Floor(value * 1000m));
			}
		}
		else if (dpt.FastContains(UDeltaPrecisionType.UInt32))
		{
			if (unsigned)
			{
				WriteUInt32Unpacked((uint)System.Math.Floor(value * 1000m));
			}
			else
			{
				WriteInt32Unpacked((int)System.Math.Floor(value * 1000m));
			}
		}
		else if (dpt.FastContains(UDeltaPrecisionType.UInt64))
		{
			if (unsigned)
			{
				WriteUInt64Unpacked((ulong)System.Math.Floor(value * 1000m));
			}
			else
			{
				WriteInt64Unpacked((long)System.Math.Floor(value * 1000m));
			}
		}
		else if (dpt.FastContains(UDeltaPrecisionType.Unset))
		{
			WriteDecimalUnpacked(value);
		}
		else
		{
			NetworkManagerExtensions.LogError($"Unhandled precision type of {dpt}.");
		}
	}

	public UDeltaPrecisionType GetSDeltaPrecisionType(decimal valueA, decimal valueB, out decimal signedDifference)
	{
		signedDifference = valueB - valueA;
		decimal positiveValue = ((signedDifference < 0m) ? (signedDifference * -1m) : signedDifference);
		return GetDeltaPrecisionType(positiveValue, unsigned: false);
	}

	public UDeltaPrecisionType GetUDeltaPrecisionType(decimal valueA, decimal valueB, out decimal unsignedDifference)
	{
		bool num = valueB > valueA;
		if (num)
		{
			unsignedDifference = valueB - valueA;
		}
		else
		{
			unsignedDifference = valueA - valueB;
		}
		UDeltaPrecisionType uDeltaPrecisionType = GetDeltaPrecisionType(unsignedDifference, unsigned: true);
		if (num && uDeltaPrecisionType != 0)
		{
			uDeltaPrecisionType |= UDeltaPrecisionType.NextValueIsLarger;
		}
		return uDeltaPrecisionType;
	}

	public UDeltaPrecisionType GetDeltaPrecisionType(decimal positiveValue, bool unsigned)
	{
		if (unsigned)
		{
			if (positiveValue < 4294967.295m)
			{
				if (!(positiveValue < 0.255m))
				{
					if (positiveValue < 65.535m)
					{
						return UDeltaPrecisionType.UInt16;
					}
					return UDeltaPrecisionType.UInt32;
				}
				return UDeltaPrecisionType.UInt8;
			}
			if (positiveValue < 18446744073709600m)
			{
				return UDeltaPrecisionType.UInt64;
			}
			return UDeltaPrecisionType.Unset;
		}
		if (positiveValue < 2147483.647m)
		{
			if (!(positiveValue < 0.127m))
			{
				if (positiveValue < 32.767m)
				{
					return UDeltaPrecisionType.UInt16;
				}
				return UDeltaPrecisionType.UInt32;
			}
			return UDeltaPrecisionType.UInt8;
		}
		if (positiveValue < 9223372036854780m)
		{
			return UDeltaPrecisionType.UInt64;
		}
		return UDeltaPrecisionType.Unset;
	}

	[DefaultDeltaWriter]
	public bool WriteDeltaNetworkBehaviour(NetworkBehaviour valueA, NetworkBehaviour valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		if (valueA == valueB && option == DeltaSerializerOption.Unset)
		{
			return false;
		}
		WriteNetworkBehaviour(valueB);
		return true;
	}

	public bool WriteDeltaTransformProperties(TransformProperties valueA, TransformProperties valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		int position = Position;
		Skip(1);
		byte b = 0;
		if (WriteDeltaVector3(valueA.Position, valueB.Position, DeltaSerializerOption.Unset))
		{
			b = (byte)(b | 1u);
		}
		if (WriteDeltaQuaternion(valueA.Rotation, valueB.Rotation, 0.0001f, DeltaSerializerOption.Unset))
		{
			b = (byte)(b | 2u);
		}
		if (WriteDeltaVector3(valueA.Scale, valueB.Scale, DeltaSerializerOption.Unset))
		{
			b = (byte)(b | 4u);
		}
		if (b != 0 || option != DeltaSerializerOption.Unset)
		{
			InsertUInt8Unpacked(b, position);
			return true;
		}
		Position = position;
		return false;
	}

	[DefaultDeltaWriter]
	public bool WriteDeltaQuaternion(Quaternion valueA, Quaternion valueB, float precision = 0.0001f, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		if (option == DeltaSerializerOption.Unset && !IsQuaternionChanged(valueA, valueB))
		{
			return false;
		}
		QuaternionDeltaPrecisionCompression.Compress(this, valueA, valueB, precision);
		return true;
	}

	private bool IsQuaternionChanged(Quaternion valueA, Quaternion valueB)
	{
		if (Mathf.Abs(valueA.x - valueB.x) > 0.0025f)
		{
			return true;
		}
		if (Mathf.Abs(valueA.y - valueB.y) > 0.0025f)
		{
			return true;
		}
		if (Mathf.Abs(valueA.z - valueB.z) > 0.0025f)
		{
			return true;
		}
		if (Mathf.Abs(valueA.w - valueB.w) > 0.0025f)
		{
			return true;
		}
		return false;
	}

	[DefaultDeltaWriter]
	public bool WriteDeltaVector2(Vector2 valueA, Vector2 valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		byte b = 0;
		int position = Position;
		Skip(1);
		if (WriteUDeltaSingle(valueA.x, valueB.x, DeltaSerializerOption.Unset))
		{
			b++;
		}
		if (WriteUDeltaSingle(valueA.y, valueB.y, DeltaSerializerOption.Unset))
		{
			b += 2;
		}
		if (b != 0 || option != DeltaSerializerOption.Unset)
		{
			InsertUInt8Unpacked(b, position);
			return true;
		}
		Position = position;
		return false;
	}

	[DefaultDeltaWriter]
	public bool WriteDeltaVector3(Vector3 valueA, Vector3 valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		byte b = 0;
		int position = Position;
		Skip(1);
		if (WriteUDeltaSingle(valueA.x, valueB.x, DeltaSerializerOption.Unset))
		{
			b++;
		}
		if (WriteUDeltaSingle(valueA.y, valueB.y, DeltaSerializerOption.Unset))
		{
			b += 2;
		}
		if (WriteUDeltaSingle(valueA.z, valueB.z, DeltaSerializerOption.Unset))
		{
			b += 4;
		}
		if (b != 0 || option != DeltaSerializerOption.Unset)
		{
			InsertUInt8Unpacked(b, position);
			return true;
		}
		Position = position;
		return false;
	}

	public bool WriteDeltaVector3_New(Vector3 valueA, Vector3 valueB, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		UnsignedVector3DeltaFlag unsignedVector3DeltaFlag = UnsignedVector3DeltaFlag.Unset;
		float unsignedDifference;
		UDeltaPrecisionType uDeltaPrecisionType = GetUDeltaPrecisionType(valueA.x, valueB.x, out unsignedDifference);
		float unsignedDifference2;
		UDeltaPrecisionType uDeltaPrecisionType2 = GetUDeltaPrecisionType(valueA.y, valueB.y, out unsignedDifference2);
		float unsignedDifference3;
		UDeltaPrecisionType uDeltaPrecisionType3 = GetUDeltaPrecisionType(valueA.z, valueB.z, out unsignedDifference3);
		byte b = 0;
		bool flag = (uint)uDeltaPrecisionType == b && (int)uDeltaPrecisionType2 > (int)b && (int)uDeltaPrecisionType3 > (int)b;
		if (flag && option == DeltaSerializerOption.Unset)
		{
			return false;
		}
		if (flag && option != DeltaSerializerOption.Unset)
		{
			WriteUInt8Unpacked(0);
			return true;
		}
		int position = Position;
		int num;
		if (uDeltaPrecisionType.FastContains(UDeltaPrecisionType.UInt8) && uDeltaPrecisionType2.FastContains(UDeltaPrecisionType.UInt8))
		{
			num = ((!uDeltaPrecisionType3.FastContains(UDeltaPrecisionType.UInt8)) ? 1 : 0);
			if (num == 0)
			{
				Skip(1);
				goto IL_00b2;
			}
		}
		else
		{
			num = 1;
		}
		Skip(2);
		unsignedVector3DeltaFlag |= UnsignedVector3DeltaFlag.More;
		goto IL_00b2;
		IL_00b2:
		if (uDeltaPrecisionType != 0)
		{
			unsignedVector3DeltaFlag |= GetShiftedFlag(uDeltaPrecisionType, 0);
			WriteDeltaSingle(uDeltaPrecisionType, unsignedDifference, unsigned: true);
		}
		if (uDeltaPrecisionType2 != 0)
		{
			unsignedVector3DeltaFlag |= GetShiftedFlag(uDeltaPrecisionType2, 2);
			WriteDeltaSingle(uDeltaPrecisionType2, unsignedDifference2, unsigned: true);
		}
		if (uDeltaPrecisionType3 != 0)
		{
			unsignedVector3DeltaFlag |= GetShiftedFlag(uDeltaPrecisionType3, 4);
			WriteDeltaSingle(uDeltaPrecisionType3, unsignedDifference3, unsigned: true);
		}
		if (num != 0)
		{
			UnsignedVector3DeltaFlag num2 = unsignedVector3DeltaFlag;
			int num3 = (int)(num2 & (UnsignedVector3DeltaFlag)255);
			InsertUInt8Unpacked((byte)num3, position);
			int num4 = (int)num2 >> 8;
			InsertUInt8Unpacked((byte)num4, position + 1);
		}
		else
		{
			InsertUInt8Unpacked((byte)unsignedVector3DeltaFlag, position);
		}
		return true;
		static UnsignedVector3DeltaFlag GetShiftedFlag(UDeltaPrecisionType precisionType, int shift)
		{
			int num5 = (precisionType.FastContains(UDeltaPrecisionType.UInt8) ? (2 << shift) : ((!precisionType.FastContains(UDeltaPrecisionType.UInt16)) ? (512 << shift) : (256 << shift)));
			if (precisionType.FastContains(UDeltaPrecisionType.NextValueIsLarger))
			{
				num5 |= 4 << shift;
			}
			return (UnsignedVector3DeltaFlag)num5;
		}
	}

	internal void WriteDeltaReconcile<T>(T lastReconcile, T value, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		WriteDelta(lastReconcile, value, option);
	}

	internal void WriteDeltaReplicate<T>(List<T> values, int offset, DeltaSerializerOption option = DeltaSerializerOption.Unset) where T : IReplicateData
	{
		int count = values.Count;
		byte b = (byte)(count - offset);
		WriteUInt8Unpacked(b);
		T prev = ((option == DeltaSerializerOption.FullSerialize || count <= b) ? default(T) : values[offset - 1]);
		for (int i = offset; i < count; i++)
		{
			T val = values[i];
			WriteDelta(prev, val, option);
			prev = val;
			option = DeltaSerializerOption.RootSerialize;
		}
	}

	internal void WriteDeltaReplicate<T>(BasicQueue<T> values, int redundancyCount, DeltaSerializerOption option = DeltaSerializerOption.Unset) where T : IReplicateData
	{
		int count = values.Count;
		byte b = (byte)redundancyCount;
		WriteUInt8Unpacked(b);
		int num = count - redundancyCount;
		T prev = ((option == DeltaSerializerOption.FullSerialize || count <= b) ? default(T) : values[num - 1]);
		for (int i = num; i < count; i++)
		{
			T val = values[i];
			WriteDelta(prev, val, option);
			prev = val;
			option = DeltaSerializerOption.RootSerialize;
		}
	}

	public bool WriteDelta<T>(T prev, T next, DeltaSerializerOption option = DeltaSerializerOption.Unset)
	{
		Func<Writer, T, T, DeltaSerializerOption, bool> write = GenericDeltaWriter<T>.Write;
		if (write == null)
		{
			NetworkManager.LogError("Write delta method not found for " + typeof(T).FullName + ". Use a supported type or create a custom serializer.");
			return false;
		}
		return write(this, prev, next, option);
	}

	public override string ToString()
	{
		return ToString(0, Length);
	}

	public string ToString(int offset, int length)
	{
		return $"Position: {Position:0000}, Length: {Length:0000}, Buffer: {BitConverter.ToString(_buffer, offset, length)}.";
	}

	[Obsolete("Use Clear(NetworkManager) instead.")]
	public void Reset(NetworkManager newManager = null)
	{
		Clear(newManager);
	}

	public void Clear()
	{
		Length = 0;
		Position = 0;
	}

	public void Clear(NetworkManager newManager)
	{
		Clear();
		NetworkManager = newManager;
	}

	public void EnsureBufferCapacity(int count)
	{
		if (Capacity < count)
		{
			Array.Resize(ref _buffer, count);
		}
	}

	public void EnsureBufferLength(int count)
	{
		if (Position + count > _buffer.Length)
		{
			int newSize = _buffer.Length * 2 + count;
			Array.Resize(ref _buffer, newSize);
		}
	}

	public byte[] GetBuffer()
	{
		return _buffer;
	}

	public ArraySegment<byte> GetArraySegment()
	{
		return new ArraySegment<byte>(_buffer, 0, Length);
	}

	[Obsolete("Use Skip.")]
	public void Reserve(int count)
	{
		Skip(count);
	}

	public void Skip(int count)
	{
		EnsureBufferLength(count);
		Position += count;
		Length = System.Math.Max(Length, Position);
	}

	internal void Remove(int count)
	{
		Position -= count;
		Length -= count;
	}

	internal void WritePacketIdUnpacked(PacketId pid)
	{
		WriteUInt16Unpacked((ushort)pid);
	}

	internal void InsertPacketIdUnpacked(PacketId packetId, int index)
	{
		ushort value = (ushort)packetId;
		InsertUInt16Unpacked(value, index);
	}

	[Obsolete("Use InsertUInt8Unpacked.")]
	public void FastInsertUInt8Unpacked(byte value, int index)
	{
		InsertUInt8Unpacked(value, index);
	}

	public void InsertUInt8Unpacked(byte value, int index)
	{
		_buffer[index] = value;
	}

	public void InsertUInt16Unpacked(ushort value, int index)
	{
		_buffer[index++] = (byte)value;
		_buffer[index] = (byte)(value >> 8);
	}

	public void InsertInt32Unpacked(int value, int index)
	{
		InsertUInt32Unpacked((uint)value, index);
	}

	public void InsertUInt32Unpacked(uint value, int index)
	{
		_buffer[index++] = (byte)value;
		_buffer[index++] = (byte)(value >> 8);
		_buffer[index++] = (byte)(value >> 16);
		_buffer[index] = (byte)(value >> 24);
	}

	[Obsolete("Use WriteUInt8Unpacked.")]
	public void WriteByte(byte value)
	{
		WriteUInt8Unpacked(value);
	}

	[DefaultWriter]
	public void WriteUInt8Unpacked(byte value)
	{
		EnsureBufferLength(1);
		_buffer[Position++] = value;
		Length = System.Math.Max(Length, Position);
	}

	[Obsolete("Use WriteUInt8Array.")]
	public void WriteBytes(byte[] value, int offset, int count)
	{
		WriteUInt8Array(value, offset, count);
	}

	public void WriteUInt8Array(byte[] value, int offset, int count)
	{
		EnsureBufferLength(count);
		Buffer.BlockCopy(value, offset, _buffer, Position, count);
		Position += count;
		Length = System.Math.Max(Length, Position);
	}

	[Obsolete("Use WriteUInt8ArrayAndSize.")]
	public void WriteBytesAndSize(byte[] value, int offset, int count)
	{
		WriteUInt8ArrayAndSize(value, offset, count);
	}

	public void WriteUInt8ArrayAndSize(byte[] value, int offset, int count)
	{
		if (value == null)
		{
			WriteInt32(-1);
			return;
		}
		WriteInt32(count);
		WriteUInt8Array(value, offset, count);
	}

	[Obsolete("Use WriteUInt8ArrayAndSize.")]
	public void WriteBytesAndSize(byte[] value)
	{
		WriteUInt8ArrayAndSize(value);
	}

	public void WriteUInt8ArrayAndSize(byte[] value)
	{
		int count = ((value != null) ? value.Length : 0);
		WriteUInt8ArrayAndSize(value, 0, count);
	}

	[Obsolete("Use WriteInt8Unpacked.")]
	public void WriteSByte(sbyte value)
	{
		WriteInt8Unpacked(value);
	}

	[DefaultWriter]
	public void WriteInt8Unpacked(sbyte value)
	{
		WriteUInt8Unpacked((byte)value);
	}

	[DefaultWriter]
	public void WriteChar(char value)
	{
		EnsureBufferLength(2);
		_buffer[Position++] = (byte)value;
		_buffer[Position++] = (byte)((int)value >> 8);
		Length = System.Math.Max(Length, Position);
	}

	[DefaultWriter]
	public void WriteBoolean(bool value)
	{
		EnsureBufferLength(1);
		_buffer[Position++] = (byte)(value ? 1 : 0);
		Length = System.Math.Max(Length, Position);
	}

	public void WriteUInt16Unpacked(ushort value)
	{
		EnsureBufferLength(2);
		_buffer[Position++] = (byte)value;
		_buffer[Position++] = (byte)(value >> 8);
		Length = System.Math.Max(Length, Position);
	}

	[DefaultWriter]
	public void WriteUInt16(ushort value)
	{
		WriteUInt16Unpacked(value);
	}

	public void WriteInt16Unpacked(short value)
	{
		WriteUInt16Unpacked((ushort)value);
	}

	[DefaultWriter]
	public void WriteInt16(short value)
	{
		WriteUInt16Unpacked((ushort)value);
	}

	public void WriteInt32Unpacked(int value)
	{
		WriteUInt32Unpacked((uint)value);
	}

	[DefaultWriter]
	public void WriteInt32(int value)
	{
		WriteSignedPackedWhole(value);
	}

	internal static void WriteUInt32Unpacked(byte[] dst, uint value, ref int position)
	{
		dst[position++] = (byte)value;
		dst[position++] = (byte)(value >> 8);
		dst[position++] = (byte)(value >> 16);
		dst[position++] = (byte)(value >> 24);
	}

	public void WriteUInt32Unpacked(uint value)
	{
		EnsureBufferLength(4);
		WriteUInt32Unpacked(_buffer, value, ref Position);
		Length = System.Math.Max(Length, Position);
	}

	[DefaultWriter]
	public void WriteUInt32(uint value)
	{
		WriteUnsignedPackedWhole(value);
	}

	public void WriteUInt64Unpacked(ulong value)
	{
		EnsureBufferLength(8);
		_buffer[Position++] = (byte)value;
		_buffer[Position++] = (byte)(value >> 8);
		_buffer[Position++] = (byte)(value >> 16);
		_buffer[Position++] = (byte)(value >> 24);
		_buffer[Position++] = (byte)(value >> 32);
		_buffer[Position++] = (byte)(value >> 40);
		_buffer[Position++] = (byte)(value >> 48);
		_buffer[Position++] = (byte)(value >> 56);
		Length = System.Math.Max(Position, Length);
	}

	[DefaultWriter]
	public void WriteUInt64(ulong value)
	{
		WriteUnsignedPackedWhole(value);
	}

	public void WriteInt64Unpacked(long value)
	{
		WriteUInt64((ulong)value);
	}

	[DefaultWriter]
	public void WriteInt64(long value)
	{
		WriteSignedPackedWhole(value);
	}

	public void WriteSingleUnpacked(float value)
	{
		EnsureBufferLength(4);
		UIntFloat uIntFloat = default(UIntFloat);
		uIntFloat.FloatValue = value;
		UIntFloat uIntFloat2 = uIntFloat;
		WriteUInt32Unpacked(uIntFloat2.UIntValue);
	}

	[DefaultWriter]
	public void WriteSingle(float value)
	{
		WriteSingleUnpacked(value);
	}

	public void WriteDoubleUnpacked(double value)
	{
		UIntDouble uIntDouble = default(UIntDouble);
		uIntDouble.DoubleValue = value;
		UIntDouble uIntDouble2 = uIntDouble;
		WriteUInt64Unpacked(uIntDouble2.LongValue);
	}

	[DefaultWriter]
	public void WriteDouble(double value)
	{
		WriteDoubleUnpacked(value);
	}

	public void WriteDecimalUnpacked(decimal value)
	{
		UIntDecimal uIntDecimal = default(UIntDecimal);
		uIntDecimal.DecimalValue = value;
		UIntDecimal uIntDecimal2 = uIntDecimal;
		WriteUInt64Unpacked(uIntDecimal2.LongValue1);
		WriteUInt64Unpacked(uIntDecimal2.LongValue2);
	}

	[DefaultWriter]
	public void WriteDecimal(decimal value)
	{
		WriteDecimalUnpacked(value);
	}

	[DefaultWriter]
	public void WriteString(string value)
	{
		if (value == null)
		{
			WriteInt32(-1);
			return;
		}
		byte[] buffer = Strings.Buffer;
		int num = value.ToBytes(ref buffer);
		WriteInt32(num);
		if (num != 0)
		{
			WriteUInt8Array(buffer, 0, num);
		}
	}

	[DefaultWriter]
	public void WriteArraySegmentAndSize(ArraySegment<byte> value)
	{
		WriteUInt8ArrayAndSize(value.Array, value.Offset, value.Count);
	}

	public void WriteArraySegment(ArraySegment<byte> value)
	{
		WriteUInt8Array(value.Array, value.Offset, value.Count);
	}

	public void WriteVector2Unpacked(Vector2 value)
	{
		WriteSingleUnpacked(value.x);
		WriteSingleUnpacked(value.y);
	}

	[DefaultWriter]
	public void WriteVector2(Vector2 value)
	{
		WriteVector2Unpacked(value);
	}

	public void WriteVector3Unpacked(Vector3 value)
	{
		WriteSingleUnpacked(value.x);
		WriteSingleUnpacked(value.y);
		WriteSingleUnpacked(value.z);
	}

	[DefaultWriter]
	public void WriteVector3(Vector3 value)
	{
		WriteVector3Unpacked(value);
	}

	public void WriteVector4Unpacked(Vector4 value)
	{
		WriteSingleUnpacked(value.x);
		WriteSingleUnpacked(value.y);
		WriteSingleUnpacked(value.z);
		WriteSingleUnpacked(value.w);
	}

	[DefaultWriter]
	public void WriteVector4(Vector4 value)
	{
		WriteVector4Unpacked(value);
	}

	public void WriteVector2IntUnpacked(Vector2Int value)
	{
		WriteInt32Unpacked(value.x);
		WriteInt32Unpacked(value.y);
	}

	[DefaultWriter]
	public void WriteVector2Int(Vector2Int value)
	{
		WriteSignedPackedWhole(value.x);
		WriteSignedPackedWhole(value.y);
	}

	public void WriteVector3IntUnpacked(Vector3Int value)
	{
		WriteInt32Unpacked(value.x);
		WriteInt32Unpacked(value.y);
		WriteInt32Unpacked(value.z);
	}

	[DefaultWriter]
	public void WriteVector3Int(Vector3Int value)
	{
		WriteSignedPackedWhole(value.x);
		WriteSignedPackedWhole(value.y);
		WriteSignedPackedWhole(value.z);
	}

	public void WriteColorUnpacked(Color value)
	{
		WriteSingleUnpacked(value.r);
		WriteSingleUnpacked(value.g);
		WriteSingleUnpacked(value.b);
		WriteSingleUnpacked(value.a);
	}

	[DefaultWriter]
	public void WriteColor(Color value)
	{
		EnsureBufferLength(4);
		_buffer[Position++] = (byte)(value.r * 100f);
		_buffer[Position++] = (byte)(value.g * 100f);
		_buffer[Position++] = (byte)(value.b * 100f);
		_buffer[Position++] = (byte)(value.a * 100f);
		Length = System.Math.Max(Length, Position);
	}

	[DefaultWriter]
	public void WriteColor32(Color32 value)
	{
		EnsureBufferLength(4);
		_buffer[Position++] = value.r;
		_buffer[Position++] = value.g;
		_buffer[Position++] = value.b;
		_buffer[Position++] = value.a;
		Length = System.Math.Max(Length, Position);
	}

	public void WriteQuaternionUnpacked(Quaternion value)
	{
		WriteSingleUnpacked(value.x);
		WriteSingleUnpacked(value.y);
		WriteSingleUnpacked(value.z);
		WriteSingleUnpacked(value.w);
	}

	public void WriteQuaternion64(Quaternion value)
	{
		ulong value2 = Quaternion64Compression.Compress(value);
		WriteUInt64Unpacked(value2);
	}

	[DefaultWriter]
	public void WriteQuaternion32(Quaternion value)
	{
		Quaternion32Compression.Compress(this, value);
	}

	internal void WriteQuaternion(Quaternion value, AutoPackType autoPackType)
	{
		switch (autoPackType)
		{
		case AutoPackType.Packed:
			WriteQuaternion32(value);
			break;
		case AutoPackType.PackedLess:
			WriteQuaternion64(value);
			break;
		default:
			WriteQuaternionUnpacked(value);
			break;
		}
	}

	public void WriteRectUnpacked(Rect value)
	{
		WriteSingleUnpacked(value.xMin);
		WriteSingleUnpacked(value.yMin);
		WriteSingleUnpacked(value.width);
		WriteSingleUnpacked(value.height);
	}

	[DefaultWriter]
	public void WriteRect(Rect value)
	{
		WriteRectUnpacked(value);
	}

	public void WritePlaneUnpacked(UnityEngine.Plane value)
	{
		WriteVector3Unpacked(value.normal);
		WriteSingleUnpacked(value.distance);
	}

	[DefaultWriter]
	public void WritePlane(UnityEngine.Plane value)
	{
		WritePlaneUnpacked(value);
	}

	public void WriteRayUnpacked(Ray value)
	{
		WriteVector3Unpacked(value.origin);
		WriteVector3Unpacked(value.direction);
	}

	[DefaultWriter]
	public void WriteRay(Ray value)
	{
		WriteRayUnpacked(value);
	}

	public void WriteRay2DUnpacked(Ray2D value)
	{
		WriteVector2Unpacked(value.origin);
		WriteVector2Unpacked(value.direction);
	}

	[DefaultWriter]
	public void WriteRay2D(Ray2D value)
	{
		WriteRay2DUnpacked(value);
	}

	public void WriteMatrix4x4Unpacked(Matrix4x4 value)
	{
		WriteSingleUnpacked(value.m00);
		WriteSingleUnpacked(value.m01);
		WriteSingleUnpacked(value.m02);
		WriteSingleUnpacked(value.m03);
		WriteSingleUnpacked(value.m10);
		WriteSingleUnpacked(value.m11);
		WriteSingleUnpacked(value.m12);
		WriteSingleUnpacked(value.m13);
		WriteSingleUnpacked(value.m20);
		WriteSingleUnpacked(value.m21);
		WriteSingleUnpacked(value.m22);
		WriteSingleUnpacked(value.m23);
		WriteSingleUnpacked(value.m30);
		WriteSingleUnpacked(value.m31);
		WriteSingleUnpacked(value.m32);
		WriteSingleUnpacked(value.m33);
	}

	[DefaultWriter]
	public void WriteMatrix4x4(Matrix4x4 value)
	{
		WriteMatrix4x4Unpacked(value);
	}

	[DefaultWriter]
	public void WriteGuidAllocated(Guid value)
	{
		byte[] array = value.ToByteArray();
		WriteUInt8Array(array, 0, array.Length);
	}

	public void WriteTickUnpacked(uint value)
	{
		WriteUInt32Unpacked(value);
	}

	[DefaultWriter]
	public void WriteGameObject(GameObject go)
	{
		NetworkObject component;
		NetworkBehaviour component2;
		if (go == null)
		{
			WriteUInt8Unpacked(0);
		}
		else if (go.TryGetComponent<NetworkObject>(out component))
		{
			WriteUInt8Unpacked(1);
			WriteNetworkObject(component);
		}
		else if (go.TryGetComponent<NetworkBehaviour>(out component2))
		{
			WriteUInt8Unpacked(2);
			WriteNetworkBehaviour(component2);
		}
		else
		{
			WriteUInt8Unpacked(0);
			NetworkManager.LogError("GameObject " + go.name + " cannot be serialized because it does not have a NetworkObject nor NetworkBehaviour.");
		}
	}

	[DefaultWriter]
	public void WriteTransform(Transform t)
	{
		if (t == null)
		{
			WriteNetworkObject(null);
			return;
		}
		NetworkObject component = t.GetComponent<NetworkObject>();
		WriteNetworkObject(component);
	}

	public void WriteNetworkObjectId(NetworkObject nob)
	{
		int objectId = ((nob == null) ? 65535 : nob.ObjectId);
		WriteNetworkObjectId(objectId);
	}

	[DefaultWriter]
	public void WriteNetworkObject(NetworkObject nob)
	{
		if (nob == null)
		{
			WriteNetworkObjectId(65535);
			return;
		}
		bool isSpawned = nob.IsSpawned;
		if (isSpawned)
		{
			WriteNetworkObjectId(nob.ObjectId);
		}
		else
		{
			WriteNetworkObjectId(nob.PrefabId);
		}
		WriteBoolean(isSpawned);
	}

	internal void WriteSpawnedNetworkObject(NetworkObject nob)
	{
		WriteNetworkObjectId(nob.ObjectId);
		WriteUInt16(nob.SpawnableCollectionId);
		WriteInt32(nob.GetInitializeOrder());
	}

	internal void WriteNetworkObjectForDespawn(NetworkObject nob, DespawnType dt)
	{
		WriteNetworkObjectId(nob.ObjectId);
		WriteUInt8Unpacked((byte)dt);
	}

	public void WriteNetworkObjectId(int objectId)
	{
		WriteSignedPackedWhole(objectId);
	}

	[DefaultWriter]
	public void WriteNetworkBehaviour(NetworkBehaviour nb)
	{
		if (nb == null)
		{
			WriteNetworkObject(null);
			WriteUInt8Unpacked(0);
		}
		else
		{
			WriteNetworkObject(nb.NetworkObject);
			WriteUInt8Unpacked(nb.ComponentIndex);
		}
	}

	public void WriteNetworkBehaviourId(NetworkBehaviour nb)
	{
		if (nb == null)
		{
			WriteUInt8Unpacked(byte.MaxValue);
		}
		else
		{
			WriteUInt8Unpacked(nb.ComponentIndex);
		}
	}

	[DefaultWriter]
	public void WriteDateTime(DateTime dt)
	{
		WriteSignedPackedWhole(dt.ToBinary());
	}

	[DefaultWriter]
	public void WriteChannel(Channel channel)
	{
		WriteUInt8Unpacked((byte)channel);
	}

	[DefaultWriter]
	public void WriteLayerMask(LayerMask value)
	{
		WriteSignedPackedWhole(value.value);
	}

	[DefaultWriter]
	public void WriteNetworkConnection(NetworkConnection connection)
	{
		int id = ((connection == null) ? (-1) : connection.ClientId);
		WriteNetworkConnectionId(id);
	}

	[DefaultWriter]
	public void WriteTransformProperties(TransformProperties value)
	{
		WriteVector3(value.Position);
		WriteQuaternion32(value.Rotation);
		WriteVector3(value.Scale);
	}

	public void WriteNetworkConnectionId(int id)
	{
		WriteSignedPackedWhole(id);
	}

	public void WriteDictionary<TKey, TValue>(Dictionary<TKey, TValue> dict)
	{
		if (dict == null)
		{
			WriteSignedPackedWhole(-1L);
			return;
		}
		WriteSignedPackedWhole(dict.Count);
		foreach (KeyValuePair<TKey, TValue> item in dict)
		{
			Write(item.Key);
			Write(item.Value);
		}
	}

	internal void WriteStateUpdatePacket(uint lastPacketTick)
	{
		WriteTickUnpacked(lastPacketTick);
	}

	public ulong ZigZagEncode(ulong value)
	{
		if (value >> 63 != 0)
		{
			return ~(value << 1) | 1;
		}
		return value << 1;
	}

	public void WriteSignedPackedWhole(long value)
	{
		WriteUnsignedPackedWhole(ZigZagEncode((ulong)value));
	}

	public void WriteUnsignedPackedWhole(ulong value)
	{
		EnsureBufferLength(9);
		while (value > 127)
		{
			_buffer[Position++] = (byte)((value & 0x7F) | 0x80);
			value >>= 7;
		}
		_buffer[Position++] = (byte)(value & 0x7F);
		Length = System.Math.Max(Length, Position);
	}

	public void WriteList<T>(List<T> value, int offset, int count)
	{
		if (value == null)
		{
			WriteSignedPackedWhole(-1L);
			return;
		}
		if (offset + count > value.Count)
		{
			count = 0;
		}
		WriteSignedPackedWhole(count);
		for (int i = 0; i < count; i++)
		{
			Write(value[i + offset]);
		}
	}

	public void WriteList<T>(List<T> value, int offset)
	{
		int num = value?.Count ?? 0;
		WriteList(value, offset, num - offset);
	}

	public void WriteList<T>(List<T> value)
	{
		int count = value?.Count ?? 0;
		WriteList(value, 0, count);
	}

	public void WriteHashSet<T>(HashSet<T> value)
	{
		if (value == null)
		{
			WriteSignedPackedWhole(-1L);
			return;
		}
		WriteSignedPackedWhole(value.Count);
		foreach (T item in value)
		{
			Write(item);
		}
	}

	public void WriteArray<T>(T[] value)
	{
		int count = ((value != null) ? value.Length : 0);
		WriteArray(value, 0, count);
	}

	public void WriteArray<T>(T[] value, int offset)
	{
		int num = ((value != null) ? value.Length : 0);
		WriteArray(value, offset, num - offset);
	}

	public void WriteArray<T>(T[] value, int offset, int count)
	{
		if (value == null)
		{
			WriteSignedPackedWhole(-1L);
			return;
		}
		if (value.Length == 0 || offset >= count)
		{
			WriteSignedPackedWhole(0L);
			return;
		}
		WriteSignedPackedWhole(count);
		for (int i = offset; i < count; i++)
		{
			Write(value[i]);
		}
	}

	internal void WriteReconcile<T>(T data)
	{
		Write(data);
	}

	internal void WriteReplicate<T>(RingBuffer<ReplicateDataContainer<T>> values, int offset) where T : IReplicateData, new()
	{
		int count = values.Count;
		byte value = (byte)(count - offset);
		WriteUInt8Unpacked(value);
		for (int i = offset; i < count; i++)
		{
			WriteReplicateDataContainer(values[i]);
		}
	}

	internal void WriteReplicate<T>(BasicQueue<ReplicateDataContainer<T>> values, int redundancyCount) where T : IReplicateData, new()
	{
		int count = values.Count;
		byte value = (byte)redundancyCount;
		WriteUInt8Unpacked(value);
		for (int i = count - redundancyCount; i < count; i++)
		{
			WriteReplicateDataContainer(values[i]);
		}
	}

	private void WriteReplicateDataContainer<T>(ReplicateDataContainer<T> value) where T : IReplicateData, new()
	{
		Write(value.Data);
		WriteChannel(value.Channel);
	}

	public void Write<T>(T value)
	{
		Action<Writer, T> write = GenericWriter<T>.Write;
		if (write == null)
		{
			NetworkManager.LogError("Write method not found for " + typeof(T).FullName + ". Use a supported type or create a custom serializer.");
		}
		else
		{
			write(this, value);
		}
	}
}
