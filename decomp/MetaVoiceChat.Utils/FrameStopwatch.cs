using System.Diagnostics;
using UnityEngine;

namespace MetaVoiceChat.Utils;

public class FrameStopwatch
{
	private readonly Stopwatch stopwatch = new Stopwatch();

	private int currentFrame = -1;

	private int warningFrame = -1;

	public void Start()
	{
		if (Time.frameCount != currentFrame)
		{
			currentFrame = Time.frameCount;
			stopwatch.Restart();
		}
		else
		{
			stopwatch.Start();
		}
	}

	public void Stop(float warningMs, string warningMessage, bool shouldReset, bool allowMultipleWarningsPerFrame)
	{
		if (shouldReset)
		{
			Reset();
			return;
		}
		stopwatch.Stop();
		float num = (float)stopwatch.Elapsed.TotalMilliseconds;
		if (!(num < warningMs) && (currentFrame != warningFrame || allowMultipleWarningsPerFrame))
		{
			UnityEngine.Debug.LogWarning($"{warningMessage}\nElapsed ms of {num} is greater than warning ms of {warningMs} on frame {currentFrame}.");
			warningFrame = currentFrame;
		}
	}

	public void Reset()
	{
		stopwatch.Stop();
		stopwatch.Reset();
		currentFrame = -1;
		warningFrame = -1;
	}
}
