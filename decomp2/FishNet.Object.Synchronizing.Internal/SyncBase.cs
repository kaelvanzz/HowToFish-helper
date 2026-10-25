using System;
using FishNet.CodeGenerating;
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Serializing;
using FishNet.Transporting;
using UnityEngine;

namespace FishNet.Object.Synchronizing.Internal;

public class SyncBase
{
	[MakePublic]
	internal SyncTypeSettings Settings;

	public NetworkManager NetworkManager;

	public NetworkBehaviour NetworkBehaviour;

	[MakePublic]
	internal uint NextSyncTick;

	private uint _timeToTicks;

	private Channel _currentChannel;

	private ushort _lastReadChangeId;

	private ushort _lastWrittenChangeId;

	private const ushort UNSET_CHANGE_ID = 0;

	private const ushort MAXIMUM_CHANGE_ID = ushort.MaxValue;

	public bool IsInitialized { get; private set; }

	public bool IsNetworkInitialized
	{
		get
		{
			if (IsInitialized)
			{
				if (!NetworkBehaviour.IsServerInitialized)
				{
					return NetworkBehaviour.IsClientInitialized;
				}
				return true;
			}
			return false;
		}
	}

	public bool IsSyncObject { get; private set; }

	[MakePublic]
	internal float SendRate => Settings.SendRate;

	public bool IsDirty { get; private set; }

	public bool OnStartServerCalled { get; private set; }

	public bool OnStartClientCalled { get; private set; }

	public uint SyncIndex { get; protected set; }

	internal Channel Channel => _currentChannel;

	internal void SetCurrentChannel(Channel channel)
	{
		_currentChannel = channel;
	}

	public SyncBase()
		: this(default(SyncTypeSettings))
	{
	}

	public SyncBase(SyncTypeSettings settings)
	{
		Settings = settings;
	}

	public void UpdateSettings(SyncTypeSettings settings)
	{
		Settings = settings;
		SetTimeToTicks();
	}

	public void UpdatePermissions(WritePermission writePermissions, ReadPermission readPermissions)
	{
		UpdatePermissions(writePermissions);
		UpdatePermissions(readPermissions);
	}

	public void UpdatePermissions(WritePermission writePermissions)
	{
		Settings.WritePermission = writePermissions;
	}

	public void UpdatePermissions(ReadPermission readPermissions)
	{
		Settings.ReadPermission = readPermissions;
	}

	public void UpdateSendRate(float sendRate)
	{
		Settings.SendRate = sendRate;
		SetTimeToTicks();
	}

	public void UpdateSettings(Channel channel)
	{
		CheckChannel(ref channel);
		_currentChannel = channel;
	}

	public void UpdateSettings(WritePermission writePermissions, ReadPermission readPermissions, float sendRate, Channel channel)
	{
		CheckChannel(ref channel);
		_currentChannel = channel;
		Settings = new SyncTypeSettings(writePermissions, readPermissions, sendRate, channel);
		SetTimeToTicks();
	}

	private void CheckChannel(ref Channel c)
	{
		if (c == Channel.Unreliable && IsSyncObject)
		{
			c = Channel.Reliable;
			string message = "Channel cannot be unreliable for SyncObjects. Channel has been changed to reliable.";
			NetworkManager.LogWarning(message);
		}
	}

	[MakePublic]
	public void InitializeEarly(NetworkBehaviour nb, uint syncIndex, bool isSyncObject)
	{
		NetworkBehaviour = nb;
		SyncIndex = syncIndex;
		IsSyncObject = isSyncObject;
		NetworkBehaviour.RegisterSyncType(this, SyncIndex);
	}

	[MakePublic]
	public void InitializeLate()
	{
		Initialized();
	}

	protected virtual void Initialized()
	{
		IsInitialized = true;
	}

	[MakePublic]
	public void PreInitialize(NetworkManager networkManager, bool asServer)
	{
		NetworkManager = networkManager;
		if (Settings.IsDefault())
		{
			float sendRate = Mathf.Max(networkManager.ServerManager.GetSyncTypeRate(), (float)networkManager.TimeManager.TickDelta);
			Settings = new SyncTypeSettings(sendRate);
		}
		SetTimeToTicks();
	}

	private void SetTimeToTicks()
	{
		if (!(NetworkManager == null))
		{
			_timeToTicks = NetworkManager.TimeManager.TimeToTicks(Settings.SendRate, TickRounding.RoundUp);
		}
	}

	[MakePublic]
	public virtual void OnStartCallback(bool asServer)
	{
		if (asServer)
		{
			OnStartServerCalled = true;
		}
		else
		{
			OnStartClientCalled = true;
		}
	}

	[MakePublic]
	public virtual void OnStopCallback(bool asServer)
	{
		if (asServer)
		{
			OnStartServerCalled = false;
		}
		else
		{
			OnStartClientCalled = false;
		}
	}

	protected bool CanNetworkSetValues(bool log = true)
	{
		if (!IsInitialized)
		{
			return true;
		}
		if (!IsNetworkInitialized)
		{
			return true;
		}
		if (NetworkBehaviour.IsServerStarted)
		{
			return true;
		}
		bool num = Settings.WritePermission == WritePermission.ClientUnsynchronized || (Settings.ReadPermission == ReadPermission.ExcludeOwner && NetworkBehaviour.IsOwner);
		if (!num && log)
		{
			LogServerNotActiveWarning();
		}
		return num;
	}

	protected void LogServerNotActiveWarning()
	{
		if (NetworkManager != null)
		{
			NetworkManager.LogWarning("Cannot complete operation as server when server is not active. You can disable this warning by setting WritePermissions to " + WritePermission.ClientUnsynchronized.ToString() + ".");
		}
	}

	protected bool Dirty()
	{
		_currentChannel = Settings.Channel;
		bool flag = NetworkBehaviour.DirtySyncType();
		IsDirty |= flag;
		return flag;
	}

	protected bool CanInvokeCallbackAsServer()
	{
		if (IsNetworkInitialized)
		{
			return NetworkBehaviour.IsServerStarted;
		}
		return true;
	}

	protected virtual bool ReadChangeId(Reader reader)
	{
		if (NetworkManager == null)
		{
			NetworkManager.LogWarning("NetworkManager is unexpectedly null during a SyncType read.");
			return false;
		}
		bool flag = reader.ReadBoolean();
		ushort num = reader.ReadUInt16();
		if (_lastReadChangeId != 0)
		{
			if (flag)
			{
				if (num >= _lastReadChangeId)
				{
					return false;
				}
			}
			else if (num <= _lastReadChangeId)
			{
				return false;
			}
		}
		_lastReadChangeId = num;
		return true;
	}

	protected virtual void WriteChangeId(PooledWriter writer)
	{
		bool value;
		if (_lastWrittenChangeId >= ushort.MaxValue)
		{
			value = true;
			_lastWrittenChangeId = 0;
		}
		else
		{
			value = false;
		}
		_lastWrittenChangeId++;
		writer.WriteBoolean(value);
		writer.WriteUInt16(_lastWrittenChangeId);
	}

	protected bool IsReadAsClientHost(bool asServer)
	{
		if (!asServer)
		{
			return NetworkManager.IsServerStarted;
		}
		return false;
	}

	protected bool CanReset(bool asServer)
	{
		bool flag = IsNetworkInitialized && NetworkManager.IsClientStarted;
		if (!asServer || flag)
		{
			if (!asServer)
			{
				return NetworkBehaviour.IsDeinitializing;
			}
			return false;
		}
		return true;
	}

	protected void SetReadArguments(PooledReader reader, bool asServer, out bool newChangeId, out bool asClientHost, out bool canModifyValues)
	{
		newChangeId = ReadChangeId(reader);
		asClientHost = IsReadAsClientHost(asServer);
		canModifyValues = newChangeId && !asClientHost;
	}

	internal void ResetDirty()
	{
		if (!IsSyncObject && Settings.Channel == Channel.Unreliable)
		{
			if (_currentChannel == Channel.Unreliable)
			{
				_currentChannel = Channel.Reliable;
			}
			else
			{
				IsDirty = false;
			}
		}
		else
		{
			IsDirty = false;
		}
	}

	internal bool IsNextSyncTimeMet(uint tick)
	{
		if (IsDirty)
		{
			return tick >= NextSyncTick;
		}
		return false;
	}

	[Obsolete("Use IsNextSyncTimeMet.")]
	internal bool SyncTimeMet(uint tick)
	{
		return IsNextSyncTimeMet(tick);
	}

	[MakePublic]
	public virtual void WriteDelta(PooledWriter writer, bool resetSyncTick = true)
	{
		WriteHeader(writer, resetSyncTick);
	}

	protected virtual void WriteHeader(PooledWriter writer, bool resetSyncTick = true)
	{
		if (resetSyncTick)
		{
			NextSyncTick = NetworkManager.TimeManager.LocalTick + _timeToTicks;
		}
		writer.WriteUInt8Unpacked((byte)SyncIndex);
		WriteChangeId(writer);
	}

	[Obsolete("This method no longer functions. You may remove it from your code.")]
	protected void FullWritten()
	{
	}

	[MakePublic]
	public virtual void WriteFull(PooledWriter writer)
	{
	}

	[MakePublic]
	public virtual void Read(PooledReader reader, bool asServer)
	{
	}

	protected internal virtual void ResetState()
	{
		ResetState(asServer: true);
		ResetState(asServer: false);
	}

	[MakePublic]
	public virtual void ResetState(bool asServer)
	{
		if (asServer)
		{
			NextSyncTick = 0u;
			SetCurrentChannel(Settings.Channel);
			IsDirty = false;
		}
		_lastReadChangeId = 0;
	}
}
