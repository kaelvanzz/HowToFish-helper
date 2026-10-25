using System;
using GameKit.Dependencies.Utilities;

namespace FishNet.Managing.Timing;

public readonly struct PreciseTick : IEquatable<PreciseTick>
{
	public readonly uint Tick;

	public readonly double PercentAsDouble;

	public readonly byte PercentAsByte;

	public const double MAXIMUM_DOUBLE_PERCENT = 1.0;

	public const byte MAXIMUM_BYTE_PERCENT = 100;

	public static PreciseTick GetUnsetValue()
	{
		return new PreciseTick(0u, 0);
	}

	public PreciseTick(uint tick)
	{
		Tick = tick;
		PercentAsByte = 0;
		PercentAsDouble = 0.0;
	}

	public PreciseTick(uint tick, byte percentAsByte)
	{
		Tick = tick;
		percentAsByte = Maths.ClampByte(percentAsByte, 0, 100);
		PercentAsByte = percentAsByte;
		PercentAsDouble = (double)(int)percentAsByte / 100.0;
	}

	public PreciseTick(uint tick, double percent)
	{
		Tick = tick;
		percent = Maths.ClampDouble(percent, 0.0, 1.0);
		PercentAsByte = (byte)(percent * 100.0);
		PercentAsDouble = percent;
	}

	public bool IsValid()
	{
		return Tick != 0;
	}

	public override string ToString()
	{
		return string.Format("Tick {0}, Percent {1}", Tick, PercentAsByte.ToString("000"));
	}

	public static bool operator ==(PreciseTick a, PreciseTick b)
	{
		if (a.Tick == b.Tick)
		{
			return a.PercentAsByte == b.PercentAsByte;
		}
		return false;
	}

	public static bool operator !=(PreciseTick a, PreciseTick b)
	{
		return !(a == b);
	}

	public static bool operator >=(PreciseTick a, PreciseTick b)
	{
		if (b.Tick > a.Tick)
		{
			return false;
		}
		if (a.Tick > b.Tick)
		{
			return true;
		}
		return a.PercentAsByte >= b.PercentAsByte;
	}

	public static bool operator <=(PreciseTick a, PreciseTick b)
	{
		return b >= a;
	}

	public static bool operator >(PreciseTick a, PreciseTick b)
	{
		if (b.Tick > a.Tick)
		{
			return false;
		}
		if (a.Tick > b.Tick)
		{
			return true;
		}
		return a.PercentAsByte > b.PercentAsByte;
	}

	public static bool operator <(PreciseTick a, PreciseTick b)
	{
		return b > a;
	}

	public bool Equals(PreciseTick other)
	{
		if (Tick == other.Tick)
		{
			return PercentAsByte == other.PercentAsByte;
		}
		return false;
	}

	public override bool Equals(object obj)
	{
		if (obj is PreciseTick other)
		{
			return Equals(other);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(Tick, PercentAsDouble, PercentAsByte);
	}
}
