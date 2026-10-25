using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using GameKit.Dependencies.Utilities;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class SyncDictionary<TKey, TValue> : SyncBase, IDictionary<TKey, TValue>, ICollection<KeyValuePair<TKey, TValue>>, IEnumerable<KeyValuePair<TKey, TValue>>, IEnumerable, IReadOnlyDictionary<TKey, TValue>, IReadOnlyCollection<KeyValuePair<TKey, TValue>>
{
	private struct CachedOnChange
	{
		internal readonly SyncDictionaryOperation Operation;

		internal readonly TKey Key;

		internal readonly TValue Value;

		public CachedOnChange(SyncDictionaryOperation operation, TKey key, TValue value)
		{
			Operation = operation;
			Key = key;
			Value = value;
		}
	}

	private struct ChangeData
	{
		internal readonly SyncDictionaryOperation Operation;

		internal readonly TKey Key;

		internal readonly TValue Value;

		internal readonly int CollectionCountAfterChange;

		public ChangeData(SyncDictionaryOperation operation, TKey key, TValue value, int collectionCountAfterChange)
		{
			Operation = operation;
			Key = key;
			Value = value;
			CollectionCountAfterChange = collectionCountAfterChange;
		}
	}

	[APIExclude]
	public delegate void SyncDictionaryChanged(SyncDictionaryOperation op, TKey key, TValue value, bool asServer);

	public Dictionary<TKey, TValue> Collection;

	private Dictionary<TKey, TValue> _initialValues = new Dictionary<TKey, TValue>();

	private List<ChangeData> _changed = new List<ChangeData>();

	private List<CachedOnChange> _serverOnChanges = new List<CachedOnChange>();

	private List<CachedOnChange> _clientOnChanges = new List<CachedOnChange>();

	private bool _valuesChanged;

	private bool _sendAll;

	[APIExclude]
	public bool IsReadOnly => false;

	public int Count => Collection.Count;

	public ICollection<TKey> Keys => Collection.Keys;

	[APIExclude]
	IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Collection.Keys;

	public ICollection<TValue> Values => Collection.Values;

	[APIExclude]
	IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Collection.Values;

	public TValue this[TKey key]
	{
		get
		{
			return Collection[key];
		}
		set
		{
			if (CanNetworkSetValues())
			{
				SyncDictionaryOperation operation = (Collection.ContainsKey(key) ? SyncDictionaryOperation.Set : SyncDictionaryOperation.Add);
				Collection[key] = value;
				AddOperation(operation, key, value, Collection.Count);
			}
		}
	}

	public event SyncDictionaryChanged OnChange;

	public SyncDictionary(SyncTypeSettings settings = default(SyncTypeSettings))
		: this(CollectionCaches<TKey, TValue>.RetrieveDictionary(), settings)
	{
	}

	public SyncDictionary(Dictionary<TKey, TValue> collection, SyncTypeSettings settings = default(SyncTypeSettings))
		: base(settings)
	{
		Collection = ((collection == null) ? CollectionCaches<TKey, TValue>.RetrieveDictionary() : collection);
		_initialValues = CollectionCaches<TKey, TValue>.RetrieveDictionary();
		_changed = CollectionCaches<ChangeData>.RetrieveList();
		_serverOnChanges = CollectionCaches<CachedOnChange>.RetrieveList();
		_clientOnChanges = CollectionCaches<CachedOnChange>.RetrieveList();
	}

	~SyncDictionary()
	{
		CollectionCaches<TKey, TValue>.StoreAndDefault(ref Collection);
		CollectionCaches<TKey, TValue>.StoreAndDefault(ref _initialValues);
		CollectionCaches<ChangeData>.StoreAndDefault(ref _changed);
		CollectionCaches<CachedOnChange>.StoreAndDefault(ref _serverOnChanges);
		CollectionCaches<CachedOnChange>.StoreAndDefault(ref _clientOnChanges);
	}

	public Dictionary<TKey, TValue> GetCollection(bool asServer)
	{
		return Collection;
	}

	protected override void Initialized()
	{
		base.Initialized();
		foreach (KeyValuePair<TKey, TValue> item in Collection)
		{
			_initialValues[item.Key] = item.Value;
		}
	}

	[APIExclude]
	private void AddOperation(SyncDictionaryOperation operation, TKey key, TValue value, int collectionCountAfterChange)
	{
		if (!base.IsInitialized)
		{
			return;
		}
		bool flag = !base.IsNetworkInitialized || NetworkBehaviour.IsServerStarted;
		if (flag)
		{
			_valuesChanged = true;
			if (Dirty())
			{
				ChangeData item = new ChangeData(operation, key, value, collectionCountAfterChange);
				_changed.Add(item);
			}
		}
		InvokeOnChange(operation, key, value, flag);
	}

	protected internal override void OnStartCallback(bool asServer)
	{
		base.OnStartCallback(asServer);
		List<CachedOnChange> list = (asServer ? _serverOnChanges : _clientOnChanges);
		if (this.OnChange != null)
		{
			foreach (CachedOnChange item in list)
			{
				this.OnChange(item.Operation, item.Key, item.Value, asServer);
			}
		}
		list.Clear();
	}

	private void WriteOperationHeader(PooledWriter writer, SyncDictionaryOperation operation, int collectionCountAfterChange)
	{
		writer.WriteUInt8Unpacked((byte)operation);
		writer.WriteInt32(collectionCountAfterChange);
	}

	private void ReadOperationHeader(PooledReader reader, out SyncDictionaryOperation operation, out int collectionCountAfterChange)
	{
		operation = (SyncDictionaryOperation)reader.ReadUInt8Unpacked();
		collectionCountAfterChange = reader.ReadInt32();
	}

	[APIExclude]
	protected internal override void WriteDelta(PooledWriter writer, bool resetSyncTick = true)
	{
		if (_sendAll)
		{
			_sendAll = false;
			_changed.Clear();
			WriteFull(writer);
			return;
		}
		base.WriteDelta(writer, resetSyncTick);
		writer.WriteBoolean(value: false);
		writer.WriteInt32(_changed.Count);
		for (int i = 0; i < _changed.Count; i++)
		{
			ChangeData changeData = _changed[i];
			WriteOperationHeader(writer, changeData.Operation, changeData.CollectionCountAfterChange);
			if (changeData.Operation == SyncDictionaryOperation.Add || changeData.Operation == SyncDictionaryOperation.Set)
			{
				writer.Write(changeData.Key);
				writer.Write(changeData.Value);
			}
			else if (changeData.Operation == SyncDictionaryOperation.Remove)
			{
				writer.Write(changeData.Key);
			}
		}
		_changed.Clear();
	}

	[APIExclude]
	protected internal override void WriteFull(PooledWriter writer)
	{
		if (!_valuesChanged)
		{
			return;
		}
		base.WriteHeader(writer, resetSyncTick: false);
		writer.WriteBoolean(value: true);
		writer.WriteInt32(Collection.Count);
		int num = 0;
		foreach (KeyValuePair<TKey, TValue> item in Collection)
		{
			WriteOperationHeader(writer, SyncDictionaryOperation.Add, num + 1);
			writer.Write(item.Key);
			writer.Write(item.Value);
			num++;
		}
	}

	[APIExclude]
	protected internal override void Read(PooledReader reader, bool asServer)
	{
		SetReadArguments(reader, asServer, out var newChangeId, out var asClientHost, out var canModifyValues);
		if (asClientHost && !base.OnStartServerCalled)
		{
			NetworkManager.LogWarning("SyncType " + GetType().Name + " received a Read but was deinitialized on the server. Client callback values may be incorrect. This is a ClientHost limitation.");
		}
		IDictionary<TKey, TValue> collection = Collection;
		bool flag = reader.ReadBoolean();
		if (canModifyValues && flag)
		{
			collection.Clear();
		}
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			ReadOperationHeader(reader, out var operation, out var collectionCountAfterChange);
			TKey key = default(TKey);
			TValue value = default(TValue);
			switch (operation)
			{
			case SyncDictionaryOperation.Add:
			case SyncDictionaryOperation.Set:
			{
				int num2 = ((operation == SyncDictionaryOperation.Add) ? (collection.Count + 1) : collection.Count);
				key = reader.Read<TKey>();
				value = reader.Read<TValue>();
				if (canModifyValues && num2 == collectionCountAfterChange)
				{
					collection[key] = value;
				}
				break;
			}
			case SyncDictionaryOperation.Clear:
				if (canModifyValues)
				{
					collection.Clear();
				}
				break;
			case SyncDictionaryOperation.Remove:
				key = reader.Read<TKey>();
				if (canModifyValues && collection.Count - 1 == collectionCountAfterChange)
				{
					collection.Remove(key);
				}
				break;
			}
			if (newChangeId)
			{
				InvokeOnChange(operation, key, value, asServer: false);
			}
		}
		if (newChangeId && num > 0)
		{
			InvokeOnChange(SyncDictionaryOperation.Complete, default(TKey), default(TValue), asServer: false);
		}
	}

	private void InvokeOnChange(SyncDictionaryOperation operation, TKey key, TValue value, bool asServer)
	{
		if (asServer)
		{
			if (NetworkBehaviour.OnStartServerCalled)
			{
				this.OnChange?.Invoke(operation, key, value, asServer);
			}
			else
			{
				_serverOnChanges.Add(new CachedOnChange(operation, key, value));
			}
		}
		else if (NetworkBehaviour.OnStartClientCalled)
		{
			this.OnChange?.Invoke(operation, key, value, asServer);
		}
		else
		{
			_clientOnChanges.Add(new CachedOnChange(operation, key, value));
		}
	}

	[APIExclude]
	protected internal override void ResetState(bool asServer)
	{
		base.ResetState(asServer);
		if (!CanReset(asServer))
		{
			return;
		}
		_sendAll = false;
		_changed.Clear();
		Collection.Clear();
		_valuesChanged = false;
		foreach (KeyValuePair<TKey, TValue> initialValue in _initialValues)
		{
			Collection[initialValue.Key] = initialValue.Value;
		}
	}

	public void Add(KeyValuePair<TKey, TValue> item)
	{
		Add(item.Key, item.Value);
	}

	public void Add(TKey key, TValue value)
	{
		Add(key, value, asServer: true);
	}

	private void Add(TKey key, TValue value, bool asServer)
	{
		if (CanNetworkSetValues())
		{
			Collection.Add(key, value);
			if (asServer)
			{
				AddOperation(SyncDictionaryOperation.Add, key, value, Collection.Count);
			}
		}
	}

	public void Clear()
	{
		Clear(asServer: true);
	}

	private void Clear(bool asServer)
	{
		if (CanNetworkSetValues())
		{
			Collection.Clear();
			if (asServer)
			{
				AddOperation(SyncDictionaryOperation.Clear, default(TKey), default(TValue), Collection.Count);
			}
		}
	}

	public bool ContainsKey(TKey key)
	{
		return Collection.ContainsKey(key);
	}

	public bool Contains(KeyValuePair<TKey, TValue> item)
	{
		if (TryGetValue(item.Key, out var value))
		{
			return EqualityComparer<TValue>.Default.Equals(value, item.Value);
		}
		return false;
	}

	public void CopyTo([NotNull] KeyValuePair<TKey, TValue>[] array, int offset)
	{
		if (offset <= -1 || offset >= array.Length)
		{
			NetworkManager.LogError("Index is out of range.");
			return;
		}
		if (array.Length - offset < Count)
		{
			NetworkManager.LogError($"Array is not large enough to copy data. Array is of length {array.Length}, index is {offset}, and number of values to be copied is {Count.ToString()}.");
			return;
		}
		int num = offset;
		foreach (KeyValuePair<TKey, TValue> item in Collection)
		{
			array[num] = item;
			num++;
		}
	}

	public bool Remove(TKey key)
	{
		if (!CanNetworkSetValues())
		{
			return false;
		}
		if (Collection.Remove(key))
		{
			AddOperation(SyncDictionaryOperation.Remove, key, default(TValue), Collection.Count);
			return true;
		}
		return false;
	}

	public bool Remove(KeyValuePair<TKey, TValue> item)
	{
		return Remove(item.Key);
	}

	public bool TryGetValue(TKey key, out TValue value)
	{
		return Collection.TryGetValueIL2CPP(key, out value);
	}

	public void DirtyAll()
	{
		if (base.IsInitialized && CanNetworkSetValues() && Dirty())
		{
			_sendAll = true;
		}
	}

	public void Dirty(TKey key)
	{
		if (base.IsInitialized && CanNetworkSetValues() && Collection.TryGetValueIL2CPP(key, out var value))
		{
			AddOperation(SyncDictionaryOperation.Set, key, value, Collection.Count);
		}
	}

	public bool Dirty(TValue value, EqualityComparer<TValue> comparer = null)
	{
		if (!base.IsInitialized)
		{
			return false;
		}
		if (!CanNetworkSetValues())
		{
			return false;
		}
		if (comparer == null)
		{
			comparer = EqualityComparer<TValue>.Default;
		}
		foreach (KeyValuePair<TKey, TValue> item in Collection)
		{
			if (comparer.Equals(item.Value, value))
			{
				AddOperation(SyncDictionaryOperation.Set, item.Key, value, Collection.Count);
				return true;
			}
		}
		return false;
	}

	public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
	{
		return Collection.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return Collection.GetEnumerator();
	}
}
