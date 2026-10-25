using System;
using System.Runtime.InteropServices;
using FishNet.CodeGenerating;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Object.Helping;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Serializable]
[StructLayout(LayoutKind.Auto, CharSet = CharSet.Auto)]
[APIExclude]
public class SyncVar<T> : SyncBase, ISyncVar
{
	public struct InterpolationContainer
	{
		public T LastValue;

		public float UpdateTime;

		public void Update(T prevValue)
		{
			LastValue = prevValue;
			UpdateTime = Time.unscaledTime;
		}
	}

	private struct CachedOnChange
	{
		internal readonly T Previous;

		internal readonly T Next;

		public CachedOnChange(T previous, T next)
		{
			Previous = previous;
			Next = next;
		}
	}

	public delegate void OnChanged(T prev, T next, bool asServer);

	private CachedOnChange? _serverOnChange;

	private CachedOnChange? _clientOnChange;

	private T _initialValue;

	[SerializeField]
	private T _value;

	private InterpolationContainer _interpolator;

	private bool _valueSetAfterInitialized;

	public T Value
	{
		get
		{
			return _value;
		}
		set
		{
			SetValue(value, calledByUser: true);
		}
	}

	public event OnChanged OnChange;

	public T InterpolatedValue(bool useCurrentValue = false)
	{
		if (useCurrentValue)
		{
			return _value;
		}
		float value = Time.unscaledTime - _interpolator.UpdateTime;
		float percent = Mathf.InverseLerp(0f, Settings.SendRate, value);
		return Interpolate(_interpolator.LastValue, _value, percent);
	}

	public SyncVar(SyncTypeSettings settings = default(SyncTypeSettings))
		: this(default(T), settings)
	{
	}

	public SyncVar(T initialValue, SyncTypeSettings settings = default(SyncTypeSettings))
		: base(settings)
	{
		SetInitialValues(initialValue);
	}

	protected override void Initialized()
	{
		base.Initialized();
		_initialValue = _value;
	}

	public void SetInitialValues(T value)
	{
		_initialValue = value;
		if (!_valueSetAfterInitialized)
		{
			UpdateValues(value);
		}
		if (base.IsInitialized)
		{
			_valueSetAfterInitialized = true;
		}
	}

	private void UpdateValues(T next)
	{
		if (base.IsNetworkInitialized)
		{
			_interpolator.Update(_value);
		}
		_value = next;
	}

	internal void SetValue(T nextValue, bool calledByUser, bool sendRpc = false)
	{
		if (!base.IsInitialized)
		{
			SetInitialValues(nextValue);
			return;
		}
		_valueSetAfterInitialized = true;
		bool isNetworkInitialized = base.IsNetworkInitialized;
		if (isNetworkInitialized && CodegenHelper.NetworkObject_Deinitializing(NetworkBehaviour))
		{
			return;
		}
		if (calledByUser)
		{
			if (!CanNetworkSetValues())
			{
				return;
			}
			bool asServer2 = CanInvokeCallbackAsServer();
			if (!isNetworkInitialized)
			{
				T value = _value;
				UpdateValues(nextValue);
				InvokeOnChange(value, _value, asServer: true);
			}
			else
			{
				if (Comparers.EqualityCompare(_value, nextValue))
				{
					return;
				}
				T value2 = _value;
				UpdateValues(nextValue);
				InvokeOnChange(value2, _value, asServer2);
			}
			TryDirty(asServer2);
		}
		else
		{
			T value3 = _value;
			if (!NetworkManager.IsServerStarted)
			{
				UpdateValues(nextValue);
			}
			InvokeOnChange(value3, nextValue, asServer: false);
		}
		void TryDirty(bool asServer)
		{
			if (isNetworkInitialized && asServer)
			{
				Dirty();
			}
		}
	}

	protected virtual T Interpolate(T previous, T current, float percent)
	{
		NetworkManager.LogError("Type " + typeof(T).FullName + " does not support interpolation. Implement a supported type class or create your own. See class FloatSyncVar for an example.");
		return default(T);
	}

	private bool AsServerInvoke()
	{
		if (base.IsNetworkInitialized)
		{
			return NetworkBehaviour.IsServerStarted;
		}
		return true;
	}

	public void DirtyAll()
	{
		if (base.IsInitialized && CanNetworkSetValues())
		{
			_valueSetAfterInitialized = true;
			Dirty();
			bool asServer = CanInvokeCallbackAsServer();
			InvokeOnChange(_value, _value, asServer);
		}
	}

	private void InvokeOnChange(T prev, T next, bool asServer)
	{
		if (asServer)
		{
			if (NetworkBehaviour.OnStartServerCalled)
			{
				this.OnChange?.Invoke(prev, next, asServer);
			}
			else
			{
				_serverOnChange = new CachedOnChange(prev, next);
			}
		}
		else if (NetworkBehaviour.OnStartClientCalled)
		{
			this.OnChange?.Invoke(prev, next, asServer);
		}
		else
		{
			_clientOnChange = new CachedOnChange(prev, next);
		}
	}

	[MakePublic]
	public override void OnStartCallback(bool asServer)
	{
		base.OnStartCallback(asServer);
		if (this.OnChange != null)
		{
			CachedOnChange? cachedOnChange = (asServer ? _serverOnChange : _clientOnChange);
			if (cachedOnChange.HasValue)
			{
				InvokeOnChange(cachedOnChange.Value.Previous, cachedOnChange.Value.Next, asServer);
			}
		}
		if (asServer)
		{
			_serverOnChange = null;
		}
		else
		{
			_clientOnChange = null;
		}
	}

	[MakePublic]
	public override void WriteDelta(PooledWriter writer, bool resetSyncTick = true)
	{
		base.WriteDelta(writer, resetSyncTick);
		writer.Write(_value);
	}

	[MakePublic]
	public override void WriteFull(PooledWriter obj0)
	{
		if (_valueSetAfterInitialized)
		{
			WriteDelta(obj0, resetSyncTick: false);
		}
	}

	protected internal override void Read(PooledReader reader, bool asServer)
	{
		T nextValue = reader.Read<T>();
		if (ReadChangeId(reader))
		{
			SetValue(nextValue, calledByUser: false);
		}
	}

	[APIExclude]
	protected override bool ReadChangeId(Reader reader)
	{
		return true;
	}

	[APIExclude]
	protected override void WriteChangeId(PooledWriter writer)
	{
	}

	[MakePublic]
	public override void ResetState(bool asServer)
	{
		base.ResetState(asServer);
		if (CanReset(asServer))
		{
			_value = _initialValue;
			_valueSetAfterInitialized = false;
		}
	}
}
