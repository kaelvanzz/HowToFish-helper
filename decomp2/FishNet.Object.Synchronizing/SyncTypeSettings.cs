using System;
using FishNet.Transporting;

namespace FishNet.Object.Synchronizing;

[Serializable]
public struct SyncTypeSettings
{
	public WritePermission WritePermission;

	public ReadPermission ReadPermission;

	public float SendRate;

	public Channel Channel;

	internal bool IsDefault()
	{
		if (WritePermission == WritePermission.ServerOnly && ReadPermission == ReadPermission.Observers && SendRate == 0f)
		{
			return Channel == Channel.Reliable;
		}
		return false;
	}

	public SyncTypeSettings(float sendRate = 0.1f)
	{
		WritePermission = WritePermission.ServerOnly;
		ReadPermission = ReadPermission.Observers;
		SendRate = sendRate;
		Channel = Channel.Reliable;
	}

	public SyncTypeSettings(float sendRate, Channel channel)
	{
		WritePermission = WritePermission.ServerOnly;
		ReadPermission = ReadPermission.Observers;
		SendRate = sendRate;
		Channel = channel;
	}

	public SyncTypeSettings(Channel channel)
	{
		WritePermission = WritePermission.ServerOnly;
		ReadPermission = ReadPermission.Observers;
		SendRate = 0.1f;
		Channel = channel;
	}

	public SyncTypeSettings(WritePermission writePermissions)
	{
		WritePermission = writePermissions;
		ReadPermission = ReadPermission.Observers;
		SendRate = 0.1f;
		Channel = Channel.Reliable;
	}

	public SyncTypeSettings(ReadPermission readPermissions)
	{
		WritePermission = WritePermission.ServerOnly;
		ReadPermission = readPermissions;
		SendRate = 0.1f;
		Channel = Channel.Reliable;
	}

	public SyncTypeSettings(WritePermission writePermissions, ReadPermission readPermissions)
	{
		WritePermission = writePermissions;
		ReadPermission = readPermissions;
		SendRate = 0.1f;
		Channel = Channel.Reliable;
	}

	public SyncTypeSettings(WritePermission writePermissions, ReadPermission readPermissions, float sendRate, Channel channel)
	{
		WritePermission = writePermissions;
		ReadPermission = readPermissions;
		SendRate = sendRate;
		Channel = channel;
	}
}
