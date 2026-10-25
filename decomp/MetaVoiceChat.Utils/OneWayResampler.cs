using System;
using Concentus;

namespace MetaVoiceChat.Utils;

public class OneWayResampler
{
	private IResampler resampler;

	private int channels;

	private int inputFrequency;

	private int outputFrequency;

	private int quality;

	public void Configure(int channels, int inputFrequency, int outputFrequency, int quality)
	{
		if (this.channels != channels || this.inputFrequency != inputFrequency || this.outputFrequency != outputFrequency || this.quality != quality)
		{
			Free();
			this.channels = channels;
			this.inputFrequency = inputFrequency;
			this.outputFrequency = outputFrequency;
			this.quality = quality;
			resampler = ResamplerFactory.CreateResampler(channels, inputFrequency, outputFrequency, quality);
		}
	}

	public void ProcessInterleaved(Span<float> input, ref int in_len, Span<float> output, ref int out_len)
	{
		resampler.ProcessInterleaved(input, ref in_len, output, ref out_len);
	}

	public void ResetMem()
	{
		resampler?.ResetMem();
	}

	public void Free()
	{
		resampler?.Dispose();
		resampler = null;
		channels = 0;
		inputFrequency = 0;
		outputFrequency = 0;
		quality = 0;
	}
}
