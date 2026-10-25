using System;
using FishNet.Documenting;

namespace FishNet.Managing.Timing;

[APIExclude]
public class MovingAverage : IDisposable
{
	private int _writeIndex;

	private float[] _samples;

	private int _writtenSamples;

	private float _sampleAccumulator;

	public float Average { get; private set; }

	public int SampleSize { get; private set; }

	public MovingAverage(int sampleSize)
	{
		if (sampleSize < 2)
		{
			NetworkManagerExtensions.LogWarning("Using a sampleSize of less than 2 will always return the most recent value as Average.");
			sampleSize = 1;
		}
		SampleSize = sampleSize;
		_samples = new float[sampleSize];
	}

	public void ComputeAverage(float newSample)
	{
		if (_samples.Length <= 1)
		{
			Average = newSample;
			return;
		}
		_sampleAccumulator += newSample;
		_samples[_writeIndex] = newSample;
		_writeIndex++;
		_writtenSamples = Math.Max(_writtenSamples, _writeIndex);
		if (_writeIndex >= _samples.Length)
		{
			_writeIndex = 0;
		}
		Average = _sampleAccumulator / (float)_writtenSamples;
		if (_writtenSamples >= _samples.Length)
		{
			_sampleAccumulator -= _samples[_writeIndex];
		}
	}

	public void Reset()
	{
		_sampleAccumulator = 0f;
		_writeIndex = 0;
		_writtenSamples = 0;
	}

	public void Dispose()
	{
		Reset();
	}
}
