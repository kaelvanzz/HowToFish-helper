namespace MetaVoiceChat.Output.OnAudioFilterReadVcOutput;

public readonly struct OnAudioFilterReadFrame
{
	public readonly float[] data;

	public readonly int dataLength;

	public readonly int sampleRateHz;

	public readonly int channels;

	public readonly ushort sequenceNumber;

	public readonly uint timestamp;

	public int SamplesPerChannel
	{
		get
		{
			if (channels <= 0)
			{
				return 0;
			}
			return dataLength / channels;
		}
	}

	public OnAudioFilterReadFrame(float[] data, int dataLength, int sampleRateHz, int channels, ushort sequenceNumber, uint timestamp)
	{
		this.data = data;
		this.dataLength = dataLength;
		this.sampleRateHz = sampleRateHz;
		this.channels = channels;
		this.sequenceNumber = sequenceNumber;
		this.timestamp = timestamp;
	}
}
