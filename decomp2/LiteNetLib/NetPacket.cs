using System;
using LiteNetLib.Utils;

namespace LiteNetLib;

internal sealed class NetPacket
{
	private static readonly int PropertiesCount;

	private static readonly int[] HeaderSizes;

	public byte[] RawData;

	public int Size;

	public object UserData;

	public NetPacket Next;

	public PacketProperty Property
	{
		get
		{
			return (PacketProperty)(RawData[0] & 0x1Fu);
		}
		set
		{
			RawData[0] = (byte)((RawData[0] & 0xE0u) | (uint)value);
		}
	}

	public byte ConnectionNumber
	{
		get
		{
			return (byte)((RawData[0] & 0x60) >> 5);
		}
		set
		{
			RawData[0] = (byte)((RawData[0] & 0x9Fu) | (uint)(value << 5));
		}
	}

	public ushort Sequence
	{
		get
		{
			return BitConverter.ToUInt16(RawData, 1);
		}
		set
		{
			FastBitConverter.GetBytes(RawData, 1, value);
		}
	}

	public bool IsFragmented => (RawData[0] & 0x80) != 0;

	public byte ChannelId
	{
		get
		{
			return RawData[3];
		}
		set
		{
			RawData[3] = value;
		}
	}

	public ushort FragmentId
	{
		get
		{
			return BitConverter.ToUInt16(RawData, 4);
		}
		set
		{
			FastBitConverter.GetBytes(RawData, 4, value);
		}
	}

	public ushort FragmentPart
	{
		get
		{
			return BitConverter.ToUInt16(RawData, 6);
		}
		set
		{
			FastBitConverter.GetBytes(RawData, 6, value);
		}
	}

	public ushort FragmentsTotal
	{
		get
		{
			return BitConverter.ToUInt16(RawData, 8);
		}
		set
		{
			FastBitConverter.GetBytes(RawData, 8, value);
		}
	}

	static NetPacket()
	{
		PropertiesCount = Enum.GetValues(typeof(PacketProperty)).Length;
		HeaderSizes = NetUtils.AllocatePinnedUninitializedArray<int>(PropertiesCount);
		for (int i = 0; i < HeaderSizes.Length; i++)
		{
			switch ((PacketProperty)(byte)i)
			{
			case PacketProperty.Channeled:
			case PacketProperty.Ack:
				HeaderSizes[i] = 4;
				break;
			case PacketProperty.Ping:
				HeaderSizes[i] = 3;
				break;
			case PacketProperty.ConnectRequest:
				HeaderSizes[i] = 18;
				break;
			case PacketProperty.ConnectAccept:
				HeaderSizes[i] = 15;
				break;
			case PacketProperty.Disconnect:
				HeaderSizes[i] = 9;
				break;
			case PacketProperty.Pong:
				HeaderSizes[i] = 11;
				break;
			default:
				HeaderSizes[i] = 1;
				break;
			}
		}
	}

	public void MarkFragmented()
	{
		RawData[0] |= 128;
	}

	public NetPacket(int size)
	{
		RawData = new byte[size];
		Size = size;
	}

	public NetPacket(PacketProperty property, int size)
	{
		size += GetHeaderSize(property);
		RawData = new byte[size];
		Property = property;
		Size = size;
	}

	public static int GetHeaderSize(PacketProperty property)
	{
		return HeaderSizes[(uint)property];
	}

	public int GetHeaderSize()
	{
		return HeaderSizes[RawData[0] & 0x1F];
	}

	public bool Verify()
	{
		byte b = (byte)(RawData[0] & 0x1Fu);
		if (b >= PropertiesCount)
		{
			return false;
		}
		int num = HeaderSizes[b];
		bool flag = (RawData[0] & 0x80) != 0;
		if (Size >= num)
		{
			if (flag)
			{
				return Size >= num + 6;
			}
			return true;
		}
		return false;
	}

	public static implicit operator Span<byte>(NetPacket p)
	{
		return new Span<byte>(p.RawData, 0, p.Size);
	}
}
