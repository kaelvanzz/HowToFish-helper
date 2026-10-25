using System.Collections.Generic;
using FishNet.Documenting;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

public class SyncTimer : SyncBase, ICustomSync
{
	private struct ChangeData
	{
		public readonly SyncTimerOperation Operation;

		public readonly float Previous;

		public readonly float Next;

		public ChangeData(SyncTimerOperation operation, float previous, float next)
		{
			Operation = operation;
			Previous = previous;
			Next = next;
		}
	}

	public delegate void SyncTypeChanged(SyncTimerOperation op, float prev, float next, bool asServer);

	private List<ChangeData> _changed = new List<ChangeData>();

	private List<ChangeData> _serverOnChanges = new List<ChangeData>();

	private List<ChangeData> _clientOnChanges = new List<ChangeData>();

	private float _updateTime;

	public float Remaining { get; private set; }

	public float Elapsed => Duration - Remaining;

	public float Duration { get; private set; }

	public bool Paused { get; private set; }

	public event SyncTypeChanged OnChange;

	public SyncTimer(SyncTypeSettings settings = default(SyncTypeSettings))
		: base(settings)
	{
	}

	protected override void Initialized()
	{
		base.Initialized();
	}

	public void StartTimer(float remaining, bool sendRemainingOnStop = true)
	{
		if (CanNetworkSetValues())
		{
			if (Remaining > 0f)
			{
				StopTimer(sendRemainingOnStop);
			}
			Paused = false;
			Remaining = remaining;
			Duration = remaining;
			SetUpdateTime();
			AddOperation(SyncTimerOperation.Start, -1f, remaining);
		}
	}

	public void PauseTimer(bool sendRemaining = false)
	{
		if (!(Remaining <= 0f) && !Paused && CanNetworkSetValues())
		{
			Paused = true;
			SyncTimerOperation operation = (sendRemaining ? SyncTimerOperation.PauseUpdated : SyncTimerOperation.Pause);
			AddOperation(operation, Remaining, Remaining);
		}
	}

	public void UnpauseTimer()
	{
		if (!(Remaining <= 0f) && Paused && CanNetworkSetValues())
		{
			Paused = false;
			SetUpdateTime();
			AddOperation(SyncTimerOperation.Unpause, Remaining, Remaining);
		}
	}

	public void StopTimer(bool sendRemaining = false)
	{
		if (!(Remaining <= 0f) && CanNetworkSetValues())
		{
			bool asServer = true;
			float remaining = Remaining;
			StopTimer_Internal(asServer);
			SyncTimerOperation operation = (sendRemaining ? SyncTimerOperation.StopUpdated : SyncTimerOperation.Stop);
			AddOperation(operation, remaining, 0f);
		}
	}

	private void AddOperation(SyncTimerOperation operation, float prev, float next)
	{
		if (base.IsInitialized)
		{
			bool flag = !base.IsNetworkInitialized || NetworkBehaviour.IsServerStarted;
			if (flag && Dirty())
			{
				ChangeData item = new ChangeData(operation, prev, next);
				_changed.Add(item);
			}
			this.OnChange?.Invoke(operation, prev, next, flag);
		}
	}

	protected internal override void WriteDelta(PooledWriter writer, bool resetSyncTick = true)
	{
		base.WriteDelta(writer, resetSyncTick);
		writer.WriteInt32(_changed.Count);
		for (int i = 0; i < _changed.Count; i++)
		{
			ChangeData changeData = _changed[i];
			writer.WriteUInt8Unpacked((byte)changeData.Operation);
			if (changeData.Operation == SyncTimerOperation.Start)
			{
				WriteStartTimer(writer, includeOperationByte: false);
			}
			else if (changeData.Operation == SyncTimerOperation.PauseUpdated || changeData.Operation == SyncTimerOperation.StopUpdated)
			{
				writer.WriteSingle(changeData.Next);
			}
		}
		_changed.Clear();
	}

	protected internal override void WriteFull(PooledWriter writer)
	{
		if (!(Remaining <= 0f))
		{
			base.WriteDelta(writer, resetSyncTick: false);
			int value = ((!Paused) ? 1 : 2);
			writer.WriteInt32(value);
			WriteStartTimer(writer, includeOperationByte: true);
			if (Paused)
			{
				writer.WriteUInt8Unpacked(2);
			}
		}
	}

	private void WriteStartTimer(Writer w, bool includeOperationByte)
	{
		if (includeOperationByte)
		{
			w.WriteUInt8Unpacked(1);
		}
		w.WriteSingle(Remaining);
		w.WriteSingle(Duration);
	}

	[APIExclude]
	protected internal override void Read(PooledReader reader, bool asServer)
	{
		SetReadArguments(reader, asServer, out var newChangeId, out var _, out var canModifyValues);
		int num = reader.ReadInt32();
		float? num2 = null;
		for (int i = 0; i < num; i++)
		{
			SyncTimerOperation syncTimerOperation = (SyncTimerOperation)reader.ReadUInt8Unpacked();
			switch (syncTimerOperation)
			{
			case SyncTimerOperation.Start:
			{
				float num3 = reader.ReadSingle();
				float num4 = reader.ReadSingle();
				if (canModifyValues)
				{
					SetUpdateTime();
					Paused = false;
					Remaining = num3;
					Duration = num4;
				}
				if (newChangeId)
				{
					InvokeOnChange(syncTimerOperation, -1f, num3, asServer);
					if (num3 == 0f)
					{
						num2 = num4;
					}
				}
				break;
			}
			case SyncTimerOperation.Pause:
			case SyncTimerOperation.PauseUpdated:
			case SyncTimerOperation.Unpause:
				if (canModifyValues)
				{
					UpdatePauseState(syncTimerOperation);
				}
				break;
			case SyncTimerOperation.Stop:
			{
				float remaining2 = Remaining;
				if (canModifyValues)
				{
					StopTimer_Internal(asServer);
				}
				if (newChangeId)
				{
					InvokeOnChange(syncTimerOperation, remaining2, 0f, asServer: false);
				}
				break;
			}
			case SyncTimerOperation.StopUpdated:
			{
				float remaining = Remaining;
				float next = reader.ReadSingle();
				if (canModifyValues)
				{
					StopTimer_Internal(asServer);
				}
				if (newChangeId)
				{
					InvokeOnChange(syncTimerOperation, remaining, next, asServer);
				}
				break;
			}
			}
		}
		if (newChangeId && num > 0)
		{
			InvokeOnChange(SyncTimerOperation.Complete, -1f, -1f, asServer: false);
		}
		if (num2.HasValue)
		{
			InvokeFinished(num2.Value);
		}
		void UpdatePauseState(SyncTimerOperation op)
		{
			bool paused = op == SyncTimerOperation.Pause || op == SyncTimerOperation.PauseUpdated;
			float remaining3 = Remaining;
			float next2 = ((op != SyncTimerOperation.PauseUpdated) ? Remaining : (Remaining = reader.ReadSingle()));
			Paused = paused;
			if (!Paused)
			{
				SetUpdateTime();
			}
			if (newChangeId)
			{
				InvokeOnChange(op, remaining3, next2, asServer);
			}
		}
	}

	private void StopTimer_Internal(bool asServer)
	{
		Paused = false;
		Remaining = 0f;
	}

	private void InvokeOnChange(SyncTimerOperation operation, float prev, float next, bool asServer)
	{
		if (asServer)
		{
			if (NetworkBehaviour.OnStartServerCalled)
			{
				this.OnChange?.Invoke(operation, prev, next, asServer);
			}
			else
			{
				_serverOnChanges.Add(new ChangeData(operation, prev, next));
			}
		}
		else if (NetworkBehaviour.OnStartClientCalled)
		{
			this.OnChange?.Invoke(operation, prev, next, asServer);
		}
		else
		{
			_clientOnChanges.Add(new ChangeData(operation, prev, next));
		}
	}

	protected internal override void OnStartCallback(bool asServer)
	{
		base.OnStartCallback(asServer);
		List<ChangeData> list = (asServer ? _serverOnChanges : _clientOnChanges);
		if (this.OnChange != null)
		{
			foreach (ChangeData item in list)
			{
				this.OnChange(item.Operation, item.Previous, item.Next, asServer);
			}
		}
		list.Clear();
	}

	private void SetUpdateTime()
	{
		_updateTime = Time.unscaledTime;
	}

	public void Update()
	{
		float delta = Time.unscaledTime - _updateTime;
		Update(delta);
	}

	public void Update(float delta)
	{
		if (!(Remaining <= 0f) && !Paused)
		{
			SetUpdateTime();
			if (delta < 0f)
			{
				delta *= -1f;
			}
			float remaining = Remaining;
			Remaining -= delta;
			if (!(Remaining > 0f))
			{
				Remaining = 0f;
				InvokeFinished(remaining);
			}
		}
	}

	private void InvokeFinished(float prev)
	{
		if (NetworkManager.IsServerStarted)
		{
			this.OnChange?.Invoke(SyncTimerOperation.Finished, prev, 0f, asServer: true);
		}
		if (NetworkManager.IsClientStarted)
		{
			this.OnChange?.Invoke(SyncTimerOperation.Finished, prev, 0f, asServer: false);
		}
	}

	public object GetSerializedType()
	{
		return null;
	}
}
