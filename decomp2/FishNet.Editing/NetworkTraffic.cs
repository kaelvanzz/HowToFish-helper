using System.Collections.Generic;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;

namespace FishNet.Editing;

internal class NetworkTraffic : IResettable
{
	public struct Packet
	{
		public string Details;

		public ulong Bytes;

		public GameObject GameObject;

		public Packet(ulong bytes)
			: this(string.Empty, bytes, null)
		{
		}

		public Packet(string details, ulong bytes)
			: this(details, bytes, null)
		{
		}

		public Packet(ulong bytes, GameObject gameObject)
			: this(string.Empty, bytes, gameObject)
		{
		}

		public Packet(string details, ulong bytes, GameObject gameObject)
		{
			Details = details;
			Bytes = bytes;
			GameObject = gameObject;
		}
	}

	public class PacketGroup : IResettable
	{
		private List<Packet> _packets = new List<Packet>();

		public PacketId PacketId { get; private set; }

		public ulong Bytes { get; private set; }

		public float Percent { get; private set; }

		public bool IsUnspecifiedPacketId => PacketId == (PacketId)65535;

		public void Initialize(PacketId packetId)
		{
			PacketId = packetId;
		}

		public void AddPacket(string details, ulong bytes, GameObject gameObject)
		{
			Bytes += bytes;
			_packets.Add(new Packet(details, bytes, gameObject));
		}

		public void SetPercent(ulong allPacketGroupBytes)
		{
			if (Bytes == 0L)
			{
				Percent = 0f;
			}
			else
			{
				Percent = (float)Bytes / (float)allPacketGroupBytes;
			}
		}

		public void ResetState()
		{
			PacketId = PacketId.Unset;
			Bytes = 0uL;
			Percent = 0f;
			_packets.Clear();
		}

		public void InitializeState()
		{
		}
	}

	private Dictionary<PacketId, PacketGroup> _packetGroups;

	public ulong Bytes;

	public void AddPacketIdData(PacketId packetId, string details, ulong bytes, GameObject gameObject)
	{
		LAddPacketId(packetId, details, bytes, gameObject);
	}

	public void AddSocketData(ulong bytes)
	{
		LAddPacketId((PacketId)65535, string.Empty, bytes, null);
	}

	private void LAddPacketId(PacketId packetId, string details, ulong bytes, GameObject gameObject)
	{
		if (!_packetGroups.TryGetValue(packetId, out var value))
		{
			value = ResettableObjectCaches<PacketGroup>.Retrieve();
			value.Initialize(packetId);
			_packetGroups[packetId] = value;
		}
		Bytes += bytes;
		value.AddPacket(details, bytes, gameObject);
	}

	public void SetPacketGroupPercentages()
	{
		ulong bytes = Bytes;
		foreach (PacketGroup value in _packetGroups.Values)
		{
			value.SetPercent(bytes);
		}
	}

	public void ResetState()
	{
		Bytes = 0uL;
		ResettableT2CollectionCaches<PacketId, PacketGroup>.StoreAndDefault(ref _packetGroups);
	}

	public void InitializeState()
	{
		_packetGroups = ResettableT2CollectionCaches<PacketId, PacketGroup>.RetrieveDictionary();
	}
}
