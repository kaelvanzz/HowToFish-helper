using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using MetaVoiceChat.NetEq;
using MetaVoiceChat.Utils;
using UnityEngine;

namespace MetaVoiceChat.Output.OnAudioFilterReadVcOutput;

[RequireComponent(typeof(UnityEngine.AudioSource))]
public sealed class OnAudioFilterReadVcOutput : VcAudioOutput
{
	private sealed class PendingFrame
	{
		public float[] Samples = Array.Empty<float>();

		public int SampleLength;

		public int SampleRate;

		public int Channels;

		public ushort SequenceNumber;

		public uint Timestamp;

		public long ReceivedTimestamp;

		public bool IsSilence;

		public NetEqConfig NetEqConfig;

		public void EnsureCapacity(int requiredSamples)
		{
			if (Samples.Length < requiredSamples)
			{
				Samples = new float[requiredSamples];
			}
		}
	}

	private sealed class NetEqSettingsSnapshot
	{
		public readonly int MaxPacketsInBuffer;

		public readonly int AdditionalDelayMs;

		public readonly OnAudioFilterReadVcConfig.JitterBufferMode JitterBufferMode;

		public readonly int CustomMinDelayMs;

		public readonly int CustomMaxDelayMs;

		public NetEqSettingsSnapshot(int maxPacketsInBuffer, int additionalDelayMs, OnAudioFilterReadVcConfig.JitterBufferMode jitterBufferMode, int customMinDelayMs, int customMaxDelayMs)
		{
			MaxPacketsInBuffer = maxPacketsInBuffer;
			AdditionalDelayMs = additionalDelayMs;
			JitterBufferMode = jitterBufferMode;
			CustomMinDelayMs = customMinDelayMs;
			CustomMaxDelayMs = customMaxDelayMs;
		}

		public bool Matches(int maxPacketsInBuffer, int additionalDelayMs, OnAudioFilterReadVcConfig.JitterBufferMode jitterBufferMode, int customMinDelayMs, int customMaxDelayMs)
		{
			if (MaxPacketsInBuffer == maxPacketsInBuffer && AdditionalDelayMs == additionalDelayMs && JitterBufferMode == jitterBufferMode && CustomMinDelayMs == customMinDelayMs)
			{
				return CustomMaxDelayMs == customMaxDelayMs;
			}
			return false;
		}
	}

	private readonly struct NetEqConfig : IEquatable<NetEqConfig>
	{
		public readonly int PacketDurationMs;

		public readonly int MaxPacketsInBuffer;

		public readonly int MaxDelayMs;

		public readonly int MinDelayMs;

		public readonly int AdditionalDelayMs;

		public NetEqConfig(int packetDurationMs, int maxPacketsInBuffer, int maxDelayMs, int minDelayMs, int additionalDelayMs)
		{
			PacketDurationMs = packetDurationMs;
			MaxPacketsInBuffer = maxPacketsInBuffer;
			MaxDelayMs = maxDelayMs;
			MinDelayMs = minDelayMs;
			AdditionalDelayMs = additionalDelayMs;
		}

		public bool Equals(NetEqConfig other)
		{
			if (PacketDurationMs == other.PacketDurationMs && MaxPacketsInBuffer == other.MaxPacketsInBuffer && MaxDelayMs == other.MaxDelayMs && MinDelayMs == other.MinDelayMs)
			{
				return AdditionalDelayMs == other.AdditionalDelayMs;
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is NetEqConfig other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(PacketDurationMs, MaxPacketsInBuffer, MaxDelayMs, MinDelayMs, AdditionalDelayMs);
		}
	}

	private sealed class SampleFifo
	{
		private float[] buffer = Array.Empty<float>();

		private int head;

		private int count;

		public int Count => count;

		public void Write(float[] source, int sourceIndex, int length)
		{
			if (length > 0)
			{
				EnsureCapacity(count + length);
				int num = (head + count) % buffer.Length;
				int num2 = Math.Min(length, buffer.Length - num);
				Array.Copy(source, sourceIndex, buffer, num, num2);
				int num3 = length - num2;
				if (num3 > 0)
				{
					Array.Copy(source, sourceIndex + num2, buffer, 0, num3);
				}
				count += length;
			}
		}

		public int Read(float[] destination, int destinationIndex, int length)
		{
			int num = Math.Min(length, count);
			if (num <= 0)
			{
				return 0;
			}
			int num2 = Math.Min(num, buffer.Length - head);
			Array.Copy(buffer, head, destination, destinationIndex, num2);
			int num3 = num - num2;
			if (num3 > 0)
			{
				Array.Copy(buffer, 0, destination, destinationIndex + num2, num3);
			}
			head = (head + num) % buffer.Length;
			count -= num;
			if (count == 0)
			{
				head = 0;
			}
			return num;
		}

		public void Clear()
		{
			head = 0;
			count = 0;
		}

		private void EnsureCapacity(int required)
		{
			if (buffer.Length < required)
			{
				int num = count;
				float[] destination = new float[NextPowerOfTwo(Math.Max(256, required))];
				if (num > 0)
				{
					Read(destination, 0, num);
				}
				buffer = destination;
				head = 0;
				count = num;
			}
		}
	}

	public const int DefaultMaxPacketsInBuffer = 50;

	public const int DefaultAdditionalDelayMs = 0;

	public const OnAudioFilterReadVcConfig.JitterBufferMode DefaultJitterBufferMode = OnAudioFilterReadVcConfig.JitterBufferMode.Balanced;

	public const int DefaultResamplerQuality = 4;

	public const int DefaultResamplerBufferMs = 10;

	[SerializeField]
	private Player _player;

	private const int PendingFramePoolCapacity = 32;

	private const int MaxPendingFrameAgeMs = 60;

	private const int MaxPacketsDrainedPerCallback = 32;

	private const int MaxNetEqReadsPerCallback = 32;

	private const int NetworkInputSampleRate = 48000;

	[SerializeField]
	private OnAudioFilterReadVcConfig audioFilterReadConfig;

	private const bool createPlaybackClip = true;

	private UnityEngine.AudioSource audioSource;

	private AudioClip playbackClip;

	private float[] playbackClipSamples = Array.Empty<float>();

	private readonly ConcurrentQueue<PendingFrame> pendingFrames = new ConcurrentQueue<PendingFrame>();

	private readonly ConcurrentQueue<PendingFrame> availableFrames = new ConcurrentQueue<PendingFrame>();

	private readonly object framePoolInitializationLock = new object();

	private int framePoolInitialized;

	private int acceptingFrames;

	private int outputActive;

	private int unsupportedPlatformErrorLogged;

	private int audioCallbacksBlocked = 1;

	private int audioCallbackDepth;

	private int disposeAudioThreadStateRequested;

	private int activateOutputRequested;

	private int recreatePlaybackClipRequested;

	private int resetAudioThreadStateRequested;

	private int resetOutputConversionRequested;

	private int cachedOutputSampleRate;

	private int cachedOutputChannels = 2;

	private int cachedDspBufferLength;

	private int cachedDspBufferCount;

	private int cachedDspBufferMs;

	private int cachedResamplerQuality = 4;

	private int cachedResamplerBufferMs = 10;

	private NetEqSettingsSnapshot cachedNetEqSettings = new NetEqSettingsSnapshot(50, 0, OnAudioFilterReadVcConfig.JitterBufferMode.Balanced, 0, 0);

	private int currentBufferSizeMs;

	private int receiveToInsertLatencyMs;

	private int localOutputBufferMs;

	private IntPtr netEqPtr = IntPtr.Zero;

	private readonly OneWayResampler resampler = new OneWayResampler();

	private readonly SampleFifo outputFifo = new SampleFifo();

	private readonly UnmanagedFloatArray netEqPacketBuffer = new UnmanagedFloatArray();

	private readonly UnmanagedFloatArray netEqReadBuffer = new UnmanagedFloatArray();

	private float[] netEqBatchBuffer = Array.Empty<float>();

	private float[] resamplerOutputBuffer = Array.Empty<float>();

	private float[] channelConvertBuffer = Array.Empty<float>();

	private float[] spatializationInputBuffer = Array.Empty<float>();

	private int netEqSampleRate;

	private int netEqChannels;

	private NetEqConfig audioThreadNetEqConfig;

	private int audioThreadOutputSampleRate;

	private int audioThreadOutputChannels;

	public int CurrentBufferSizeMs => Volatile.Read(ref currentBufferSizeMs);

	public int GetEstimatedLocalReceiveToDspLatencyMs()
	{
		return Math.Max(GetOldestPendingFrameAgeMs(), Volatile.Read(ref receiveToInsertLatencyMs)) + Volatile.Read(ref currentBufferSizeMs) + Volatile.Read(ref localOutputBufferMs) + Volatile.Read(ref cachedDspBufferMs);
	}

	public void Process(Span<float> frame, int frameSize, int frequency, int channels, ushort sequenceNumber, uint timestamp)
	{
		if (channels != 1)
		{
			throw new NotSupportedException(string.Format("{0} only supports mono network audio. Received {1} channels.", "OnAudioFilterReadVcOutput", channels));
		}
		if (frequency != 48000)
		{
			throw new NotSupportedException(string.Format("{0} only supports {1} Hz network audio. Received {2} Hz.", "OnAudioFilterReadVcOutput", 48000, frequency));
		}
		if (Volatile.Read(ref acceptingFrames) == 0 || !IsValidFrameShape(frameSize, frequency, channels))
		{
			return;
		}
		bool isEmpty = frame.IsEmpty;
		if (!isEmpty && frame.Length < frameSize)
		{
			return;
		}
		InitializePendingFramePool();
		if (!availableFrames.TryDequeue(out var result))
		{
			return;
		}
		bool flag = false;
		try
		{
			if (!isEmpty)
			{
				result.EnsureCapacity(frameSize);
				frame.Slice(0, frameSize).CopyTo(result.Samples);
			}
			result.SampleLength = frameSize;
			result.SampleRate = frequency;
			result.Channels = channels;
			result.SequenceNumber = sequenceNumber;
			result.Timestamp = timestamp;
			result.ReceivedTimestamp = Stopwatch.GetTimestamp();
			result.IsSilence = isEmpty;
			result.NetEqConfig = GetCachedNetEqConfig(frameSize / channels, frequency);
			if (Volatile.Read(ref acceptingFrames) != 0)
			{
				pendingFrames.Enqueue(result);
				flag = true;
			}
		}
		finally
		{
			if (!flag)
			{
				availableFrames.Enqueue(result);
			}
		}
	}

	private void Awake()
	{
		InitializePendingFramePool();
	}

	private void OnEnable()
	{
		Volatile.Write(ref acceptingFrames, 0);
		Volatile.Write(ref outputActive, 0);
		Volatile.Write(ref audioCallbacksBlocked, 1);
		if (!IsNativeNetEqSupportedPlatform())
		{
			if (Interlocked.Exchange(ref unsupportedPlatformErrorLogged, 1) == 0)
			{
				UnityEngine.Debug.LogError("OnAudioFilterReadVcOutput requires the trusted Windows x64 NetEQ plugin. Use VcAudioSourceOutput on unsupported platforms.", this);
			}
			base.enabled = false;
			return;
		}
		AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
		CacheMainThreadSettings();
		InitializePendingFramePool();
		ClearPendingFrames();
		if (!TryGetComponent<UnityEngine.AudioSource>(out audioSource))
		{
			Volatile.Write(ref acceptingFrames, 0);
			Volatile.Write(ref outputActive, 0);
		}
		else
		{
			ConfigureAudioSource(audioSource);
			Volatile.Write(ref activateOutputRequested, 1);
			TryActivateOutput();
		}
	}

	private void OnDisable()
	{
		AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
		Volatile.Write(ref activateOutputRequested, 0);
		RequestAudioThreadStateDisposal();
		if (audioSource != null)
		{
			audioSource.Stop();
			if (audioSource.clip == playbackClip)
			{
				audioSource.clip = null;
			}
		}
		if (playbackClip != null)
		{
			UnityEngine.Object.Destroy(playbackClip);
			playbackClip = null;
		}
		ClearPendingFrames();
	}

	private void OnDestroy()
	{
		Volatile.Write(ref activateOutputRequested, 0);
		RequestAudioThreadStateDisposal();
	}

	private void Update()
	{
		CacheMainThreadSettings();
		TryCompleteRequestedAudioThreadStateDisposal();
		TryActivateOutput();
		if (Volatile.Read(ref outputActive) != 0 && audioSource != null)
		{
			ConfigureAudioSource(audioSource);
			if (Interlocked.Exchange(ref recreatePlaybackClipRequested, 0) != 0)
			{
				CreatePlaybackClip(forceRecreate: true);
			}
			RestartPlaybackIfNeeded();
		}
	}

	private void OnValidate()
	{
		if (TryGetComponent<UnityEngine.AudioSource>(out var component))
		{
			ConfigureAudioSource(component);
		}
	}

	private void OnAudioConfigurationChanged(bool deviceWasChanged)
	{
		CacheMainThreadSettings();
		Volatile.Write(ref resetOutputConversionRequested, 1);
		Volatile.Write(ref recreatePlaybackClipRequested, 1);
	}

	private void TryActivateOutput()
	{
		if (Volatile.Read(ref activateOutputRequested) != 0 && !(audioSource == null))
		{
			TryCompleteRequestedAudioThreadStateDisposal();
			if (Volatile.Read(ref disposeAudioThreadStateRequested) == 0 && Volatile.Read(ref audioCallbackDepth) == 0 && Interlocked.CompareExchange(ref activateOutputRequested, 0, 1) == 1)
			{
				Volatile.Write(ref resetAudioThreadStateRequested, 0);
				Volatile.Write(ref resetOutputConversionRequested, 1);
				Volatile.Write(ref outputActive, 1);
				Volatile.Write(ref acceptingFrames, 1);
				CreatePlaybackClip(Interlocked.Exchange(ref recreatePlaybackClipRequested, 0) != 0);
				Volatile.Write(ref audioCallbacksBlocked, 0);
				audioSource.Play();
			}
		}
	}

	private void RequestAudioThreadStateDisposal()
	{
		Volatile.Write(ref acceptingFrames, 0);
		Volatile.Write(ref audioCallbacksBlocked, 1);
		Volatile.Write(ref outputActive, 0);
		Volatile.Write(ref resetAudioThreadStateRequested, 0);
		Interlocked.Exchange(ref disposeAudioThreadStateRequested, 1);
		TryCompleteRequestedAudioThreadStateDisposal();
	}

	private bool TryCompleteRequestedAudioThreadStateDisposal()
	{
		if (Volatile.Read(ref audioCallbackDepth) != 0)
		{
			return false;
		}
		if (Interlocked.CompareExchange(ref disposeAudioThreadStateRequested, 0, 1) != 1)
		{
			return true;
		}
		DisposeAudioThreadState();
		return true;
	}

	private bool TryEnterAudioCallback()
	{
		if (Volatile.Read(ref audioCallbacksBlocked) != 0)
		{
			return false;
		}
		Interlocked.Increment(ref audioCallbackDepth);
		if (Volatile.Read(ref audioCallbacksBlocked) == 0)
		{
			return true;
		}
		ExitAudioCallback();
		return false;
	}

	private void ExitAudioCallback()
	{
		if (Interlocked.Decrement(ref audioCallbackDepth) == 0)
		{
			TryCompleteRequestedAudioThreadStateDisposal();
		}
	}

	private void OnAudioFilterRead(float[] data, int channels)
	{
		if (data == null)
		{
			return;
		}
		if (!TryEnterAudioCallback())
		{
			Array.Clear(data, 0, data.Length);
			return;
		}
		try
		{
			OnAudioFilterReadAdmitted(data, channels);
		}
		finally
		{
			ExitAudioCallback();
		}
	}

	private void OnAudioFilterReadAdmitted(float[] data, int channels)
	{
		try
		{
			int num = Volatile.Read(ref cachedOutputSampleRate);
			if (num <= 0 || channels <= 0)
			{
				Array.Clear(data, 0, data.Length);
				return;
			}
			if (Volatile.Read(ref outputActive) == 0)
			{
				ClearPendingFrames();
				Array.Clear(data, 0, data.Length);
				return;
			}
			if (Interlocked.Exchange(ref resetAudioThreadStateRequested, 0) != 0)
			{
				DisposeAudioThreadState();
			}
			if (Interlocked.Exchange(ref resetOutputConversionRequested, 0) != 0 || audioThreadOutputSampleRate != num || audioThreadOutputChannels != channels)
			{
				ResetOutputConversionState();
			}
			audioThreadOutputSampleRate = num;
			audioThreadOutputChannels = channels;
			float[] array = EnsureAudioBuffer(ref spatializationInputBuffer, data.Length, clearNewBuffer: false);
			Array.Copy(data, 0, array, 0, data.Length);
			DrainPendingFramesAudioThread();
			if (netEqPtr == IntPtr.Zero)
			{
				Array.Clear(data, 0, data.Length);
				return;
			}
			audioThreadOutputSampleRate = num;
			audioThreadOutputChannels = channels;
			FillOutputFifo(data.Length, num, channels);
			int num2 = outputFifo.Read(data, 0, data.Length);
			if (num2 < data.Length)
			{
				Array.Clear(data, num2, data.Length - num2);
			}
			ApplySpatializationMask(data, array, data.Length);
			CacheAudioThreadLatency();
		}
		catch (Exception exception)
		{
			RecordAudioThreadException(exception);
			DisposeAudioThreadState();
			Array.Clear(data, 0, data.Length);
		}
	}

	private void RecordAudioThreadException(Exception exception)
	{
	}

	private void DrainPendingFramesAudioThread()
	{
		int num = 0;
		long timestamp = Stopwatch.GetTimestamp();
		PendingFrame result;
		while (num < 32 && pendingFrames.TryDequeue(out result))
		{
			num++;
			try
			{
				if (TimestampAgeMs(result.ReceivedTimestamp, timestamp) <= 60 && EnsureNetEqFor(result.SampleRate, result.Channels, result.NetEqConfig))
				{
					float[] array;
					IntPtr orInit = netEqPacketBuffer.GetOrInit(result.SampleLength, out array);
					if (result.IsSilence)
					{
						netEqPacketBuffer.Zero();
					}
					else
					{
						netEqPacketBuffer.Fill(new ArraySegment<float>(result.Samples, 0, result.SampleLength));
					}
					NetEqInterop.InsertPacket(netEqPtr, result.SequenceNumber, result.Timestamp, orInit, result.SampleLength, (uint)result.SampleRate, 1, (uint)result.NetEqConfig.PacketDurationMs);
					Volatile.Write(ref receiveToInsertLatencyMs, TimestampAgeMs(result.ReceivedTimestamp, Stopwatch.GetTimestamp()));
				}
			}
			finally
			{
				availableFrames.Enqueue(result);
			}
		}
		if (netEqPtr != IntPtr.Zero)
		{
			Volatile.Write(ref currentBufferSizeMs, (int)NetEqInterop.CurrentBufferSizeMs(netEqPtr));
		}
	}

	private bool EnsureNetEqFor(int sampleRate, int channels, NetEqConfig config)
	{
		if (sampleRate != 48000 || channels != 1)
		{
			return false;
		}
		if (netEqPtr != IntPtr.Zero && netEqSampleRate == sampleRate && netEqChannels == channels && audioThreadNetEqConfig.Equals(config))
		{
			return true;
		}
		DisposeAudioThreadState();
		IntPtr intPtr = NetEqInterop.CreateNetEq((uint)sampleRate, 1, config.MaxPacketsInBuffer, (uint)config.MaxDelayMs, (uint)config.MinDelayMs, (uint)config.AdditionalDelayMs);
		if (intPtr == IntPtr.Zero)
		{
			return false;
		}
		netEqPtr = intPtr;
		netEqSampleRate = sampleRate;
		netEqChannels = 1;
		audioThreadNetEqConfig = config;
		return true;
	}

	private void FillOutputFifo(int requiredSamples, int outputSampleRate, int outputChannels)
	{
		if (requiredSamples <= 0 || outputSampleRate <= 0 || outputChannels <= 0)
		{
			return;
		}
		int num = 0;
		int num2 = Math.Clamp(Volatile.Read(ref cachedResamplerBufferMs), 10, 100);
		num2 = Math.Clamp((num2 + 5) / 10 * 10, 10, 100);
		int num3 = Math.Max(TenMsSamplesPerChannel(netEqSampleRate) * netEqChannels, netEqSampleRate * num2 / 1000 * netEqChannels);
		while (outputFifo.Count < requiredSamples && num < 32)
		{
			float[] array = EnsureAudioBuffer(ref netEqBatchBuffer, num3, clearNewBuffer: false);
			int num4 = 0;
			while (num4 < num3 && num < 32)
			{
				int num5 = TenMsSamplesPerChannel(netEqSampleRate) * netEqChannels;
				float[] array2;
				IntPtr orInit = netEqReadBuffer.GetOrInit(num5, out array2, initToZero: false);
				int audio = NetEqInterop.GetAudio(netEqPtr, orInit, num5);
				num++;
				if (audio < 0 || audio > num5)
				{
					throw new InvalidOperationException($"NetEQ returned {audio} samples for a buffer with capacity {num5}.");
				}
				if (audio == 0)
				{
					break;
				}
				netEqReadBuffer.ReadFromUnmanaged(audio);
				Array.Copy(array2, 0, array, num4, audio);
				num4 += audio;
				if (audio < num5)
				{
					break;
				}
			}
			if (num4 == 0)
			{
				break;
			}
			AppendToOutputFifo(array, num4, outputSampleRate, outputChannels);
		}
		CacheAudioThreadLatency();
	}

	private void AppendToOutputFifo(float[] input, int inputSampleCount, int outputSampleRate, int outputChannels)
	{
		float[] array = input;
		int num = inputSampleCount;
		int num2 = netEqChannels;
		if (num2 != 1 || inputSampleCount <= 0)
		{
			return;
		}
		if (netEqSampleRate != outputSampleRate)
		{
			if (netEqSampleRate <= 0 || outputSampleRate <= 0)
			{
				return;
			}
			int num3 = inputSampleCount / netEqChannels;
			int num4 = Math.Max(1, (int)Math.Ceiling((double)num3 * (double)outputSampleRate / (double)netEqSampleRate) + 8);
			int num5 = num4 * netEqChannels;
			float[] array2 = EnsureAudioBuffer(ref resamplerOutputBuffer, num5, clearNewBuffer: false);
			int in_len = num3;
			int out_len = num4;
			resampler.Configure(netEqChannels, netEqSampleRate, outputSampleRate, Volatile.Read(ref cachedResamplerQuality));
			resampler.ProcessInterleaved(MemoryExtensions.AsSpan(input, 0, inputSampleCount), ref in_len, MemoryExtensions.AsSpan(array2, 0, num5), ref out_len);
			if (in_len != num3)
			{
				throw new InvalidOperationException($"The resampler consumed {in_len} of {num3} input frames.");
			}
			array = array2;
			num = out_len * netEqChannels;
		}
		if (outputChannels == 1)
		{
			outputFifo.Write(array, 0, num);
			return;
		}
		int num6 = num / num2;
		int num7 = num6 * outputChannels;
		float[] array3 = EnsureAudioBuffer(ref channelConvertBuffer, num7, clearNewBuffer: false);
		int i = 0;
		int num8 = 0;
		for (; i < num6; i++)
		{
			float num9 = array[i];
			int num10 = 0;
			while (num10 < outputChannels)
			{
				array3[num8] = num9;
				num10++;
				num8++;
			}
		}
		outputFifo.Write(array3, 0, num7);
	}

	private static void ApplySpatializationMask(float[] output, float[] spatializationMask, int length)
	{
		int num = Math.Min(length, Math.Min(output.Length, spatializationMask.Length));
		for (int i = 0; i < num; i++)
		{
			output[i] *= spatializationMask[i];
		}
		if (num < length)
		{
			Array.Clear(output, num, length - num);
		}
	}

	private void DisposeAudioThreadState()
	{
		Exception ex = null;
		IntPtr intPtr = netEqPtr;
		netEqPtr = IntPtr.Zero;
		if (intPtr != IntPtr.Zero)
		{
			try
			{
				NetEqInterop.FreeNetEq(intPtr);
			}
			catch (Exception ex2)
			{
				ex = ex2;
			}
		}
		try
		{
			netEqPacketBuffer.Free();
		}
		catch (Exception ex3)
		{
			if (ex == null)
			{
				ex = ex3;
			}
		}
		try
		{
			netEqReadBuffer.Free();
		}
		catch (Exception ex4)
		{
			if (ex == null)
			{
				ex = ex4;
			}
		}
		try
		{
			resampler.Free();
		}
		catch (Exception ex5)
		{
			if (ex == null)
			{
				ex = ex5;
			}
		}
		outputFifo.Clear();
		netEqSampleRate = 0;
		netEqChannels = 0;
		audioThreadNetEqConfig = default(NetEqConfig);
		audioThreadOutputSampleRate = 0;
		audioThreadOutputChannels = 0;
		Volatile.Write(ref currentBufferSizeMs, 0);
		Volatile.Write(ref receiveToInsertLatencyMs, 0);
		Volatile.Write(ref localOutputBufferMs, 0);
		if (ex != null)
		{
			RecordAudioThreadException(ex);
		}
	}

	private void ResetOutputConversionState()
	{
		resampler.Free();
		outputFifo.Clear();
		Volatile.Write(ref localOutputBufferMs, 0);
	}

	private void CacheAudioThreadLatency()
	{
		int num = audioThreadOutputSampleRate;
		int num2 = audioThreadOutputChannels;
		int value = 0;
		if (num > 0 && num2 > 0)
		{
			value = outputFifo.Count / num2 * 1000 / num;
		}
		Volatile.Write(ref localOutputBufferMs, value);
		if (netEqPtr != IntPtr.Zero)
		{
			Volatile.Write(ref currentBufferSizeMs, (int)NetEqInterop.CurrentBufferSizeMs(netEqPtr));
		}
	}

	private void InitializePendingFramePool()
	{
		if (Volatile.Read(ref framePoolInitialized) != 0)
		{
			return;
		}
		lock (framePoolInitializationLock)
		{
			if (Volatile.Read(ref framePoolInitialized) == 0)
			{
				for (int i = 0; i < 32; i++)
				{
					availableFrames.Enqueue(new PendingFrame());
				}
				Volatile.Write(ref framePoolInitialized, 1);
			}
		}
	}

	private void ClearPendingFrames()
	{
		PendingFrame result;
		while (pendingFrames.TryDequeue(out result))
		{
			availableFrames.Enqueue(result);
		}
	}

	private int GetOldestPendingFrameAgeMs()
	{
		if (!pendingFrames.TryPeek(out var result))
		{
			return 0;
		}
		return TimestampAgeMs(result.ReceivedTimestamp, Stopwatch.GetTimestamp());
	}

	private void CacheMainThreadSettings()
	{
		int num = Volatile.Read(ref cachedOutputSampleRate);
		int num2 = Volatile.Read(ref cachedOutputChannels);
		int num3 = Volatile.Read(ref cachedDspBufferLength);
		int num4 = Volatile.Read(ref cachedDspBufferCount);
		int outputSampleRate = AudioSettings.outputSampleRate;
		Volatile.Write(ref cachedOutputSampleRate, outputSampleRate);
		Volatile.Write(ref cachedOutputChannels, GetSpeakerModeChannelCount(AudioSettings.speakerMode));
		AudioSettings.GetDSPBufferSize(out var bufferLength, out var numBuffers);
		Volatile.Write(ref cachedDspBufferLength, bufferLength);
		Volatile.Write(ref cachedDspBufferCount, numBuffers);
		int value = ((outputSampleRate > 0) ? (bufferLength * numBuffers * 1000 / outputSampleRate) : 0);
		Volatile.Write(ref cachedDspBufferMs, value);
		if (num != 0 && (num != outputSampleRate || num2 != Volatile.Read(ref cachedOutputChannels) || num3 != bufferLength || num4 != numBuffers))
		{
			Volatile.Write(ref resetOutputConversionRequested, 1);
			Volatile.Write(ref recreatePlaybackClipRequested, 1);
		}
		OnAudioFilterReadVcConfig onAudioFilterReadVcConfig = audioFilterReadConfig;
		CacheNetEqSettings(onAudioFilterReadVcConfig);
		Volatile.Write(ref cachedResamplerQuality, Math.Clamp((onAudioFilterReadVcConfig != null) ? onAudioFilterReadVcConfig.resamplerQuality : 4, 0, 10));
		Volatile.Write(ref cachedResamplerBufferMs, Math.Clamp((onAudioFilterReadVcConfig != null) ? onAudioFilterReadVcConfig.ResamplerBufferMs : 10, 10, 100));
	}

	private void CacheNetEqSettings(OnAudioFilterReadVcConfig config)
	{
		int maxPacketsInBuffer = Math.Max(1, (config != null) ? config.maxPacketsInBuffer : 50);
		int additionalDelayMs = Math.Max(0, (config != null) ? config.additionalDelayMs : 0);
		OnAudioFilterReadVcConfig.JitterBufferMode jitterBufferMode = ((!(config != null)) ? OnAudioFilterReadVcConfig.JitterBufferMode.Balanced : config.jitterBufferMode);
		int customMinDelayMs = Math.Max(0, (config != null) ? config.customMinDelayMs : 0);
		int customMaxDelayMs = Math.Max(0, (config != null) ? config.customMaxDelayMs : 0);
		if (!Volatile.Read(ref cachedNetEqSettings).Matches(maxPacketsInBuffer, additionalDelayMs, jitterBufferMode, customMinDelayMs, customMaxDelayMs))
		{
			NetEqSettingsSnapshot value = new NetEqSettingsSnapshot(maxPacketsInBuffer, additionalDelayMs, jitterBufferMode, customMinDelayMs, customMaxDelayMs);
			Volatile.Write(ref cachedNetEqSettings, value);
		}
	}

	private NetEqConfig GetCachedNetEqConfig(int samplesPerChannel, int sampleRate)
	{
		NetEqSettingsSnapshot netEqSettingsSnapshot = Volatile.Read(ref cachedNetEqSettings);
		int num = Math.Max(1, (int)Math.Round((double)samplesPerChannel * 1000.0 / (double)sampleRate));
		int val;
		int val2;
		if (netEqSettingsSnapshot.JitterBufferMode == OnAudioFilterReadVcConfig.JitterBufferMode.Custom)
		{
			val = netEqSettingsSnapshot.CustomMinDelayMs;
			val2 = netEqSettingsSnapshot.CustomMaxDelayMs;
		}
		else
		{
			val = OnAudioFilterReadVcConfig.GetMinDelayMs(num, netEqSettingsSnapshot.JitterBufferMode);
			val2 = OnAudioFilterReadVcConfig.GetMaxDelayMs(num, netEqSettingsSnapshot.JitterBufferMode);
		}
		val = Math.Max(0, val);
		val2 = Math.Max(val, val2);
		return new NetEqConfig(num, netEqSettingsSnapshot.MaxPacketsInBuffer, val2, val, netEqSettingsSnapshot.AdditionalDelayMs);
	}

	private void CreatePlaybackClip(bool forceRecreate = false)
	{
		int num = Math.Max(1, Volatile.Read(ref cachedOutputSampleRate));
		if (!forceRecreate && playbackClip != null && playbackClip.frequency == num && playbackClip.channels == 1)
		{
			audioSource.clip = playbackClip;
			return;
		}
		int num2;
		if (Volatile.Read(ref outputActive) != 0 && audioSource != null)
		{
			num2 = (audioSource.isPlaying ? 1 : 0);
			if (num2 != 0)
			{
				audioSource.Stop();
			}
		}
		else
		{
			num2 = 0;
		}
		if (playbackClip != null)
		{
			UnityEngine.Object.Destroy(playbackClip);
		}
		playbackClip = AudioClip.Create("OnAudioFilterReadVcOutput", num, 1, num, stream: false);
		FillPlaybackClip(playbackClip);
		audioSource.clip = playbackClip;
		if (num2 != 0)
		{
			audioSource.Play();
		}
	}

	private void FillPlaybackClip(AudioClip clip)
	{
		int num = clip.samples * clip.channels;
		if (playbackClipSamples.Length != num)
		{
			playbackClipSamples = new float[num];
		}
		for (int i = 0; i < playbackClipSamples.Length; i++)
		{
			playbackClipSamples[i] = 1f;
		}
		clip.SetData(playbackClipSamples, 0);
	}

	private static void ConfigureAudioSource(UnityEngine.AudioSource source)
	{
		source.playOnAwake = false;
		source.loop = true;
		source.pitch = 1f;
		source.dopplerLevel = 0f;
		source.bypassReverbZones = true;
		source.reverbZoneMix = 0f;
		source.spatializePostEffects = false;
	}

	private void RestartPlaybackIfNeeded()
	{
		if (!(audioSource == null) && !audioSource.isPlaying)
		{
			if (audioSource.clip == null)
			{
				CreatePlaybackClip();
			}
			audioSource.Play();
		}
	}

	private static int GetSpeakerModeChannelCount(AudioSpeakerMode speakerMode)
	{
		return speakerMode switch
		{
			AudioSpeakerMode.Mono => 1, 
			AudioSpeakerMode.Stereo => 2, 
			AudioSpeakerMode.Quad => 4, 
			AudioSpeakerMode.Surround => 5, 
			AudioSpeakerMode.Mode5point1 => 6, 
			AudioSpeakerMode.Mode7point1 => 8, 
			AudioSpeakerMode.Prologic => 2, 
			_ => 2, 
		};
	}

	private static bool IsValidFrameShape(int frameSize, int inputSampleRate, int inputChannels)
	{
		if (frameSize <= 0 || inputSampleRate != 48000 || inputChannels != 1 || frameSize % inputChannels != 0)
		{
			return false;
		}
		int num = frameSize / inputChannels;
		int num2 = num * 1000 / inputSampleRate;
		if (num * 1000 == num2 * inputSampleRate)
		{
			if (num2 != 10 && num2 != 20)
			{
				return num2 == 40;
			}
			return true;
		}
		return false;
	}

	private static bool IsNativeNetEqSupportedPlatform()
	{
		return IntPtr.Size == 8;
	}

	private static int TenMsSamplesPerChannel(int sampleRate)
	{
		return Math.Max(1, (int)Math.Round((double)sampleRate * 0.01));
	}

	private static float[] EnsureAudioBuffer(ref float[] buffer, int requiredLength, bool clearNewBuffer)
	{
		if (buffer.Length >= requiredLength)
		{
			return buffer;
		}
		buffer = new float[requiredLength];
		if (clearNewBuffer)
		{
			Array.Clear(buffer, 0, buffer.Length);
		}
		return buffer;
	}

	private static int TimestampAgeMs(long timestamp, long now)
	{
		if (timestamp <= 0 || now <= timestamp)
		{
			return 0;
		}
		double num = (double)(now - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
		if (!(num >= 2147483647.0))
		{
			return (int)Math.Round(num);
		}
		return int.MaxValue;
	}

	private static int NextPowerOfTwo(int value)
	{
		value = Math.Max(1, value);
		value--;
		value |= value >> 1;
		value |= value >> 2;
		value |= value >> 4;
		value |= value >> 8;
		value |= value >> 16;
		return value + 1;
	}

	protected override void ReceiveFrame(int index, float[] samples, float targetLatency)
	{
		VcConfig vcConfig = ((metaVc != null) ? metaVc.config : null);
		if (vcConfig != null && vcConfig.samplesPerFrame > 0)
		{
			if (!_player.Owner.IsLocalClient)
			{
				_player.Mouth.SetSamples(samples);
			}
			int samplesPerFrame = vcConfig.samplesPerFrame;
			long num = Math.Max(index, 0);
			ushort sequenceNumber = (ushort)((ulong)num % 65536uL);
			uint timestamp = (uint)((ulong)(num * samplesPerFrame) % 4294967296uL);
			Span<float> frame = ((samples == null) ? Span<float>.Empty : MemoryExtensions.AsSpan(samples));
			Process(frame, samplesPerFrame, 48000, 1, sequenceNumber, timestamp);
		}
	}
}
