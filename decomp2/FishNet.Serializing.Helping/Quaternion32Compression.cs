using System;
using UnityEngine;

namespace FishNet.Serializing.Helping;

public static class Quaternion32Compression
{
	private const float Maximum = 0.70710653f;

	private const int BitsPerAxis = 10;

	private const int LargestComponentShift = 30;

	private const int AShift = 20;

	private const int BShift = 10;

	private const int IntScale = 511;

	private const int IntMask = 1023;

	public static void Compress(Writer writer, Quaternion quaternion, bool axesFlippingEnabled = true)
	{
		float num = Mathf.Abs(quaternion.x);
		float num2 = Mathf.Abs(quaternion.y);
		float num3 = Mathf.Abs(quaternion.z);
		float num4 = Mathf.Abs(quaternion.w);
		ComponentType componentType = ComponentType.X;
		float num5 = num;
		float num6 = quaternion.x;
		if (num2 > num5)
		{
			num5 = num2;
			componentType = ComponentType.Y;
			num6 = quaternion.y;
		}
		if (num3 > num5)
		{
			num5 = num3;
			componentType = ComponentType.Z;
			num6 = quaternion.z;
		}
		if (num4 > num5)
		{
			componentType = ComponentType.W;
			num6 = quaternion.w;
		}
		bool flag = num6 < 0f;
		if (!axesFlippingEnabled)
		{
			if (num < 0.00098f)
			{
				quaternion.x = 0f;
			}
			if (num2 < 0.00098f)
			{
				quaternion.y = 0f;
			}
			if (num3 < 0.00098f)
			{
				quaternion.z = 0f;
			}
			if (num4 < 0.00098f)
			{
				quaternion.w = 0f;
			}
		}
		float num7 = 0f;
		float num8 = 0f;
		float num9 = 0f;
		switch (componentType)
		{
		case ComponentType.X:
			num7 = quaternion.y;
			num8 = quaternion.z;
			num9 = quaternion.w;
			break;
		case ComponentType.Y:
			num7 = quaternion.x;
			num8 = quaternion.z;
			num9 = quaternion.w;
			break;
		case ComponentType.Z:
			num7 = quaternion.x;
			num8 = quaternion.y;
			num9 = quaternion.w;
			break;
		case ComponentType.W:
			num7 = quaternion.x;
			num8 = quaternion.y;
			num9 = quaternion.z;
			break;
		}
		if (flag && axesFlippingEnabled)
		{
			num7 = 0f - num7;
			num8 = 0f - num8;
			num9 = 0f - num9;
		}
		uint num10 = ScaleToUint(num7);
		uint num11 = ScaleToUint(num8);
		uint num12 = ScaleToUint(num9);
		if (!axesFlippingEnabled)
		{
			writer.WriteBoolean(num6 < 0f);
		}
		uint value = ((uint)componentType << 30) | (num10 << 20) | (num11 << 10) | num12;
		writer.WriteUInt32Unpacked(value);
	}

	private static uint ScaleToUint(float v)
	{
		return (uint)Mathf.RoundToInt(v / 0.70710653f * 511f) & 0x3FFu;
	}

	private static float ScaleToFloat(uint v)
	{
		float num = (float)v * 0.70710653f / 511f;
		if (num > 0.70710653f)
		{
			num -= 1.4142131f;
		}
		return num;
	}

	public static Quaternion Decompress(Reader reader, bool axesFlippingEnabled = true)
	{
		bool flag = !axesFlippingEnabled && reader.ReadBoolean();
		uint num = reader.ReadUInt32Unpacked();
		ComponentType componentType = (ComponentType)(num >> 30);
		uint v = (num >> 20) & 0x3FFu;
		uint v2 = (num >> 10) & 0x3FFu;
		uint v3 = num & 0x3FF;
		float num2 = ScaleToFloat(v);
		float num3 = ScaleToFloat(v2);
		float num4 = ScaleToFloat(v3);
		Quaternion result = default(Quaternion);
		switch (componentType)
		{
		case ComponentType.X:
			result.y = num2;
			result.z = num3;
			result.w = num4;
			result.x = Mathf.Sqrt(1f - result.y * result.y - result.z * result.z - result.w * result.w);
			if (flag)
			{
				result.x *= -1f;
			}
			break;
		case ComponentType.Y:
			result.x = num2;
			result.z = num3;
			result.w = num4;
			result.y = Mathf.Sqrt(1f - result.x * result.x - result.z * result.z - result.w * result.w);
			if (flag)
			{
				result.y *= -1f;
			}
			break;
		case ComponentType.Z:
			result.x = num2;
			result.y = num3;
			result.w = num4;
			result.z = Mathf.Sqrt(1f - result.x * result.x - result.y * result.y - result.w * result.w);
			if (flag)
			{
				result.z *= -1f;
			}
			break;
		case ComponentType.W:
			result.x = num2;
			result.y = num3;
			result.z = num4;
			result.w = Mathf.Sqrt(1f - result.x * result.x - result.y * result.y - result.z * result.z);
			if (flag)
			{
				result.w *= -1f;
			}
			break;
		default:
			throw new ArgumentOutOfRangeException("Unknown rotation component type: " + componentType);
		}
		return result;
	}
}
