using System;

namespace FishNet.Managing.Timing;

public static class PreciseTickExtensions
{
	public static PreciseTick Add(this PreciseTick pt, PreciseTick value, double delta)
	{
		double num = pt.AsDouble(delta);
		double num2 = value.AsDouble(delta);
		return (num + num2).AsPreciseTick(delta);
	}

	public static PreciseTick Subtract(this PreciseTick pt, PreciseTick value, double delta)
	{
		double num = pt.AsDouble(delta);
		double num2 = value.AsDouble(delta);
		return (num - num2).AsPreciseTick(delta);
	}

	public static double AsDouble(this PreciseTick pt, double delta)
	{
		return (double)pt.Tick * delta + pt.PercentAsDouble * delta;
	}

	public static PreciseTick AsPreciseTick(this double ptDouble, double delta)
	{
		if (ptDouble <= 0.0)
		{
			return new PreciseTick(0u, 0);
		}
		ulong num = (ulong)Math.Floor(ptDouble / delta);
		if (num >= uint.MaxValue)
		{
			return PreciseTick.GetUnsetValue();
		}
		double percent = ptDouble % delta / delta;
		return new PreciseTick((uint)num, percent);
	}
}
