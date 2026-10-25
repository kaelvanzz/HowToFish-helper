using FishNet.Serializing;

namespace FishNet.Managing.Timing;

public static class PreciseTickSerializer
{
	public static void WritePreciseTick(this Writer writer, PreciseTick value)
	{
		writer.WriteTickUnpacked(value.Tick);
		writer.WriteUInt8Unpacked(value.PercentAsByte);
	}

	public static PreciseTick ReadPreciseTick(this Reader reader)
	{
		uint tick = reader.ReadTickUnpacked();
		byte percentAsByte = reader.ReadUInt8Unpacked();
		return new PreciseTick(tick, percentAsByte);
	}
}
