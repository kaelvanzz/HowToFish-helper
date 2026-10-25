namespace FishNet.Managing.Timing;

public class EstimatedTick
{
	public enum OldTickOption : byte
	{
		Discard,
		SetLastRemoteTick,
		SetRemoteTick
	}

	private TimeManager _updateTimeManager;

	private uint _valueLocalTick;

	public uint LocalTick { get; private set; }

	public uint RemoteTick { get; private set; }

	public uint LastRemoteTick { get; private set; }

	public bool IsLastRemoteTickOrdered => LastRemoteTick == RemoteTick;

	public bool IsUnset => LocalTick == 0;

	public uint LocalTickDifference(TimeManager tm = null)
	{
		if (!TryAssignTimeManager(ref tm))
		{
			return 0u;
		}
		long num = tm.LocalTick - LocalTick;
		if (num < 0)
		{
			return 0u;
		}
		if (num > uint.MaxValue)
		{
			num = 4294967295L;
		}
		return (uint)num;
	}

	public bool IsCurrent(TimeManager tm = null)
	{
		if (!TryAssignTimeManager(ref tm))
		{
			return false;
		}
		if (!IsUnset)
		{
			return LocalTick == tm.LocalTick;
		}
		return false;
	}

	public uint Value(TimeManager tm = null)
	{
		if (!TryAssignTimeManager(ref tm))
		{
			return 0u;
		}
		bool isCurrent;
		return Value(out isCurrent, tm);
	}

	public uint Value(out bool isCurrent, TimeManager tm = null)
	{
		isCurrent = false;
		if (!TryAssignTimeManager(ref tm))
		{
			return 0u;
		}
		if (IsUnset)
		{
			return 0u;
		}
		isCurrent = IsCurrent(tm);
		return tm.LocalTick - _valueLocalTick + RemoteTick;
	}

	public void Initialize(TimeManager tm, uint remoteTick = 0u, uint lastRemoteTick = 0u, uint localTick = 0u)
	{
		_updateTimeManager = tm;
		RemoteTick = remoteTick;
		LastRemoteTick = lastRemoteTick;
		LocalTick = localTick;
	}

	public bool Update(TimeManager tm, uint remoteTick, OldTickOption oldTickOption = OldTickOption.Discard, bool resetValue = true)
	{
		_updateTimeManager = tm;
		LastRemoteTick = remoteTick;
		if (oldTickOption != OldTickOption.SetRemoteTick && remoteTick <= RemoteTick)
		{
			return false;
		}
		LocalTick = tm.LocalTick;
		if (resetValue)
		{
			_valueLocalTick = LocalTick;
		}
		RemoteTick = remoteTick;
		return true;
	}

	public bool Update(uint remoteTick, OldTickOption oldTickOption = OldTickOption.Discard, bool resetValue = true)
	{
		TimeManager tm = null;
		if (!TryAssignTimeManager(ref tm))
		{
			return false;
		}
		return Update(tm, remoteTick, oldTickOption);
	}

	public void UpdateValue()
	{
		_valueLocalTick = LocalTick;
	}

	private bool TryAssignTimeManager(ref TimeManager tm)
	{
		if (tm == null)
		{
			tm = _updateTimeManager;
		}
		return tm != null;
	}

	public void Reset()
	{
		ResetTicks();
		_updateTimeManager = null;
	}

	public void ResetTicks()
	{
		LocalTick = 0u;
		RemoteTick = 0u;
		LastRemoteTick = 0u;
		_valueLocalTick = 0u;
	}
}
