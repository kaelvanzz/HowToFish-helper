using System;
using System.Collections.Generic;
using System.Text;
using FishNet.CodeGenerating;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Serializing.Helping;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;

namespace FishNet.Serializing;

public class Reader
{
	public enum DataSource
	{
		Unset,
		Server,
		Client
	}

	public DataSource Source;

	public NetworkManager NetworkManager;

	public int Position;

	private byte[] _buffer;

	private static readonly UTF8Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

	private static readonly byte[] _guidBuffer = new byte[16];

	internal double DOUBLE_ACCURACY => 1000.0;

	internal decimal DECIMAL_ACCURACY => 1000m;

	public int Capacity => _buffer.Length;

	public int Offset { get; private set; }

	public int Length { get; private set; }

	public int Remaining => Length + Offset - Position;

	public NetworkConnection NetworkConnection { get; private set; }

	[DefaultDeltaReader]
	public bool ReadDeltaBoolean(bool valueA)
	{
		return !valueA;
	}

	[DefaultDeltaReader]
	public sbyte ReadDeltaInt8(sbyte valueA)
	{
		return (sbyte)ReadDifference8_16_32(valueA);
	}

	[DefaultDeltaReader]
	public byte ReadDeltaUInt8(byte valueA)
	{
		return (byte)ReadDifference8_16_32(valueA);
	}

	[DefaultDeltaReader]
	public short ReadDeltaInt16(short valueA)
	{
		return (short)ReadDifference8_16_32(valueA);
	}

	[DefaultDeltaReader]
	public ushort ReadDeltaUInt16(ushort valueA)
	{
		return (ushort)ReadDifference8_16_32(valueA);
	}

	[DefaultDeltaReader]
	public int ReadDeltaInt32(int valueA)
	{
		return (int)ReadDifference8_16_32(valueA);
	}

	[DefaultDeltaReader]
	public uint ReadDeltaUInt32(uint valueA)
	{
		return (uint)ReadDifference8_16_32(valueA);
	}

	[DefaultDeltaReader]
	public long ReadDeltaInt64(long valueA)
	{
		return (long)ReadDeltaUInt64((ulong)valueA);
	}

	[DefaultDeltaReader]
	public ulong ReadDeltaUInt64(ulong valueA)
	{
		bool num = ReadBoolean();
		ulong num2 = ReadUnsignedPackedWhole();
		if (!num)
		{
			return valueA - num2;
		}
		return valueA + num2;
	}

	[DefaultDeltaReader]
	private long ReadDifference8_16_32(long valueA)
	{
		long num = ReadSignedPackedWhole();
		return valueA + num;
	}

	public float ReadDeltaSingle(UDeltaPrecisionType dpt, bool unsigned)
	{
		if (dpt.FastContains(UDeltaPrecisionType.UInt8))
		{
			if (unsigned)
			{
				return (float)(int)ReadUInt8Unpacked() / (float)DOUBLE_ACCURACY;
			}
			return (float)ReadInt8Unpacked() / (float)DOUBLE_ACCURACY;
		}
		if (dpt.FastContains(UDeltaPrecisionType.UInt16))
		{
			if (unsigned)
			{
				return (float)(int)ReadUInt16Unpacked() / (float)DOUBLE_ACCURACY;
			}
			return (float)ReadInt16Unpacked() / (float)DOUBLE_ACCURACY;
		}
		return ReadSingleUnpacked();
	}

	public float ReadDeltaSingle(UDeltaPrecisionType dpt, float valueA, bool unsigned)
	{
		float num = ReadDeltaSingle(dpt, unsigned);
		if (unsigned)
		{
			if (!dpt.FastContains(UDeltaPrecisionType.NextValueIsLarger))
			{
				return valueA - num;
			}
			return valueA + num;
		}
		return valueA + num;
	}

	public float ReadDeltaSingle(float valueA)
	{
		UDeltaPrecisionType dpt = (UDeltaPrecisionType)ReadUInt8Unpacked();
		return ReadDeltaSingle(dpt, valueA, unsigned: false);
	}

	[DefaultDeltaReader]
	public float ReadUDeltaSingle(float valueA)
	{
		UDeltaPrecisionType dpt = (UDeltaPrecisionType)ReadUInt8Unpacked();
		return ReadDeltaSingle(dpt, valueA, unsigned: true);
	}

	public double ReadDeltaDouble(UDeltaPrecisionType dpt, bool unsigned)
	{
		if (dpt.FastContains(UDeltaPrecisionType.UInt8))
		{
			if (unsigned)
			{
				return (double)(int)ReadUInt8Unpacked() / DOUBLE_ACCURACY;
			}
			return (double)ReadInt8Unpacked() / DOUBLE_ACCURACY;
		}
		if (dpt.FastContains(UDeltaPrecisionType.UInt16))
		{
			if (unsigned)
			{
				return (double)(int)ReadUInt16Unpacked() / DOUBLE_ACCURACY;
			}
			return (double)ReadInt16Unpacked() / DOUBLE_ACCURACY;
		}
		if (dpt.FastContains(UDeltaPrecisionType.UInt32))
		{
			if (unsigned)
			{
				return (double)ReadUInt32Unpacked() / DOUBLE_ACCURACY;
			}
			return (double)ReadInt32Unpacked() / DOUBLE_ACCURACY;
		}
		if (dpt.FastContains(UDeltaPrecisionType.Unset))
		{
			return ReadDoubleUnpacked();
		}
		NetworkManager.LogError($"Unhandled precision type of {dpt}.");
		return 0.0;
	}

	public double ReadDeltaDouble(UDeltaPrecisionType dpt, double valueA, bool unsigned)
	{
		double num = ReadDeltaDouble(dpt, unsigned);
		if (unsigned)
		{
			if (!dpt.FastContains(UDeltaPrecisionType.NextValueIsLarger))
			{
				return valueA - num;
			}
			return valueA + num;
		}
		return valueA + num;
	}

	public double ReadDeltaDouble(double valueA)
	{
		UDeltaPrecisionType dpt = (UDeltaPrecisionType)ReadUInt8Unpacked();
		return ReadDeltaDouble(dpt, valueA, unsigned: false);
	}

	[DefaultDeltaReader]
	public double ReadUDeltaDouble(double valueA)
	{
		UDeltaPrecisionType dpt = (UDeltaPrecisionType)ReadUInt8Unpacked();
		return ReadDeltaDouble(dpt, valueA, unsigned: true);
	}

	public decimal ReadDeltaDecimal(UDeltaPrecisionType dpt, bool unsigned)
	{
		if (dpt.FastContains(UDeltaPrecisionType.UInt8))
		{
			if (unsigned)
			{
				return (decimal)ReadUInt8Unpacked() / DECIMAL_ACCURACY;
			}
			return (decimal)ReadInt8Unpacked() / DECIMAL_ACCURACY;
		}
		if (dpt.FastContains(UDeltaPrecisionType.UInt16))
		{
			if (unsigned)
			{
				return (decimal)ReadUInt16Unpacked() / DECIMAL_ACCURACY;
			}
			return (decimal)ReadInt16Unpacked() / DECIMAL_ACCURACY;
		}
		if (dpt.FastContains(UDeltaPrecisionType.UInt32))
		{
			if (unsigned)
			{
				return (decimal)ReadUInt32Unpacked() / DECIMAL_ACCURACY;
			}
			return (decimal)ReadInt32Unpacked() / DECIMAL_ACCURACY;
		}
		if (dpt.FastContains(UDeltaPrecisionType.UInt64))
		{
			if (unsigned)
			{
				return (decimal)ReadUInt64Unpacked() / DECIMAL_ACCURACY;
			}
			return (decimal)ReadInt64Unpacked() / DECIMAL_ACCURACY;
		}
		if (dpt.FastContains(UDeltaPrecisionType.Unset))
		{
			return ReadDecimalUnpacked();
		}
		NetworkManager.LogError($"Unhandled precision type of {dpt}.");
		return 0m;
	}

	public decimal ReadDeltaDecimal(UDeltaPrecisionType dpt, decimal valueA, bool unsigned)
	{
		decimal num = ReadDeltaDecimal(dpt, unsigned);
		if (unsigned)
		{
			if (!dpt.FastContains(UDeltaPrecisionType.NextValueIsLarger))
			{
				return valueA - num;
			}
			return valueA + num;
		}
		return valueA + num;
	}

	[DefaultDeltaReader]
	public decimal ReadDeltaDecimal(decimal valueA)
	{
		UDeltaPrecisionType dpt = (UDeltaPrecisionType)ReadUInt8Unpacked();
		return ReadDeltaDecimal(dpt, valueA, unsigned: false);
	}

	[DefaultDeltaReader]
	public decimal ReadUDeltaDecimal(decimal valueA)
	{
		UDeltaPrecisionType dpt = (UDeltaPrecisionType)ReadUInt8Unpacked();
		return ReadDeltaDecimal(dpt, valueA, unsigned: true);
	}

	[DefaultDeltaReader]
	public NetworkBehaviour WriteDeltaNetworkBehaviour(NetworkBehaviour valueA)
	{
		return ReadNetworkBehaviour();
	}

	[DefaultDeltaReader]
	public Quaternion ReadDeltaQuaternion(Quaternion valueA, float precision = 0.0001f)
	{
		return QuaternionDeltaPrecisionCompression.Decompress(this, valueA, precision);
	}

	[DefaultDeltaReader]
	public Vector2 ReadDeltaVector2(Vector2 valueA)
	{
		byte num = ReadUInt8Unpacked();
		if ((num & 1) == 1)
		{
			valueA.x = ReadUDeltaSingle(valueA.x);
		}
		if ((num & 2) == 2)
		{
			valueA.y = ReadUDeltaSingle(valueA.y);
		}
		return valueA;
	}

	[DefaultDeltaReader]
	public Vector3 ReadDeltaVector3(Vector3 valueA)
	{
		byte num = ReadUInt8Unpacked();
		if ((num & 1) == 1)
		{
			valueA.x = ReadUDeltaSingle(valueA.x);
		}
		if ((num & 2) == 2)
		{
			valueA.y = ReadUDeltaSingle(valueA.y);
		}
		if ((num & 4) == 4)
		{
			valueA.z = ReadUDeltaSingle(valueA.z);
		}
		return valueA;
	}

	internal T ReadDeltaReconcile<T>(T lastReconcile)
	{
		return ReadDelta(lastReconcile);
	}

	internal int ReadDeltaReplicate<T>(T lastReadReplicate, ref T[] collection, uint tick) where T : IReplicateData
	{
		_ = Remaining;
		int num = ReadUInt8Unpacked();
		if (collection == null || collection.Length < num)
		{
			collection = new T[num];
		}
		tick -= (uint)(num - 1);
		uint tick2 = lastReadReplicate.GetTick();
		T prev = lastReadReplicate;
		for (int i = 0; i < num; i++)
		{
			uint num2 = tick + (uint)i;
			if (num2 <= tick2)
			{
				ReadDelta(prev);
				continue;
			}
			T val = ReadDelta(prev);
			val.SetTick(num2);
			collection[i] = val;
			prev = val;
		}
		return num;
	}

	public T ReadDelta<T>(T prev)
	{
		Func<Reader, T, T> read = GenericDeltaReader<T>.Read;
		if (read == null)
		{
			NetworkManager.LogError("Read delta method not found for " + typeof(T).FullName + ". Use a supported type or create a custom serializer.");
			return default(T);
		}
		return read(this, prev);
	}

	public Reader()
	{
	}

	public Reader(byte[] bytes, NetworkManager networkManager, NetworkConnection networkConnection = null, DataSource source = DataSource.Unset)
	{
		Initialize(bytes, networkManager, networkConnection, source);
	}

	public Reader(ArraySegment<byte> segment, NetworkManager networkManager, NetworkConnection networkConnection = null, DataSource source = DataSource.Unset)
	{
		Initialize(segment, networkManager, networkConnection, source);
	}

	public override string ToString()
	{
		return ToString(0, Length);
	}

	public string ToString(int offset, int length)
	{
		return $"Position: {Position:0000}, Length: {Length:0000}, Buffer: {BitConverter.ToString(_buffer, offset, length)}.";
	}

	public string RemainingToString()
	{
		string arg = ((Remaining > 0) ? BitConverter.ToString(_buffer, Position, Remaining) : "null");
		return $"Remaining: {Remaining}, Length: {Length}, Buffer: {arg}.";
	}

	public ArraySegment<byte> GetRemainingData()
	{
		if (Remaining == 0)
		{
			return default(ArraySegment<byte>);
		}
		return new ArraySegment<byte>(_buffer, Position, Remaining);
	}

	public void Initialize(ArraySegment<byte> segment, NetworkManager networkManager, DataSource source = DataSource.Unset)
	{
		Initialize(segment, networkManager, null, source);
	}

	public void Initialize(ArraySegment<byte> segment, NetworkManager networkManager, NetworkConnection networkConnection = null, DataSource source = DataSource.Unset)
	{
		_buffer = segment.Array;
		if (_buffer == null)
		{
			_buffer = new byte[0];
		}
		Position = segment.Offset;
		Offset = segment.Offset;
		Length = segment.Count;
		NetworkManager = networkManager;
		NetworkConnection = networkConnection;
		Source = source;
	}

	public void Initialize(byte[] bytes, NetworkManager networkManager, DataSource source = DataSource.Unset)
	{
		Initialize(new ArraySegment<byte>(bytes), networkManager, null, source);
	}

	public void Initialize(byte[] bytes, NetworkManager networkManager, NetworkConnection networkConnection = null, DataSource source = DataSource.Unset)
	{
		Initialize(new ArraySegment<byte>(bytes), networkManager, networkConnection, source);
	}

	internal int ReadLength()
	{
		return ReadInt32();
	}

	internal PacketId ReadPacketId()
	{
		return (PacketId)ReadUInt16Unpacked();
	}

	internal PacketId PeekPacketId()
	{
		int position = Position;
		PacketId result = ReadPacketId();
		Position = position;
		return result;
	}

	internal byte PeekUInt8()
	{
		return _buffer[Position];
	}

	public void Skip(int value)
	{
		if (value >= 1 && Remaining >= value)
		{
			Position += value;
		}
	}

	public void Clear()
	{
		if (Remaining > 0)
		{
			Skip(Remaining);
		}
	}

	public ArraySegment<byte> GetArraySegmentBuffer()
	{
		return new ArraySegment<byte>(_buffer, Offset, Length);
	}

	[Obsolete("Use GetBuffer.")]
	public byte[] GetByteBuffer()
	{
		return GetBuffer();
	}

	public byte[] GetBuffer()
	{
		return _buffer;
	}

	[Obsolete("Use GetBufferAllocated().")]
	public byte[] GetByteBufferAllocated()
	{
		return GetBufferAllocated();
	}

	[Obsolete("Use GetBufferAllocated().")]
	public byte[] GetBufferAllocated()
	{
		byte[] array = new byte[Length];
		Buffer.BlockCopy(_buffer, Offset, array, 0, Length);
		return array;
	}

	public void BlockCopy(ref byte[] target, int targetOffset, int count)
	{
		Buffer.BlockCopy(_buffer, Position, target, targetOffset, count);
		Position += count;
	}

	[Obsolete("Use ReadUInt8Unpacked.")]
	public byte ReadByte()
	{
		return ReadUInt8Unpacked();
	}

	[DefaultReader]
	public byte ReadUInt8Unpacked()
	{
		byte result = _buffer[Position];
		Position++;
		return result;
	}

	[Obsolete("Use ReadUInt8ArrayAllocated.")]
	public byte[] ReadBytesAllocated(int count)
	{
		return ReadUInt8ArrayAllocated(count);
	}

	[Obsolete("Use ReadUInt8Array.")]
	public void ReadBytes(ref byte[] buffer, int count)
	{
		ReadUInt8Array(ref buffer, count);
	}

	public void ReadUInt8Array(ref byte[] buffer, int count)
	{
		if (buffer == null)
		{
			NetworkManager.LogError("Buffer cannot be null.");
		}
		else if (count > buffer.Length)
		{
			NetworkManager.LogError($"Count of {count} exceeds target length of {buffer.Length}.");
		}
		else
		{
			BlockCopy(ref buffer, 0, count);
		}
	}

	public ArraySegment<byte> ReadArraySegment(int count)
	{
		if (count < 0)
		{
			NetworkManager.Log("ArraySegment count cannot be less than 0.");
			Position += Remaining;
			return default(ArraySegment<byte>);
		}
		ArraySegment<byte> result = new ArraySegment<byte>(_buffer, Position, count);
		Position += count;
		return result;
	}

	[Obsolete("Use ReadInt8Unpacked.")]
	public sbyte ReadSByte()
	{
		return ReadInt8Unpacked();
	}

	[DefaultReader]
	public sbyte ReadInt8Unpacked()
	{
		return (sbyte)ReadUInt8Unpacked();
	}

	[DefaultReader]
	public char ReadChar()
	{
		return (char)ReadUInt16();
	}

	[DefaultReader]
	public bool ReadBoolean()
	{
		if (ReadUInt8Unpacked() != 1)
		{
			return false;
		}
		return true;
	}

	public ushort ReadUInt16Unpacked()
	{
		return (ushort)((ushort)(0u | _buffer[Position++]) | (ushort)(_buffer[Position++] << 8));
	}

	[DefaultReader]
	public ushort ReadUInt16()
	{
		return ReadUInt16Unpacked();
	}

	public short ReadInt16Unpacked()
	{
		return (short)ReadUInt16Unpacked();
	}

	[DefaultReader]
	public short ReadInt16()
	{
		return (short)ReadUInt16Unpacked();
	}

	public uint ReadUInt32Unpacked()
	{
		return 0u | _buffer[Position++] | (uint)(_buffer[Position++] << 8) | (uint)(_buffer[Position++] << 16) | (uint)(_buffer[Position++] << 24);
	}

	[DefaultReader]
	public uint ReadUInt32()
	{
		return (uint)ReadUnsignedPackedWhole();
	}

	public int ReadInt32Unpacked()
	{
		return (int)ReadUInt32Unpacked();
	}

	[DefaultReader]
	public int ReadInt32()
	{
		return (int)ReadSignedPackedWhole();
	}

	public long ReadInt64Unpacked()
	{
		return (long)ReadUInt64Unpacked();
	}

	[DefaultReader]
	public long ReadInt64()
	{
		return ReadSignedPackedWhole();
	}

	public ulong ReadUInt64Unpacked()
	{
		return 0uL | (ulong)_buffer[Position++] | ((ulong)_buffer[Position++] << 8) | ((ulong)_buffer[Position++] << 16) | ((ulong)_buffer[Position++] << 24) | ((ulong)_buffer[Position++] << 32) | ((ulong)_buffer[Position++] << 40) | ((ulong)_buffer[Position++] << 48) | ((ulong)_buffer[Position++] << 56);
	}

	[DefaultReader]
	public ulong ReadUInt64()
	{
		return ReadUnsignedPackedWhole();
	}

	public float ReadSingleUnpacked()
	{
		UIntFloat uIntFloat = default(UIntFloat);
		uIntFloat.UIntValue = ReadUInt32Unpacked();
		return uIntFloat.FloatValue;
	}

	[DefaultReader]
	public float ReadSingle()
	{
		return ReadSingleUnpacked();
	}

	public double ReadDoubleUnpacked()
	{
		UIntDouble uIntDouble = default(UIntDouble);
		uIntDouble.LongValue = ReadUInt64Unpacked();
		return uIntDouble.DoubleValue;
	}

	[DefaultReader]
	public double ReadDouble()
	{
		return ReadDoubleUnpacked();
	}

	public decimal ReadDecimalUnpacked()
	{
		UIntDecimal uIntDecimal = default(UIntDecimal);
		uIntDecimal.LongValue1 = ReadUInt64Unpacked();
		uIntDecimal.LongValue2 = ReadUInt64Unpacked();
		return uIntDecimal.DecimalValue;
	}

	[DefaultReader]
	public decimal ReadDecimal()
	{
		return ReadDecimalUnpacked();
	}

	[Obsolete("use ReadStringAllocated.")]
	public string ReadString()
	{
		return ReadStringAllocated();
	}

	[DefaultReader]
	public string ReadStringAllocated()
	{
		int num = ReadInt32();
		switch (num)
		{
		case -1:
			return null;
		case 0:
			return string.Empty;
		default:
		{
			if (!CheckAllocationAttack(num))
			{
				return string.Empty;
			}
			ArraySegment<byte> arraySegment = ReadArraySegment(num);
			return arraySegment.Array.ToString(arraySegment.Offset, arraySegment.Count);
		}
		}
	}

	[Obsolete("Use ReadUInt8ArrayAndSizeAllocated.")]
	public byte[] ReadBytesAndSizeAllocated()
	{
		return ReadUInt8ArrayAndSizeAllocated();
	}

	[DefaultReader]
	public byte[] ReadUInt8ArrayAndSizeAllocated()
	{
		int num = ReadInt32();
		if (num == -1)
		{
			return null;
		}
		return ReadUInt8ArrayAllocated(num);
	}

	[Obsolete("Use ReadUInt8ArrayAndSize.")]
	public int ReadBytesAndSize(ref byte[] target)
	{
		return ReadUInt8ArrayAndSize(ref target);
	}

	public int ReadUInt8ArrayAndSize(ref byte[] target)
	{
		int num = ReadInt32();
		if (num > 0)
		{
			ReadUInt8Array(ref target, num);
		}
		return num;
	}

	[DefaultReader]
	public ArraySegment<byte> ReadArraySegmentAndSize()
	{
		int num = ReadInt32();
		if (num == -1)
		{
			return default(ArraySegment<byte>);
		}
		return ReadArraySegment(num);
	}

	public Vector2 ReadVector2Unpacked()
	{
		return new Vector2(ReadSingleUnpacked(), ReadSingleUnpacked());
	}

	[DefaultReader]
	public Vector2 ReadVector2()
	{
		return ReadVector2Unpacked();
	}

	public Vector3 ReadVector3Unpacked()
	{
		return new Vector3(ReadSingleUnpacked(), ReadSingleUnpacked(), ReadSingleUnpacked());
	}

	[DefaultReader]
	public Vector3 ReadVector3()
	{
		return ReadVector3Unpacked();
	}

	public Vector4 ReadVector4Unpacked()
	{
		return new Vector4(ReadSingleUnpacked(), ReadSingleUnpacked(), ReadSingleUnpacked(), ReadSingleUnpacked());
	}

	[DefaultReader]
	public Vector4 ReadVector4()
	{
		return ReadVector4Unpacked();
	}

	public Vector2Int ReadVector2IntUnpacked()
	{
		return new Vector2Int(ReadInt32Unpacked(), ReadInt32Unpacked());
	}

	[DefaultReader]
	public Vector2Int ReadVector2Int()
	{
		return new Vector2Int((int)ReadSignedPackedWhole(), (int)ReadSignedPackedWhole());
	}

	public Vector3Int ReadVector3IntUnpacked()
	{
		return new Vector3Int(ReadInt32Unpacked(), ReadInt32Unpacked(), ReadInt32Unpacked());
	}

	[DefaultReader]
	public Vector3Int ReadVector3Int()
	{
		return new Vector3Int((int)ReadSignedPackedWhole(), (int)ReadSignedPackedWhole(), (int)ReadSignedPackedWhole());
	}

	public Color ReadColorUnpacked()
	{
		float r = ReadSingleUnpacked();
		float g = ReadSingleUnpacked();
		float b = ReadSingleUnpacked();
		float a = ReadSingleUnpacked();
		return new Color(r, g, b, a);
	}

	[DefaultReader]
	public Color ReadColor()
	{
		float r = (float)(int)ReadUInt8Unpacked() / 100f;
		float g = (float)(int)ReadUInt8Unpacked() / 100f;
		float b = (float)(int)ReadUInt8Unpacked() / 100f;
		float a = (float)(int)ReadUInt8Unpacked() / 100f;
		return new Color(r, g, b, a);
	}

	[DefaultReader]
	public Color32 ReadColor32()
	{
		return new Color32(ReadUInt8Unpacked(), ReadUInt8Unpacked(), ReadUInt8Unpacked(), ReadUInt8Unpacked());
	}

	public Quaternion ReadQuaternionUnpacked()
	{
		return new Quaternion(ReadSingleUnpacked(), ReadSingleUnpacked(), ReadSingleUnpacked(), ReadSingleUnpacked());
	}

	public Quaternion ReadQuaternion64()
	{
		return Quaternion64Compression.Decompress(ReadUInt64Unpacked());
	}

	[DefaultReader]
	public Quaternion ReadQuaternion32()
	{
		return Quaternion32Compression.Decompress(this);
	}

	internal Quaternion ReadQuaternion(AutoPackType autoPackType)
	{
		return autoPackType switch
		{
			AutoPackType.Packed => ReadQuaternion32(), 
			AutoPackType.PackedLess => ReadQuaternion64(), 
			_ => ReadQuaternionUnpacked(), 
		};
	}

	public Rect ReadRectUnpacked()
	{
		return new Rect(ReadSingleUnpacked(), ReadSingleUnpacked(), ReadSingleUnpacked(), ReadSingleUnpacked());
	}

	[DefaultReader]
	public Rect ReadRect()
	{
		return ReadRectUnpacked();
	}

	public UnityEngine.Plane ReadPlaneUnpacked()
	{
		return new UnityEngine.Plane(ReadVector3Unpacked(), ReadSingleUnpacked());
	}

	[DefaultReader]
	public UnityEngine.Plane ReadPlane()
	{
		return ReadPlaneUnpacked();
	}

	public Ray ReadRayUnpacked()
	{
		Vector3 origin = ReadVector3Unpacked();
		Vector3 direction = ReadVector3Unpacked();
		return new Ray(origin, direction);
	}

	[DefaultReader]
	public Ray ReadRay()
	{
		return ReadRayUnpacked();
	}

	public Ray2D ReadRay2DUnpacked()
	{
		Vector3 vector = ReadVector2Unpacked();
		return new Ray2D(direction: ReadVector2Unpacked(), origin: (Vector2)vector);
	}

	[DefaultReader]
	public Ray2D ReadRay2D()
	{
		return ReadRay2DUnpacked();
	}

	public Matrix4x4 ReadMatrix4x4Unpacked()
	{
		Matrix4x4 result = default(Matrix4x4);
		result.m00 = ReadSingleUnpacked();
		result.m01 = ReadSingleUnpacked();
		result.m02 = ReadSingleUnpacked();
		result.m03 = ReadSingleUnpacked();
		result.m10 = ReadSingleUnpacked();
		result.m11 = ReadSingleUnpacked();
		result.m12 = ReadSingleUnpacked();
		result.m13 = ReadSingleUnpacked();
		result.m20 = ReadSingleUnpacked();
		result.m21 = ReadSingleUnpacked();
		result.m22 = ReadSingleUnpacked();
		result.m23 = ReadSingleUnpacked();
		result.m30 = ReadSingleUnpacked();
		result.m31 = ReadSingleUnpacked();
		result.m32 = ReadSingleUnpacked();
		result.m33 = ReadSingleUnpacked();
		return result;
	}

	[DefaultReader]
	public Matrix4x4 ReadMatrix4x4()
	{
		return ReadMatrix4x4Unpacked();
	}

	public byte[] ReadUInt8ArrayAllocated(int count)
	{
		if (count < 0)
		{
			NetworkManager.Log("Bytes count cannot be less than 0.");
			Position += Remaining;
			return null;
		}
		byte[] buffer = new byte[count];
		ReadUInt8Array(ref buffer, count);
		return buffer;
	}

	[DefaultReader]
	public Guid ReadGuid()
	{
		byte[] buffer = _guidBuffer;
		ReadUInt8Array(ref buffer, 16);
		return new Guid(buffer);
	}

	public uint ReadTickUnpacked()
	{
		return ReadUInt32Unpacked();
	}

	[DefaultReader]
	public GameObject ReadGameObject()
	{
		byte b = ReadUInt8Unpacked();
		GameObject result;
		switch (b)
		{
		case 0:
			result = null;
			break;
		case 1:
		{
			NetworkObject networkObject = ReadNetworkObject();
			result = ((networkObject == null) ? null : networkObject.gameObject);
			break;
		}
		case 2:
		{
			NetworkBehaviour networkBehaviour = ReadNetworkBehaviour();
			result = ((networkBehaviour == null) ? null : networkBehaviour.gameObject);
			break;
		}
		default:
			result = null;
			NetworkManager.LogError($"Unhandled ReadGameObject type of {b}.");
			break;
		}
		return result;
	}

	[DefaultReader]
	public Transform ReadTransform()
	{
		NetworkObject networkObject = ReadNetworkObject();
		if (!(networkObject == null))
		{
			return networkObject.transform;
		}
		return null;
	}

	[DefaultReader]
	public NetworkObject ReadNetworkObject()
	{
		int objectOrPrefabId;
		return ReadNetworkObject(out objectOrPrefabId);
	}

	public NetworkObject ReadNetworkObject(bool logException)
	{
		int objectOrPrefabId;
		return ReadNetworkObject(out objectOrPrefabId, null, logException);
	}

	public NetworkObject ReadNetworkObject(out int objectOrPrefabId, HashSet<int> readSpawningObjects = null, bool logException = true)
	{
		objectOrPrefabId = ReadNetworkObjectId();
		if (objectOrPrefabId == 65535)
		{
			return null;
		}
		bool flag = ReadBoolean();
		bool started = NetworkManager.ServerManager.Started;
		bool started2 = NetworkManager.ClientManager.Started;
		NetworkObject value;
		if (flag)
		{
			value = null;
			if (started2)
			{
				NetworkManager.ClientManager.Objects.Spawned.TryGetValueIL2CPP(objectOrPrefabId, out value);
			}
			if (value == null && started)
			{
				NetworkManager.ServerManager.Objects.Spawned.TryGetValueIL2CPP(objectOrPrefabId, out value);
			}
			if (value == null && !started && logException && (readSpawningObjects == null || !readSpawningObjects.Contains(objectOrPrefabId)))
			{
				NetworkManager.LogWarning($"Spawned NetworkObject was expected to exist but does not for Id {objectOrPrefabId}. This may occur if you sent a NetworkObject reference which does not exist, be it destroyed or if the client does not have visibility.");
			}
		}
		else
		{
			bool asServer = !started2;
			value = NetworkManager.GetPrefab(objectOrPrefabId, asServer);
		}
		return value;
	}

	public int ReadNetworkObjectId()
	{
		return (int)ReadSignedPackedWhole();
	}

	internal int ReadNetworkObjectForSpawn(out int initializeOrder, out ushort collectionid)
	{
		int result = ReadNetworkObjectId();
		collectionid = ReadUInt16();
		initializeOrder = ReadInt32();
		return result;
	}

	internal int ReadNetworkObjectForDespawn(out DespawnType dt)
	{
		int result = ReadNetworkObjectId();
		dt = (DespawnType)ReadUInt8Unpacked();
		return result;
	}

	internal byte ReadNetworkBehaviourId(out int objectId)
	{
		objectId = ReadNetworkObjectId();
		if (objectId != 65535)
		{
			return ReadUInt8Unpacked();
		}
		return 0;
	}

	public NetworkBehaviour ReadNetworkBehaviour(out int objectId, out byte componentIndex, HashSet<int> readSpawningObjects = null, bool logException = true)
	{
		NetworkObject networkObject = ReadNetworkObject(out objectId, readSpawningObjects, logException);
		componentIndex = ReadUInt8Unpacked();
		if (networkObject == null)
		{
			return null;
		}
		if (componentIndex >= networkObject.NetworkBehaviours.Count)
		{
			NetworkManager.LogError($"ComponentIndex of {componentIndex} is out of bounds on {networkObject.gameObject.name} [id {networkObject.ObjectId}]. This may occur if you have modified your gameObject/prefab without saving it, or the scene.");
			return null;
		}
		return networkObject.NetworkBehaviours[componentIndex];
	}

	[DefaultReader]
	public NetworkBehaviour ReadNetworkBehaviour()
	{
		int objectId;
		byte componentIndex;
		return ReadNetworkBehaviour(out objectId, out componentIndex);
	}

	public NetworkBehaviour ReadNetworkBehaviour(bool logException)
	{
		int objectId;
		byte componentIndex;
		return ReadNetworkBehaviour(out objectId, out componentIndex, null, logException);
	}

	public byte ReadNetworkBehaviourId()
	{
		return ReadUInt8Unpacked();
	}

	[DefaultReader]
	public DateTime ReadDateTime()
	{
		return DateTime.FromBinary(ReadSignedPackedWhole());
	}

	[DefaultReader]
	public Channel ReadChannel()
	{
		return (Channel)ReadUInt8Unpacked();
	}

	public int ReadNetworkConnectionId()
	{
		return (int)ReadSignedPackedWhole();
	}

	[DefaultReader]
	public LayerMask ReadLayerMask()
	{
		return (int)ReadSignedPackedWhole();
	}

	[DefaultReader]
	public NetworkConnection ReadNetworkConnection()
	{
		int num = ReadNetworkConnectionId();
		if (num == -1)
		{
			return NetworkManager.EmptyConnection;
		}
		if (NetworkManager.IsServerStarted)
		{
			if (NetworkManager.ServerManager.Clients.TryGetValueIL2CPP(num, out var value))
			{
				return value;
			}
			if (NetworkManager.IsClientStarted)
			{
				if (NetworkManager.ClientManager.Clients.TryGetValueIL2CPP(num, out value))
				{
					return value;
				}
				return new NetworkConnection(NetworkManager, num, -1, asServer: true);
			}
			NetworkManager.LogWarning($"Unable to find connection for read Id {num}. An empty connection will be returned.");
			return NetworkManager.EmptyConnection;
		}
		if (num == NetworkManager.ClientManager.Connection.ClientId)
		{
			return NetworkManager.ClientManager.Connection;
		}
		if (NetworkManager.ClientManager.Clients.TryGetValueIL2CPP(num, out var value2))
		{
			return value2;
		}
		return new NetworkConnection(NetworkManager, num, -1, asServer: true);
	}

	[DefaultReader]
	public TransformProperties ReadTransformProperties()
	{
		Vector3 position = ReadVector3();
		Quaternion rotation = ReadQuaternion32();
		Vector3 localScale = ReadVector3();
		return new TransformProperties(position, rotation, localScale);
	}

	private bool CheckAllocationAttack(int size)
	{
		if (size != -1 && size < 0)
		{
			NetworkManager.LogError($"Size of {size} is invalid.");
			return false;
		}
		if (size > Remaining)
		{
			NetworkManager.LogError($"Read size of {size} is larger than remaining data of {Remaining}.");
			return false;
		}
		return true;
	}

	internal void ReadStateUpdatePacket(out uint clientTick)
	{
		clientTick = ReadTickUnpacked();
	}

	public ulong ZigZagDecode(ulong value)
	{
		ulong num = value << 63;
		if (num != 0)
		{
			return ~(value >> 1) | num;
		}
		return value >> 1;
	}

	public long ReadSignedPackedWhole()
	{
		return (long)ZigZagDecode(ReadUnsignedPackedWhole());
	}

	public ulong ReadUnsignedPackedWhole()
	{
		int num = 0;
		ulong num2 = 0uL;
		int num3 = 10;
		int i = 0;
		int num4 = GetBuffer().Length;
		for (; i < num3; i++)
		{
			if (Position >= num4)
			{
				NetworkManager.LogError($"Read position of {Position} is beyond reader's buffer length of {num4}.");
				return 0uL;
			}
			byte b = _buffer[Position++];
			num2 |= (ulong)((long)(b & 0x7F) << num);
			if ((b & 0x80) == 0)
			{
				break;
			}
			num += 7;
		}
		return num2;
	}

	internal T ReadReconcile<T>()
	{
		return Read<T>();
	}

	internal List<ReplicateDataContainer<T>> ReadReplicate<T>(uint tick) where T : IReplicateData, new()
	{
		List<ReplicateDataContainer<T>> list = CollectionCaches<ReplicateDataContainer<T>>.RetrieveList();
		int num = ReadUInt8Unpacked();
		if (num <= 0)
		{
			NetworkManager.Log("Replicate count cannot be 0 or less.");
			Position += Remaining;
			return list;
		}
		tick -= (uint)(num - 1);
		for (int i = 0; i < num; i++)
		{
			ReplicateDataContainer<T> item = ReadReplicateData<T>(tick + (uint)i);
			list.Add(item);
		}
		return list;
	}

	private ReplicateDataContainer<T> ReadReplicateData<T>(uint tick) where T : IReplicateData, new()
	{
		T data = Read<T>();
		Channel channel = ReadChannel();
		return new ReplicateDataContainer<T>(data, channel, tick, isCreated: true);
	}

	public Dictionary<TKey, TValue> ReadDictionary<TKey, TValue>()
	{
		int num = (int)ReadSignedPackedWhole();
		if (num == -1)
		{
			return null;
		}
		Dictionary<TKey, TValue> dictionary = CollectionCaches<TKey, TValue>.RetrieveDictionary();
		ReadDictionary(num, dictionary);
		return dictionary;
	}

	[Obsolete("Use ReadDictionary.")]
	public Dictionary<TKey, TValue> ReadDictionaryAllocated<TKey, TValue>()
	{
		return ReadDictionary<TKey, TValue>();
	}

	public int ReadDictionary<TKey, TValue>(ref Dictionary<TKey, TValue> collection, bool allowNullification = false)
	{
		int num = (int)ReadSignedPackedWhole();
		if (num == -1)
		{
			if (allowNullification)
			{
				collection = null;
			}
			return num;
		}
		ReadDictionary(num, collection);
		return num;
	}

	private void ReadDictionary<TKey, TValue>(int count, Dictionary<TKey, TValue> collection)
	{
		if (count < 0)
		{
			NetworkManager.LogError("Collection count cannot be less than 0.");
			Position += Remaining;
			return;
		}
		if (collection == null)
		{
			collection = new Dictionary<TKey, TValue>(count);
		}
		else
		{
			collection.Clear();
		}
		for (int i = 0; i < count; i++)
		{
			TKey key = Read<TKey>();
			TValue value = Read<TValue>();
			collection.Add(key, value);
		}
	}

	public List<T> ReadList<T>()
	{
		int num = (int)ReadSignedPackedWhole();
		if (num == -1)
		{
			return null;
		}
		List<T> collection = CollectionCaches<T>.RetrieveList();
		ReadList(num, ref collection);
		return collection;
	}

	[Obsolete("Use ReadList.")]
	public List<T> ReadListAllocated<T>()
	{
		return ReadList<T>();
	}

	public int ReadList<T>(ref List<T> collection, bool allowNullification = false)
	{
		int num = (int)ReadSignedPackedWhole();
		if (num == -1)
		{
			if (allowNullification)
			{
				collection = null;
			}
			return num;
		}
		ReadList(num, ref collection);
		return num;
	}

	private void ReadList<T>(int count, ref List<T> collection)
	{
		if (count < 0)
		{
			NetworkManager.LogError("List count cannot be less than 0.");
			Position += Remaining;
			return;
		}
		if (collection == null)
		{
			collection = new List<T>(count);
		}
		else
		{
			collection.Clear();
		}
		for (int i = 0; i < count; i++)
		{
			collection.Add(Read<T>());
		}
	}

	public HashSet<T> ReadHashSet<T>()
	{
		int num = (int)ReadSignedPackedWhole();
		if (num == -1)
		{
			return null;
		}
		HashSet<T> collection = CollectionCaches<T>.RetrieveHashSet();
		ReadHashSet(num, ref collection);
		return collection;
	}

	public int HashSet<T>(ref HashSet<T> collection, bool allowNullification = false)
	{
		int num = (int)ReadSignedPackedWhole();
		if (num == -1)
		{
			if (allowNullification)
			{
				collection = null;
			}
			return num;
		}
		ReadHashSet(num, ref collection);
		return num;
	}

	private void ReadHashSet<T>(int count, ref HashSet<T> collection)
	{
		if (count < 0)
		{
			NetworkManager.LogError("List count cannot be less than 0.");
			Position += Remaining;
			return;
		}
		if (collection == null)
		{
			collection = new HashSet<T>(count);
		}
		else
		{
			collection.Clear();
		}
		for (int i = 0; i < count; i++)
		{
			collection.Add(Read<T>());
		}
	}

	public T[] ReadArrayAllocated<T>()
	{
		T[] collection = null;
		ReadArray(ref collection);
		return collection;
	}

	public int ReadArray<T>(ref T[] collection)
	{
		int num = (int)ReadSignedPackedWhole();
		if (num == -1)
		{
			return 0;
		}
		if (num == 0)
		{
			if (collection == null)
			{
				collection = new T[0];
			}
			return 0;
		}
		if (num < 0)
		{
			NetworkManager.Log("Array count cannot be less than 0.");
			Position += Remaining;
			return 0;
		}
		if (collection == null)
		{
			collection = new T[num];
		}
		else if (collection.Length < num)
		{
			Array.Resize(ref collection, num);
		}
		for (int i = 0; i < num; i++)
		{
			collection[i] = Read<T>();
		}
		return num;
	}

	public T Read<T>()
	{
		Func<Reader, T> read = GenericReader<T>.Read;
		if (read == null)
		{
			NetworkManager.LogError("Read method not found for " + typeof(T).FullName + ". Use a supported type or create a custom serializer.");
			return default(T);
		}
		return read(this);
	}

	public SubStream ReadSubStream()
	{
		int num = ReadInt32();
		if (num == -1)
		{
			return SubStream.GetUninitialized();
		}
		return SubStream.CreateFromReader(this, num);
	}

	public bool2 Readbool2()
	{
		byte b = ReadUInt8Unpacked();
		bool2 result = default(bool2);
		result.x = (b & 1) != 0;
		result.y = (b & 2) != 0;
		return result;
	}

	public bool3 Readbool3()
	{
		byte b = ReadUInt8Unpacked();
		bool3 result = default(bool3);
		result.x = (b & 1) != 0;
		result.y = (b & 2) != 0;
		result.z = (b & 4) != 0;
		return result;
	}

	public bool4 Readbool4()
	{
		byte b = ReadUInt8Unpacked();
		bool4 result = default(bool4);
		result.x = (b & 1) != 0;
		result.y = (b & 2) != 0;
		result.z = (b & 4) != 0;
		result.w = (b & 8) != 0;
		return result;
	}

	public bool2x2 Readbool2x2()
	{
		byte b = ReadUInt8Unpacked();
		bool2x2 result = default(bool2x2);
		result.c0.x = (b & 1) != 0;
		result.c0.y = (b & 2) != 0;
		result.c1.x = (b & 4) != 0;
		result.c1.y = (b & 8) != 0;
		return result;
	}

	public bool2x3 Readbool2x3()
	{
		byte b = ReadUInt8Unpacked();
		bool2x3 result = default(bool2x3);
		result.c0.x = (b & 1) != 0;
		result.c0.y = (b & 2) != 0;
		result.c1.x = (b & 4) != 0;
		result.c1.y = (b & 8) != 0;
		result.c2.x = (b & 0x10) != 0;
		result.c2.y = (b & 0x20) != 0;
		return result;
	}

	public bool2x4 Readbool2x4()
	{
		byte b = ReadUInt8Unpacked();
		bool2x4 result = default(bool2x4);
		result.c0.x = (b & 1) != 0;
		result.c0.y = (b & 2) != 0;
		result.c1.x = (b & 4) != 0;
		result.c1.y = (b & 8) != 0;
		result.c2.x = (b & 0x10) != 0;
		result.c2.y = (b & 0x20) != 0;
		result.c3.x = (b & 0x40) != 0;
		result.c3.y = (b & 0x80) != 0;
		return result;
	}

	public bool3x2 Readbool3x2()
	{
		byte b = ReadUInt8Unpacked();
		bool3x2 result = default(bool3x2);
		result.c0.x = (b & 1) != 0;
		result.c0.y = (b & 2) != 0;
		result.c0.z = (b & 4) != 0;
		result.c1.x = (b & 8) != 0;
		result.c1.y = (b & 0x10) != 0;
		result.c1.z = (b & 0x20) != 0;
		return result;
	}

	public bool3x3 Readbool3x3()
	{
		ushort num = ReadUInt16();
		bool3x3 result = default(bool3x3);
		result.c0.x = (num & 1) != 0;
		result.c0.y = (num & 2) != 0;
		result.c0.z = (num & 4) != 0;
		result.c1.x = (num & 8) != 0;
		result.c1.y = (num & 0x10) != 0;
		result.c1.z = (num & 0x20) != 0;
		result.c2.x = (num & 0x40) != 0;
		result.c2.y = (num & 0x80) != 0;
		result.c2.z = (num & 0x100) != 0;
		return result;
	}

	public bool3x4 Readbool3x4()
	{
		ushort num = ReadUInt16();
		bool3x4 result = default(bool3x4);
		result.c0.x = (num & 1) != 0;
		result.c0.y = (num & 2) != 0;
		result.c0.z = (num & 4) != 0;
		result.c1.x = (num & 8) != 0;
		result.c1.y = (num & 0x10) != 0;
		result.c1.z = (num & 0x20) != 0;
		result.c2.x = (num & 0x40) != 0;
		result.c2.y = (num & 0x80) != 0;
		result.c2.z = (num & 0x100) != 0;
		result.c3.x = (num & 0x200) != 0;
		result.c3.y = (num & 0x400) != 0;
		result.c3.z = (num & 0x800) != 0;
		return result;
	}

	public bool4x2 Readbool4x2()
	{
		byte b = ReadUInt8Unpacked();
		bool4x2 result = default(bool4x2);
		result.c0.x = (b & 1) != 0;
		result.c0.y = (b & 2) != 0;
		result.c0.z = (b & 4) != 0;
		result.c0.w = (b & 8) != 0;
		result.c1.x = (b & 0x10) != 0;
		result.c1.y = (b & 0x20) != 0;
		result.c1.z = (b & 0x40) != 0;
		result.c1.w = (b & 0x80) != 0;
		return result;
	}

	public bool4x3 Readbool4x3()
	{
		ushort num = ReadUInt16();
		bool4x3 result = default(bool4x3);
		result.c0.x = (num & 1) != 0;
		result.c0.y = (num & 2) != 0;
		result.c0.z = (num & 4) != 0;
		result.c0.w = (num & 8) != 0;
		result.c1.x = (num & 0x10) != 0;
		result.c1.y = (num & 0x20) != 0;
		result.c1.z = (num & 0x40) != 0;
		result.c1.w = (num & 0x80) != 0;
		result.c2.x = (num & 0x100) != 0;
		result.c2.y = (num & 0x200) != 0;
		result.c2.z = (num & 0x400) != 0;
		result.c2.w = (num & 0x800) != 0;
		return result;
	}

	public bool4x4 Readbool4x4()
	{
		ushort num = ReadUInt16();
		bool4x4 result = default(bool4x4);
		result.c0.x = (num & 1) != 0;
		result.c0.y = (num & 2) != 0;
		result.c0.z = (num & 4) != 0;
		result.c0.w = (num & 8) != 0;
		result.c1.x = (num & 0x10) != 0;
		result.c1.y = (num & 0x20) != 0;
		result.c1.z = (num & 0x40) != 0;
		result.c1.w = (num & 0x80) != 0;
		result.c2.x = (num & 0x100) != 0;
		result.c2.y = (num & 0x200) != 0;
		result.c2.z = (num & 0x400) != 0;
		result.c2.w = (num & 0x800) != 0;
		result.c3.x = (num & 0x1000) != 0;
		result.c3.y = (num & 0x2000) != 0;
		result.c3.z = (num & 0x4000) != 0;
		result.c3.w = (num & 0x8000) != 0;
		return result;
	}

	public double2 Readdouble2()
	{
		double2 result = default(double2);
		result.x = ReadDouble();
		result.y = ReadDouble();
		return result;
	}

	public double3 Readdouble3()
	{
		double3 result = default(double3);
		result.x = ReadDouble();
		result.y = ReadDouble();
		result.z = ReadDouble();
		return result;
	}

	public double4 Readdouble4()
	{
		double4 result = default(double4);
		result.x = ReadDouble();
		result.y = ReadDouble();
		result.z = ReadDouble();
		result.w = ReadDouble();
		return result;
	}

	public double2x2 Readdouble2x2()
	{
		double2x2 result = default(double2x2);
		result.c0 = Readdouble2();
		result.c1 = Readdouble2();
		return result;
	}

	public double2x3 Readdouble2x3()
	{
		double2x3 result = default(double2x3);
		result.c0 = Readdouble2();
		result.c1 = Readdouble2();
		result.c2 = Readdouble2();
		return result;
	}

	public double2x4 Readdouble2x4()
	{
		double2x4 result = default(double2x4);
		result.c0 = Readdouble2();
		result.c1 = Readdouble2();
		result.c2 = Readdouble2();
		result.c3 = Readdouble2();
		return result;
	}

	public double3x2 Readdouble3x2()
	{
		double3x2 result = default(double3x2);
		result.c0 = Readdouble3();
		result.c1 = Readdouble3();
		return result;
	}

	public double4x2 Readdouble4x2()
	{
		double4x2 result = default(double4x2);
		result.c0 = Readdouble4();
		result.c1 = Readdouble4();
		return result;
	}

	public double3x4 Readdouble3x4()
	{
		double3x4 result = default(double3x4);
		result.c0 = Readdouble3();
		result.c1 = Readdouble3();
		result.c2 = Readdouble3();
		result.c3 = Readdouble3();
		return result;
	}

	public double4x3 Readdouble4x3()
	{
		double4x3 result = default(double4x3);
		result.c0 = Readdouble4();
		result.c1 = Readdouble4();
		result.c2 = Readdouble4();
		return result;
	}

	public double3x3 Readdouble3x3()
	{
		double3x3 result = default(double3x3);
		result.c0 = Readdouble3();
		result.c1 = Readdouble3();
		result.c2 = Readdouble3();
		return result;
	}

	public double4x4 Readdouble4x4()
	{
		double4x4 result = default(double4x4);
		result.c0 = Readdouble4();
		result.c1 = Readdouble4();
		result.c2 = Readdouble4();
		result.c3 = Readdouble4();
		return result;
	}

	public float2 Readfloat2()
	{
		float2 result = default(float2);
		result.x = ReadSingle();
		result.y = ReadSingle();
		return result;
	}

	public float3 Readfloat3()
	{
		float3 result = default(float3);
		result.x = ReadSingle();
		result.y = ReadSingle();
		result.z = ReadSingle();
		return result;
	}

	public float4 Readfloat4()
	{
		float4 result = default(float4);
		result.x = ReadSingle();
		result.y = ReadSingle();
		result.z = ReadSingle();
		result.w = ReadSingle();
		return result;
	}

	public float2x2 Readfloat2x2()
	{
		float2x2 result = default(float2x2);
		result.c0 = Readfloat2();
		result.c1 = Readfloat2();
		return result;
	}

	public float2x3 Readfloat2x3()
	{
		float2x3 result = default(float2x3);
		result.c0 = Readfloat2();
		result.c1 = Readfloat2();
		result.c2 = Readfloat2();
		return result;
	}

	public float2x4 Readfloat2x4()
	{
		float2x4 result = default(float2x4);
		result.c0 = Readfloat2();
		result.c1 = Readfloat2();
		result.c2 = Readfloat2();
		result.c3 = Readfloat2();
		return result;
	}

	public float3x2 Readfloat3x2()
	{
		float3x2 result = default(float3x2);
		result.c0 = Readfloat3();
		result.c1 = Readfloat3();
		return result;
	}

	public float3x3 Readfloat3x3()
	{
		float3x3 result = default(float3x3);
		result.c0 = Readfloat3();
		result.c1 = Readfloat3();
		result.c2 = Readfloat3();
		return result;
	}

	public float3x4 Readfloat3x4()
	{
		float3x4 result = default(float3x4);
		result.c0 = Readfloat3();
		result.c1 = Readfloat3();
		result.c2 = Readfloat3();
		result.c3 = Readfloat3();
		return result;
	}

	public float4x2 Readfloat4x2()
	{
		float4x2 result = default(float4x2);
		result.c0 = Readfloat4();
		result.c1 = Readfloat4();
		return result;
	}

	public float4x3 Readfloat4x3()
	{
		float4x3 result = default(float4x3);
		result.c0 = Readfloat4();
		result.c1 = Readfloat4();
		result.c2 = Readfloat4();
		return result;
	}

	public float4x4 Readfloat4x4()
	{
		float4x4 result = default(float4x4);
		result.c0 = Readfloat4();
		result.c1 = Readfloat4();
		result.c2 = Readfloat4();
		result.c3 = Readfloat4();
		return result;
	}

	public half Readhalf()
	{
		half result = default(half);
		result.value = ReadUInt16();
		return result;
	}

	public half2 Readhalf2()
	{
		half2 result = default(half2);
		result.x.value = ReadUInt16();
		result.y.value = ReadUInt16();
		return result;
	}

	public half3 Readhalf3()
	{
		half3 result = default(half3);
		result.x.value = ReadUInt16();
		result.y.value = ReadUInt16();
		result.z.value = ReadUInt16();
		return result;
	}

	public half4 Readhalf4()
	{
		half4 result = default(half4);
		result.x.value = ReadUInt16();
		result.y.value = ReadUInt16();
		result.z.value = ReadUInt16();
		result.w.value = ReadUInt16();
		return result;
	}

	public int2 Readint2()
	{
		int2 result = default(int2);
		result.x = ReadInt32();
		result.y = ReadInt32();
		return result;
	}

	public int3 Readint3()
	{
		int3 result = default(int3);
		result.x = ReadInt32();
		result.y = ReadInt32();
		result.z = ReadInt32();
		return result;
	}

	public int4 Readint4()
	{
		int4 result = default(int4);
		result.x = ReadInt32();
		result.y = ReadInt32();
		result.z = ReadInt32();
		result.w = ReadInt32();
		return result;
	}

	public int2x2 Readint2x2()
	{
		int2x2 result = default(int2x2);
		result.c0 = Readint2();
		result.c1 = Readint2();
		return result;
	}

	public int2x3 Readint2x3()
	{
		int2x3 result = default(int2x3);
		result.c0 = Readint2();
		result.c1 = Readint2();
		result.c2 = Readint2();
		return result;
	}

	public int2x4 Readint2x4()
	{
		int2x4 result = default(int2x4);
		result.c0 = Readint2();
		result.c1 = Readint2();
		result.c2 = Readint2();
		result.c3 = Readint2();
		return result;
	}

	public int3x2 Readint3x2()
	{
		int3x2 result = default(int3x2);
		result.c0 = Readint3();
		result.c1 = Readint3();
		return result;
	}

	public int3x3 Readint3x3()
	{
		int3x3 result = default(int3x3);
		result.c0 = Readint3();
		result.c1 = Readint3();
		result.c2 = Readint3();
		return result;
	}

	public int3x4 Readint3x4()
	{
		int3x4 result = default(int3x4);
		result.c0 = Readint3();
		result.c1 = Readint3();
		result.c2 = Readint3();
		result.c3 = Readint3();
		return result;
	}

	public int4x2 Readint4x2()
	{
		int4x2 result = default(int4x2);
		result.c0 = Readint4();
		result.c1 = Readint4();
		return result;
	}

	public int4x3 Readint4x3()
	{
		int4x3 result = default(int4x3);
		result.c0 = Readint4();
		result.c1 = Readint4();
		result.c2 = Readint4();
		return result;
	}

	public int4x4 Readint4x4()
	{
		int4x4 result = default(int4x4);
		result.c0 = Readint4();
		result.c1 = Readint4();
		result.c2 = Readint4();
		result.c3 = Readint4();
		return result;
	}

	public quaternion Readquaternion()
	{
		return new quaternion(Readfloat4());
	}

	public Unity.Mathematics.Random Readrandom()
	{
		Unity.Mathematics.Random result = default(Unity.Mathematics.Random);
		result.state = ReadUInt32();
		return result;
	}

	public RigidTransform ReadRigidTransform()
	{
		RigidTransform result = default(RigidTransform);
		result.rot = Readquaternion();
		result.pos = Readfloat3();
		return result;
	}

	public AffineTransform ReadAffineTransform()
	{
		AffineTransform result = default(AffineTransform);
		result.rs = Readfloat3x3();
		result.t = Readfloat3();
		return result;
	}

	public MinMaxAABB ReadMinMaxAABB()
	{
		MinMaxAABB result = default(MinMaxAABB);
		result.Min = Readfloat3();
		result.Max = Readfloat3();
		return result;
	}

	public uint2 Readuint2()
	{
		uint2 result = default(uint2);
		result.x = ReadUInt32();
		result.y = ReadUInt32();
		return result;
	}

	public uint3 Readuint3()
	{
		uint3 result = default(uint3);
		result.x = ReadUInt32();
		result.y = ReadUInt32();
		result.z = ReadUInt32();
		return result;
	}

	public uint4 Readuint4()
	{
		uint4 result = default(uint4);
		result.x = ReadUInt32();
		result.y = ReadUInt32();
		result.z = ReadUInt32();
		result.w = ReadUInt32();
		return result;
	}

	public uint2x2 Readuint2x2()
	{
		uint2x2 result = default(uint2x2);
		result.c0 = Readuint2();
		result.c1 = Readuint2();
		return result;
	}

	public uint2x3 Readuint2x3()
	{
		uint2x3 result = default(uint2x3);
		result.c0 = Readuint2();
		result.c1 = Readuint2();
		result.c2 = Readuint2();
		return result;
	}

	public uint2x4 Readuint2x4()
	{
		uint2x4 result = default(uint2x4);
		result.c0 = Readuint2();
		result.c1 = Readuint2();
		result.c2 = Readuint2();
		result.c3 = Readuint2();
		return result;
	}

	public uint3x2 Readuint3x2()
	{
		uint3x2 result = default(uint3x2);
		result.c0 = Readuint3();
		result.c1 = Readuint3();
		return result;
	}

	public uint3x3 Readuint3x3()
	{
		uint3x3 result = default(uint3x3);
		result.c0 = Readuint3();
		result.c1 = Readuint3();
		result.c2 = Readuint3();
		return result;
	}

	public uint3x4 Readuint3x4()
	{
		uint3x4 result = default(uint3x4);
		result.c0 = Readuint3();
		result.c1 = Readuint3();
		result.c2 = Readuint3();
		result.c3 = Readuint3();
		return result;
	}

	public uint4x2 Readuint4x2()
	{
		uint4x2 result = default(uint4x2);
		result.c0 = Readuint4();
		result.c1 = Readuint4();
		return result;
	}

	public uint4x3 Readuint4x3()
	{
		uint4x3 result = default(uint4x3);
		result.c0 = Readuint4();
		result.c1 = Readuint4();
		result.c2 = Readuint4();
		return result;
	}

	public uint4x4 Readuint4x4()
	{
		uint4x4 result = default(uint4x4);
		result.c0 = Readuint4();
		result.c1 = Readuint4();
		result.c2 = Readuint4();
		result.c3 = Readuint4();
		return result;
	}
}
