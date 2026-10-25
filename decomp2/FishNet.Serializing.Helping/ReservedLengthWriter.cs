using FishNet.Managing;
using GameKit.Dependencies.Utilities;

namespace FishNet.Serializing.Helping;

internal class ReservedLengthWriter : IResettable
{
	private Writer _writer;

	private int _startPosition;

	private byte _reservedBytes;

	public int Length
	{
		get
		{
			if (_writer != null)
			{
				return _writer.Position - _startPosition;
			}
			return 0;
		}
	}

	public void Initialize(Writer writer, byte reservedBytes)
	{
		_writer = writer;
		_reservedBytes = reservedBytes;
		writer.Skip(reservedBytes);
		_startPosition = writer.Position;
	}

	public void WriteLength()
	{
		WriteLength((uint)Length);
		ResetState();
	}

	public bool WriteLengthOrRemove(uint written)
	{
		if (written == 0)
		{
			_writer.Remove(_reservedBytes);
		}
		else
		{
			WriteLength(written);
		}
		ResetState();
		return written != 0;
	}

	public void WriteLength(uint written)
	{
		switch (_reservedBytes)
		{
		case 1:
			_writer.InsertUInt8Unpacked((byte)written, _startPosition - _reservedBytes);
			break;
		case 2:
			_writer.InsertUInt16Unpacked((ushort)written, _startPosition - _reservedBytes);
			break;
		case 4:
			_writer.InsertUInt32Unpacked(written, _startPosition - _reservedBytes);
			break;
		default:
		{
			string text = $"Reserved bytes value of {_reservedBytes} is unhandled.";
			if (_writer != null)
			{
				_writer.NetworkManager.LogError(text);
			}
			else
			{
				NetworkManagerExtensions.LogError(text);
			}
			break;
		}
		}
		ResetState();
	}

	public bool WriteLengthOrRemove()
	{
		int num = _writer.Position - _startPosition;
		if (num == 0)
		{
			_writer.Remove(_reservedBytes);
		}
		else
		{
			WriteLength((uint)num);
		}
		ResetState();
		return num > 0;
	}

	public static uint ReadLength(PooledReader reader, byte reservedBytes, bool resetPosition = false)
	{
		uint num;
		switch (reservedBytes)
		{
		case 1:
			num = reader.ReadUInt8Unpacked();
			break;
		case 2:
			num = reader.ReadUInt16Unpacked();
			break;
		case 4:
			num = reader.ReadUInt32Unpacked();
			break;
		default:
		{
			string text = $"Reserved bytes value of {reservedBytes} is unhandled.";
			if (reader != null)
			{
				reader.NetworkManager.LogError(text);
			}
			else
			{
				NetworkManagerExtensions.LogError(text);
			}
			return 0u;
		}
		}
		if (resetPosition)
		{
			reader.Position -= (int)num;
		}
		return num;
	}

	public void ResetState()
	{
		_writer = null;
		_startPosition = 0;
		_reservedBytes = 0;
	}

	public void InitializeState()
	{
	}
}
