using System;
using FishNet.Managing;
using UnityEngine;

namespace FishNet.Serializing.Helping;

public static class QuaternionPrecisionCompression
{
	public static void Compress(Writer writer, Quaternion value, float precision = 0.001f)
	{
		if (precision >= 0.001f)
		{
			Quaternion32Compression.Compress(writer, value, axesFlippingEnabled: false);
			return;
		}
		int position = writer.Position;
		writer.Skip(1);
		QuaternionPrecisionFlag flags = QuaternionPrecisionFlag.Unset;
		float largestAxesValue = float.MinValue;
		UpdateLargestValues(Math.Abs(value.x), QuaternionPrecisionFlag.LargestIsX);
		UpdateLargestValues(Math.Abs(value.y), QuaternionPrecisionFlag.LargestIsY);
		UpdateLargestValues(Math.Abs(value.z), QuaternionPrecisionFlag.LargestIsZ);
		UpdateLargestValues(Math.Abs(value.w), QuaternionPrecisionFlag.LargestIsW);
		if (flags == QuaternionPrecisionFlag.LargestIsX)
		{
			WriteValuesAndSetPositives(value.y, value.z, value.w, value.x);
		}
		else if (flags == QuaternionPrecisionFlag.LargestIsY)
		{
			WriteValuesAndSetPositives(value.x, value.z, value.w, value.y);
		}
		else if (flags == QuaternionPrecisionFlag.LargestIsZ)
		{
			WriteValuesAndSetPositives(value.x, value.y, value.w, value.z);
		}
		else if (flags == QuaternionPrecisionFlag.LargestIsW)
		{
			WriteValuesAndSetPositives(value.x, value.y, value.z, value.w);
		}
		writer.InsertUInt8Unpacked((byte)flags, position);
		void UpdateLargestValues(float checkedValue, QuaternionPrecisionFlag newFlag)
		{
			if (checkedValue > largestAxesValue)
			{
				largestAxesValue = checkedValue;
				flags = newFlag;
			}
		}
		void WriteValuesAndSetPositives(float aValue, float bValue, float cValue, float largestAxes)
		{
			uint num = (uint)Mathf.RoundToInt(1f / precision);
			uint num2 = (uint)Mathf.RoundToInt(Math.Abs(aValue) * (float)num);
			uint num3 = (uint)Mathf.RoundToInt(Math.Abs(bValue) * (float)num);
			uint num4 = (uint)Mathf.RoundToInt(Math.Abs(cValue) * (float)num);
			writer.WriteUnsignedPackedWhole(num2);
			writer.WriteUnsignedPackedWhole(num3);
			writer.WriteUnsignedPackedWhole(num4);
			if (aValue < 0f)
			{
				flags |= QuaternionPrecisionFlag.AIsNegative;
			}
			if (bValue < 0f)
			{
				flags |= QuaternionPrecisionFlag.BIsNegative;
			}
			if (cValue <= 0f)
			{
				flags |= QuaternionPrecisionFlag.CIsNegative;
			}
			if (largestAxes < 0f)
			{
				flags |= QuaternionPrecisionFlag.DIsNegative;
			}
		}
	}

	public static Quaternion Decompress(Reader reader, float precision = 0.001f)
	{
		if (precision >= 0.001f)
		{
			return Quaternion32Compression.Decompress(reader, axesFlippingEnabled: false);
		}
		uint num = (uint)Mathf.RoundToInt(1f / precision);
		QuaternionPrecisionFlag quaternionPrecisionFlag = (QuaternionPrecisionFlag)reader.ReadUInt8Unpacked();
		if (quaternionPrecisionFlag == QuaternionPrecisionFlag.Unset)
		{
			NetworkManagerExtensions.LogError("Unset flags were returned.");
			return default(Quaternion);
		}
		float aValue = (float)reader.ReadUnsignedPackedWhole() / (float)num;
		float bValue = (float)reader.ReadUnsignedPackedWhole() / (float)num;
		float cValue = (float)reader.ReadUnsignedPackedWhole() / (float)num;
		if (quaternionPrecisionFlag.FastContains(QuaternionPrecisionFlag.AIsNegative))
		{
			aValue *= -1f;
		}
		if (quaternionPrecisionFlag.FastContains(QuaternionPrecisionFlag.BIsNegative))
		{
			bValue *= -1f;
		}
		if (quaternionPrecisionFlag.FastContains(QuaternionPrecisionFlag.CIsNegative))
		{
			cValue *= -1f;
		}
		float num2 = GetMagnitude(aValue, bValue, cValue);
		float dValue = 1f - num2;
		if (dValue < 0f)
		{
			dValue *= -1f;
		}
		dValue = (float)Math.Sqrt(dValue);
		if (dValue >= 0f && quaternionPrecisionFlag.FastContains(QuaternionPrecisionFlag.DIsNegative))
		{
			dValue *= -1f;
		}
		if (!TryNormalize())
		{
			return default(Quaternion);
		}
		if (quaternionPrecisionFlag.FastContains(QuaternionPrecisionFlag.LargestIsX))
		{
			return new Quaternion(dValue, aValue, bValue, cValue);
		}
		if (quaternionPrecisionFlag.FastContains(QuaternionPrecisionFlag.LargestIsY))
		{
			return new Quaternion(aValue, dValue, bValue, cValue);
		}
		if (quaternionPrecisionFlag.FastContains(QuaternionPrecisionFlag.LargestIsZ))
		{
			return new Quaternion(aValue, bValue, dValue, cValue);
		}
		if (quaternionPrecisionFlag.FastContains(QuaternionPrecisionFlag.LargestIsW))
		{
			return new Quaternion(aValue, bValue, cValue, dValue);
		}
		NetworkManagerExtensions.LogError($"Unhandled Largest flag. Received flags are {quaternionPrecisionFlag}.");
		return default(Quaternion);
		static float GetMagnitude(float a, float b, float c, float d = 0f)
		{
			return a * a + b * b + c * c + d * d;
		}
		bool TryNormalize()
		{
			float num3 = (float)Math.Sqrt(GetMagnitude(aValue, bValue, cValue, dValue));
			if (num3 < float.Epsilon)
			{
				NetworkManagerExtensions.LogError("Magnitude cannot be normalized.");
				return false;
			}
			aValue /= num3;
			bValue /= num3;
			cValue /= num3;
			dValue /= num3;
			return true;
		}
	}
}
