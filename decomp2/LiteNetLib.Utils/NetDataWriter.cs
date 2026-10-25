using System;
using System.Net;
using System.Text;
using System.Threading;

namespace LiteNetLib.Utils;

public class NetDataWriter
{
	protected byte[] _data;

	protected int _position;

	private const int InitialSize = 64;

	private readonly bool _autoResize;

	public static readonly ThreadLocal<UTF8Encoding> uTF8Encoding = new ThreadLocal<UTF8Encoding>(() => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));

	public const int StringBufferMaxLength = 65535;

	private readonly byte[] _stringBuffer = new byte[65535];

	public int Capacity => _data.Length;

	public byte[] Data => _data;

	public int Length => _position;

	public NetDataWriter()
		: this(autoResize: true, 64)
	{
	}

	public NetDataWriter(bool autoResize)
		: this(autoResize, 64)
	{
	}

	public NetDataWriter(bool autoResize, int initialSize)
	{
		_data = new byte[initialSize];
		_autoResize = autoResize;
	}

	public static NetDataWriter FromBytes(byte[] bytes, bool copy)
	{
		if (copy)
		{
			NetDataWriter netDataWriter = new NetDataWriter(autoResize: true, bytes.Length);
			netDataWriter.Put(bytes);
			return netDataWriter;
		}
		return new NetDataWriter(autoResize: true, 0)
		{
			_data = bytes,
			_position = bytes.Length
		};
	}

	public static NetDataWriter FromBytes(byte[] bytes, int offset, int length)
	{
		NetDataWriter netDataWriter = new NetDataWriter(autoResize: true, bytes.Length);
		netDataWriter.Put(bytes, offset, length);
		return netDataWriter;
	}

	public static NetDataWriter FromString(string value)
	{
		NetDataWriter netDataWriter = new NetDataWriter();
		netDataWriter.Put(value);
		return netDataWriter;
	}

	public void ResizeIfNeed(int newSize)
	{
		if (_data.Length < newSize)
		{
			Array.Resize(ref _data, Math.Max(newSize, _data.Length * 2));
		}
	}

	public void EnsureFit(int additionalSize)
	{
		if (_data.Length < _position + additionalSize)
		{
			Array.Resize(ref _data, Math.Max(_position + additionalSize, _data.Length * 2));
		}
	}

	public void Reset(int size)
	{
		ResizeIfNeed(size);
		_position = 0;
	}

	public void Reset()
	{
		_position = 0;
	}

	public byte[] CopyData()
	{
		byte[] array = new byte[_position];
		Buffer.BlockCopy(_data, 0, array, 0, _position);
		return array;
	}

	public int SetPosition(int position)
	{
		int position2 = _position;
		_position = position;
		return position2;
	}

	public void Put(float value)
	{
		if (_autoResize)
		{
			ResizeIfNeed(_position + 4);
		}
		FastBitConverter.GetBytes(_data, _position, value);
		_position += 4;
	}

	public void Put(double value)
	{
		if (_autoResize)
		{
			ResizeIfNeed(_position + 8);
		}
		FastBitConverter.GetBytes(_data, _position, value);
		_position += 8;
	}

	public void Put(long value)
	{
		if (_autoResize)
		{
			ResizeIfNeed(_position + 8);
		}
		FastBitConverter.GetBytes(_data, _position, value);
		_position += 8;
	}

	public void Put(ulong value)
	{
		if (_autoResize)
		{
			ResizeIfNeed(_position + 8);
		}
		FastBitConverter.GetBytes(_data, _position, value);
		_position += 8;
	}

	public void Put(int value)
	{
		if (_autoResize)
		{
			ResizeIfNeed(_position + 4);
		}
		FastBitConverter.GetBytes(_data, _position, value);
		_position += 4;
	}

	public void Put(uint value)
	{
		if (_autoResize)
		{
			ResizeIfNeed(_position + 4);
		}
		FastBitConverter.GetBytes(_data, _position, value);
		_position += 4;
	}

	public void Put(char value)
	{
		Put((ushort)value);
	}

	public void Put(ushort value)
	{
		if (_autoResize)
		{
			ResizeIfNeed(_position + 2);
		}
		FastBitConverter.GetBytes(_data, _position, value);
		_position += 2;
	}

	public void Put(short value)
	{
		if (_autoResize)
		{
			ResizeIfNeed(_position + 2);
		}
		FastBitConverter.GetBytes(_data, _position, value);
		_position += 2;
	}

	public void Put(sbyte value)
	{
		if (_autoResize)
		{
			ResizeIfNeed(_position + 1);
		}
		_data[_position] = (byte)value;
		_position++;
	}

	public void Put(byte value)
	{
		if (_autoResize)
		{
			ResizeIfNeed(_position + 1);
		}
		_data[_position] = value;
		_position++;
	}

	public void Put(byte[] data, int offset, int length)
	{
		if (_autoResize)
		{
			ResizeIfNeed(_position + length);
		}
		Buffer.BlockCopy(data, offset, _data, _position, length);
		_position += length;
	}

	public void Put(byte[] data)
	{
		if (_autoResize)
		{
			ResizeIfNeed(_position + data.Length);
		}
		Buffer.BlockCopy(data, 0, _data, _position, data.Length);
		_position += data.Length;
	}

	public void PutSBytesWithLength(sbyte[] data, int offset, ushort length)
	{
		if (_autoResize)
		{
			ResizeIfNeed(_position + 2 + length);
		}
		FastBitConverter.GetBytes(_data, _position, length);
		Buffer.BlockCopy(data, offset, _data, _position + 2, length);
		_position += 2 + length;
	}

	public void PutSBytesWithLength(sbyte[] data)
	{
		PutArray(data, 1);
	}

	public void PutBytesWithLength(byte[] data, int offset, ushort length)
	{
		if (_autoResize)
		{
			ResizeIfNeed(_position + 2 + length);
		}
		FastBitConverter.GetBytes(_data, _position, length);
		Buffer.BlockCopy(data, offset, _data, _position + 2, length);
		_position += 2 + length;
	}

	public void PutBytesWithLength(byte[] data)
	{
		PutArray(data, 1);
	}

	public void Put(bool value)
	{
		Put((byte)(value ? 1u : 0u));
	}

	public void PutArray(Array arr, int sz)
	{
		ushort num = (ushort)((arr != null) ? ((ushort)arr.Length) : 0);
		sz *= num;
		if (_autoResize)
		{
			ResizeIfNeed(_position + sz + 2);
		}
		FastBitConverter.GetBytes(_data, _position, num);
		if (arr != null)
		{
			Buffer.BlockCopy(arr, 0, _data, _position + 2, sz);
		}
		_position += sz + 2;
	}

	public void PutArray(float[] value)
	{
		PutArray(value, 4);
	}

	public void PutArray(double[] value)
	{
		PutArray(value, 8);
	}

	public void PutArray(long[] value)
	{
		PutArray(value, 8);
	}

	public void PutArray(ulong[] value)
	{
		PutArray(value, 8);
	}

	public void PutArray(int[] value)
	{
		PutArray(value, 4);
	}

	public void PutArray(uint[] value)
	{
		PutArray(value, 4);
	}

	public void PutArray(ushort[] value)
	{
		PutArray(value, 2);
	}

	public void PutArray(short[] value)
	{
		PutArray(value, 2);
	}

	public void PutArray(bool[] value)
	{
		PutArray(value, 1);
	}

	public void PutArray(string[] value)
	{
		ushort num = (ushort)((value != null) ? ((ushort)value.Length) : 0);
		Put(num);
		for (int i = 0; i < num; i++)
		{
			Put(value[i]);
		}
	}

	public void PutArray(string[] value, int strMaxLength)
	{
		ushort num = (ushort)((value != null) ? ((ushort)value.Length) : 0);
		Put(num);
		for (int i = 0; i < num; i++)
		{
			Put(value[i], strMaxLength);
		}
	}

	public void PutArray<T>(T[] value) where T : INetSerializable, new()
	{
		ushort num = (ushort)((value != null) ? ((uint)value.Length) : 0u);
		Put(num);
		for (int i = 0; i < num; i++)
		{
			value[i].Serialize(this);
		}
	}

	public void Put(IPEndPoint endPoint)
	{
		Put(endPoint.Address.ToString());
		Put(endPoint.Port);
	}

	public void Put(string value)
	{
		Put(value, 0);
	}

	public void Put(string value, int maxLength)
	{
		if (string.IsNullOrEmpty(value))
		{
			Put((ushort)0);
			return;
		}
		int charCount = ((maxLength > 0 && value.Length > maxLength) ? maxLength : value.Length);
		int bytes = uTF8Encoding.Value.GetBytes(value, 0, charCount, _stringBuffer, 0);
		if (bytes == 0 || bytes >= 65535)
		{
			Put((ushort)0);
			return;
		}
		Put(checked((ushort)(bytes + 1)));
		Put(_stringBuffer, 0, bytes);
	}

	public void Put<T>(T obj) where T : INetSerializable
	{
		obj.Serialize(this);
	}
}
