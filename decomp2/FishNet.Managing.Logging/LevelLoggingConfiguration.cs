using System;
using System.Text;
using FishNet.Documenting;
using FishNet.Managing.Timing;
using UnityEngine;

namespace FishNet.Managing.Logging;

[CreateAssetMenu(fileName = "New LevelLoggingConfiguration", menuName = "FishNet/Logging/Level Logging Configuration")]
public class LevelLoggingConfiguration : LoggingConfiguration
{
	[Tooltip("True to add localtick to logs.")]
	[SerializeField]
	private bool _addLocalTick;

	[Tooltip("True to add timestamps to logs.")]
	[SerializeField]
	private bool _addTimestamps = true;

	[Tooltip("True to add timestamps when in editor. False to only include timestamps in builds.")]
	[SerializeField]
	private bool _enableTimestampsInEditor;

	[Tooltip("Type of logging to use for development builds and editor.")]
	[SerializeField]
	private LoggingType _developmentLogging = LoggingType.Common;

	[Tooltip("Type of logging to use for GUI builds.")]
	[SerializeField]
	private LoggingType _guiLogging = LoggingType.Warning;

	[Tooltip("Type of logging to use for headless builds.")]
	[SerializeField]
	private LoggingType _headlessLogging = LoggingType.Error;

	private bool _initialized;

	private LoggingType _highestLoggingType;

	private static StringBuilder _stringBuilder = new StringBuilder();

	[APIExclude]
	public void LoggingConstructor(bool loggingEnabled, LoggingType development, LoggingType gui, LoggingType headless)
	{
		IsEnabled = loggingEnabled;
		_developmentLogging = development;
		_guiLogging = gui;
		_headlessLogging = headless;
	}

	public override void InitializeOnce()
	{
		byte val = 0;
		val = Math.Max(val, (byte)_guiLogging);
		_highestLoggingType = (LoggingType)val;
		_initialized = true;
	}

	public override bool CanLog(LoggingType loggingType)
	{
		if (!IsEnabled)
		{
			return false;
		}
		if (!_initialized)
		{
			return false;
		}
		return (int)loggingType <= (int)_highestLoggingType;
	}

	public override void Log(string value)
	{
		if (CanLog(LoggingType.Common))
		{
			Debug.Log(AddSettingsToLog(value));
		}
	}

	public override void LogWarning(string value)
	{
		if (CanLog(LoggingType.Warning))
		{
			Debug.LogWarning(AddSettingsToLog(value));
		}
	}

	public override void LogError(string value)
	{
		if (CanLog(LoggingType.Error))
		{
			Debug.LogError(AddSettingsToLog(value));
		}
	}

	public override LoggingConfiguration Clone()
	{
		LevelLoggingConfiguration levelLoggingConfiguration = ScriptableObject.CreateInstance<LevelLoggingConfiguration>();
		levelLoggingConfiguration.LoggingConstructor(IsEnabled, _developmentLogging, _guiLogging, _headlessLogging);
		levelLoggingConfiguration._addTimestamps = _addTimestamps;
		levelLoggingConfiguration._addLocalTick = _addLocalTick;
		levelLoggingConfiguration._enableTimestampsInEditor = _enableTimestampsInEditor;
		return levelLoggingConfiguration;
	}

	private string AddSettingsToLog(string value)
	{
		_stringBuilder.Clear();
		if (_addTimestamps && (!Application.isEditor || _enableTimestampsInEditor))
		{
			_stringBuilder.Append($"[{DateTime.Now:yyyy.MM.dd HH:mm:ss}] ");
		}
		if (_addLocalTick)
		{
			TimeManager timeManager = InstanceFinder.TimeManager;
			uint num = ((!(timeManager == null)) ? timeManager.LocalTick : 0u);
			_stringBuilder.Append($"LocalTick [{num}] ");
		}
		if (_stringBuilder.Length > 0)
		{
			_stringBuilder.Append(value);
			value = _stringBuilder.ToString();
		}
		return value;
	}
}
