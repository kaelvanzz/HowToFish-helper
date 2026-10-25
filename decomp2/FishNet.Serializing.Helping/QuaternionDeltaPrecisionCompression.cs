using System;
using FishNet.Managing;
using UnityEngine;

namespace FishNet.Serializing.Helping;

public static class QuaternionDeltaPrecisionCompression
{
	public static void Compress(Writer writer, Quaternion valueA, Quaternion valueB, float precision = 0.001f)
	{
		uint lMultiplier = (uint)Mathf.RoundToInt(1f / precision);
		int position = writer.Position;
		writer.Skip(1);
		QuaternionDeltaPrecisionFlag flags = QuaternionDeltaPrecisionFlag.Unset;
		long largestUValue = -1L;
		bool largestIsNegative = false;
		uint multipliedUResult;
		bool nextIsLarger = GetNextIsLarger(valueA.x, valueB.x, lMultiplier, out multipliedUResult);
		UpdateLargestValues(multipliedUResult, valueB.x, QuaternionDeltaPrecisionFlag.LargestIsX);
		uint multipliedUResult2;
		bool nextIsLarger2 = GetNextIsLarger(valueA.y, valueB.y, lMultiplier, out multipliedUResult2);
		UpdateLargestValues(multipliedUResult2, valueB.y, QuaternionDeltaPrecisionFlag.LargestIsY);
		uint multipliedUResult3;
		bool nextIsLarger3 = GetNextIsLarger(valueA.z, valueB.z, lMultiplier, out multipliedUResult3);
		UpdateLargestValues(multipliedUResult3, valueB.z, QuaternionDeltaPrecisionFlag.LargestIsZ);
		uint multipliedUResult4;
		bool nextIsLarger4 = GetNextIsLarger(valueA.w, valueB.w, lMultiplier, out multipliedUResult4);
		UpdateLargestValues(multipliedUResult4, valueB.w, QuaternionDeltaPrecisionFlag.LargestIsW);
		if (flags == QuaternionDeltaPrecisionFlag.Unset)
		{
			writer.InsertUInt8Unpacked((byte)flags, position);
			NetworkManagerExtensions.LogError("Flags should not be unset.");
			return;
		}
		if (flags == QuaternionDeltaPrecisionFlag.LargestIsX)
		{
			WriteValues(multipliedUResult2, nextIsLarger2, multipliedUResult3, nextIsLarger3, multipliedUResult4, nextIsLarger4);
		}
		else if (flags == QuaternionDeltaPrecisionFlag.LargestIsY)
		{
			WriteValues(multipliedUResult, nextIsLarger, multipliedUResult3, nextIsLarger3, multipliedUResult4, nextIsLarger4);
		}
		else if (flags == QuaternionDeltaPrecisionFlag.LargestIsZ)
		{
			WriteValues(multipliedUResult, nextIsLarger, multipliedUResult2, nextIsLarger2, multipliedUResult4, nextIsLarger4);
		}
		else if (flags == QuaternionDeltaPrecisionFlag.LargestIsW)
		{
			WriteValues(multipliedUResult, nextIsLarger, multipliedUResult2, nextIsLarger2, multipliedUResult3, nextIsLarger3);
		}
		if (largestIsNegative)
		{
			flags |= QuaternionDeltaPrecisionFlag.NextDIsNegative;
		}
		writer.InsertUInt8Unpacked((byte)flags, position);
		void UpdateLargestValues(uint checkedValue, float fValue, QuaternionDeltaPrecisionFlag newFlag)
		{
			if (checkedValue > largestUValue)
			{
				largestUValue = checkedValue;
				flags = newFlag;
				largestIsNegative = fValue < 0f;
			}
		}
		void WriteValues(uint aValue, bool aIsLarger, uint bValue, bool bIsLarger, uint cValue, bool cIsLarger)
		{
			writer.WriteUnsignedPackedWhole(aValue);
			if (aIsLarger)
			{
				flags |= QuaternionDeltaPrecisionFlag.NextAIsLarger;
			}
			writer.WriteUnsignedPackedWhole(bValue);
			if (bIsLarger)
			{
				flags |= QuaternionDeltaPrecisionFlag.NextBIsLarger;
			}
			writer.WriteUnsignedPackedWhole(cValue);
			if (cIsLarger)
			{
				flags |= QuaternionDeltaPrecisionFlag.NextCIsLarger;
			}
		}
	}

	public static Quaternion Decompress(Reader reader, Quaternion valueA, float precision = 0.001f)
	{
		uint num = (uint)Mathf.RoundToInt(1f / precision);
		QuaternionDeltaPrecisionFlag quaternionDeltaPrecisionFlag = (QuaternionDeltaPrecisionFlag)reader.ReadUInt8Unpacked();
		if (quaternionDeltaPrecisionFlag == QuaternionDeltaPrecisionFlag.Unset)
		{
			NetworkManagerExtensions.LogError("Unset flags were returned.");
			return default(Quaternion);
		}
		int num2 = (int)reader.ReadUnsignedPackedWhole();
		uint num3 = (uint)reader.ReadUnsignedPackedWhole();
		uint num4 = (uint)reader.ReadUnsignedPackedWhole();
		float num5 = (float)(uint)num2 / (float)num;
		float num6 = (float)num3 / (float)num;
		float num7 = (float)num4 / (float)num;
		if (!quaternionDeltaPrecisionFlag.FastContains(QuaternionDeltaPrecisionFlag.NextAIsLarger))
		{
			num5 *= -1f;
		}
		if (!quaternionDeltaPrecisionFlag.FastContains(QuaternionDeltaPrecisionFlag.NextBIsLarger))
		{
			num6 *= -1f;
		}
		if (!quaternionDeltaPrecisionFlag.FastContains(QuaternionDeltaPrecisionFlag.NextCIsLarger))
		{
			num7 *= -1f;
		}
		float nextA;
		float nextB;
		float nextC;
		if (quaternionDeltaPrecisionFlag.FastContains(QuaternionDeltaPrecisionFlag.LargestIsX))
		{
			nextA = valueA.y + num5;
			nextB = valueA.z + num6;
			nextC = valueA.w + num7;
		}
		else if (quaternionDeltaPrecisionFlag.FastContains(QuaternionDeltaPrecisionFlag.LargestIsY))
		{
			nextA = valueA.x + num5;
			nextB = valueA.z + num6;
			nextC = valueA.w + num7;
		}
		else if (quaternionDeltaPrecisionFlag.FastContains(QuaternionDeltaPrecisionFlag.LargestIsZ))
		{
			nextA = valueA.x + num5;
			nextB = valueA.y + num6;
			nextC = valueA.w + num7;
		}
		else
		{
			if (!quaternionDeltaPrecisionFlag.FastContains(QuaternionDeltaPrecisionFlag.LargestIsW))
			{
				NetworkManagerExtensions.LogError($"Largest axes was not handled. Flags {quaternionDeltaPrecisionFlag}.");
				return default(Quaternion);
			}
			nextA = valueA.x + num5;
			nextB = valueA.y + num6;
			nextC = valueA.z + num7;
		}
		float num8 = GetMagnitude(nextA, nextB, nextC);
		float nextD = 1f - num8;
		if (nextD < 0f)
		{
			nextD *= -1f;
		}
		nextD = (float)Math.Sqrt(nextD);
		if (nextD >= 0f && quaternionDeltaPrecisionFlag.FastContains(QuaternionDeltaPrecisionFlag.NextDIsNegative))
		{
			nextD *= -1f;
		}
		if (!TryNormalize())
		{
			return default(Quaternion);
		}
		if (quaternionDeltaPrecisionFlag.FastContains(QuaternionDeltaPrecisionFlag.LargestIsX))
		{
			return new Quaternion(nextD, nextA, nextB, nextC);
		}
		if (quaternionDeltaPrecisionFlag.FastContains(QuaternionDeltaPrecisionFlag.LargestIsY))
		{
			return new Quaternion(nextA, nextD, nextB, nextC);
		}
		if (quaternionDeltaPrecisionFlag.FastContains(QuaternionDeltaPrecisionFlag.LargestIsZ))
		{
			return new Quaternion(nextA, nextB, nextD, nextC);
		}
		if (quaternionDeltaPrecisionFlag.FastContains(QuaternionDeltaPrecisionFlag.LargestIsW))
		{
			return new Quaternion(nextA, nextB, nextC, nextD);
		}
		NetworkManagerExtensions.LogError($"Unhandled Largest flag. Received flags are {quaternionDeltaPrecisionFlag}.");
		return default(Quaternion);
		static float GetMagnitude(float a, float b, float c, float d = 0f)
		{
			return a * a + b * b + c * c + d * d;
		}
		bool TryNormalize()
		{
			float num9 = (float)Math.Sqrt(GetMagnitude(nextA, nextB, nextC, nextD));
			if (num9 < float.Epsilon)
			{
				NetworkManagerExtensions.LogError("Magnitude cannot be normalized.");
				return false;
			}
			nextA /= num9;
			nextB /= num9;
			nextC /= num9;
			nextD /= num9;
			return true;
		}
	}

	private static bool GetNextIsLarger(float a, float b, uint lMultiplier, out uint multipliedUResult)
	{
		bool num = b > a;
		float num2 = (num ? (b - a) : (a - b));
		multipliedUResult = (uint)Mathf.RoundToInt(num2 * (float)lMultiplier);
		return num;
	}
}
