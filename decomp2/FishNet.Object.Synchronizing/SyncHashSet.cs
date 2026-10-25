using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Documenting;
using FishNet.Managing;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using GameKit.Dependencies.Utilities;

namespace FishNet.Object.Synchronizing;

[Serializable]
public class SyncHashSet<T> : SyncBase, ISet<T>, ICollection<T>, IEnumerable<T>, IEnumerable
{
	private struct CachedOnChange
	{
		internal readonly SyncHashSetOperation Operation;

		internal readonly T Item;

		public CachedOnChange(SyncHashSetOperation operation, T item)
		{
			Operation = operation;
			Item = item;
		}
	}

	private struct ChangeData
	{
		internal readonly SyncHashSetOperation Operation;

		internal readonly T Item;

		internal readonly int CollectionCountAfterChange;

		public ChangeData(SyncHashSetOperation operation, T item, int collectionCountAfterChange)
		{
			Operation = operation;
			Item = item;
			CollectionCountAfterChange = collectionCountAfterChange;
		}
	}

	[APIExclude]
	public delegate void SyncHashSetChanged(SyncHashSetOperation op, T item, bool asServer);

	public HashSet<T> Collection;

	private static List<T> _cache = new List<T>();

	private HashSet<T> _initialValues;

	private List<ChangeData> _changed;

	private List<CachedOnChange> _serverOnChanges;

	private List<CachedOnChange> _clientOnChanges;

	private readonly IEqualityComparer<T> _comparer;

	private bool _valuesChanged;

	private bool _sendAll;

	[APIExclude]
	public bool IsReadOnly => false;

	public int Count => Collection.Count;

	public event SyncHashSetChanged OnChange;

	public SyncHashSet(SyncTypeSettings settings = default(SyncTypeSettings))
		: this(CollectionCaches<T>.RetrieveHashSet(), (IEqualityComparer<T>)EqualityComparer<T>.Default, settings)
	{
	}

	public SyncHashSet(IEqualityComparer<T> comparer, SyncTypeSettings settings = default(SyncTypeSettings))
		: this(CollectionCaches<T>.RetrieveHashSet(), (comparer == null) ? EqualityComparer<T>.Default : comparer, settings)
	{
	}

	public SyncHashSet(HashSet<T> collection, IEqualityComparer<T> comparer = null, SyncTypeSettings settings = default(SyncTypeSettings))
		: base(settings)
	{
		IEqualityComparer<T> comparer2;
		if (comparer != null)
		{
			comparer2 = comparer;
		}
		else
		{
			IEqualityComparer<T> @default = EqualityComparer<T>.Default;
			comparer2 = @default;
		}
		_comparer = comparer2;
		Collection = ((collection == null) ? CollectionCaches<T>.RetrieveHashSet() : collection);
		_initialValues = CollectionCaches<T>.RetrieveHashSet();
		_changed = CollectionCaches<ChangeData>.RetrieveList();
		_serverOnChanges = CollectionCaches<CachedOnChange>.RetrieveList();
		_clientOnChanges = CollectionCaches<CachedOnChange>.RetrieveList();
	}

	~SyncHashSet()
	{
		CollectionCaches<T>.StoreAndDefault(ref Collection);
		CollectionCaches<T>.StoreAndDefault(ref _initialValues);
		CollectionCaches<ChangeData>.StoreAndDefault(ref _changed);
		CollectionCaches<CachedOnChange>.StoreAndDefault(ref _serverOnChanges);
		CollectionCaches<CachedOnChange>.StoreAndDefault(ref _clientOnChanges);
	}

	protected override void Initialized()
	{
		base.Initialized();
		foreach (T item in Collection)
		{
			_initialValues.Add(item);
		}
	}

	public HashSet<T> GetCollection(bool asServer)
	{
		return Collection;
	}

	private void AddOperation(SyncHashSetOperation operation, T item, int collectionCountAfterChange)
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
				ChangeData item2 = new ChangeData(operation, item, collectionCountAfterChange);
				_changed.Add(item2);
			}
		}
		InvokeOnChange(operation, item, flag);
	}

	protected internal override void OnStartCallback(bool asServer)
	{
		base.OnStartCallback(asServer);
		List<CachedOnChange> list = (asServer ? _serverOnChanges : _clientOnChanges);
		if (this.OnChange != null)
		{
			foreach (CachedOnChange item in list)
			{
				this.OnChange(item.Operation, item.Item, asServer);
			}
		}
		list.Clear();
	}

	private void WriteOperationHeader(PooledWriter writer, SyncHashSetOperation operation, int collectionCountAfterChange)
	{
		writer.WriteUInt8Unpacked((byte)operation);
		writer.WriteInt32(collectionCountAfterChange);
	}

	private void ReadOperationHeader(PooledReader reader, out SyncHashSetOperation operation, out int collectionCountAfterChange)
	{
		operation = (SyncHashSetOperation)reader.ReadUInt8Unpacked();
		collectionCountAfterChange = reader.ReadInt32();
	}

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
			if (changeData.Operation == SyncHashSetOperation.Add || changeData.Operation == SyncHashSetOperation.Remove || changeData.Operation == SyncHashSetOperation.Set)
			{
				writer.Write(changeData.Item);
			}
		}
		_changed.Clear();
	}

	protected internal override void WriteFull(PooledWriter writer)
	{
		if (!_valuesChanged)
		{
			return;
		}
		base.WriteHeader(writer, resetSyncTick: false);
		writer.WriteBoolean(value: true);
		int count = Collection.Count;
		writer.WriteInt32(count);
		int num = 0;
		foreach (T item in Collection)
		{
			WriteOperationHeader(writer, SyncHashSetOperation.Add, num + 1);
			writer.Write(item);
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
		ISet<T> collection = Collection;
		bool flag = reader.ReadBoolean();
		if (canModifyValues && flag)
		{
			collection.Clear();
		}
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			ReadOperationHeader(reader, out var operation, out var collectionCountAfterChange);
			T item = default(T);
			switch (operation)
			{
			case SyncHashSetOperation.Add:
				item = reader.Read<T>();
				if (canModifyValues && collection.Count + 1 == collectionCountAfterChange)
				{
					collection.Add(item);
				}
				break;
			case SyncHashSetOperation.Clear:
				if (canModifyValues)
				{
					collection.Clear();
				}
				break;
			case SyncHashSetOperation.Remove:
				item = reader.Read<T>();
				if (canModifyValues && collection.Count - 1 == collectionCountAfterChange)
				{
					collection.Remove(item);
				}
				break;
			case SyncHashSetOperation.Set:
				item = reader.Read<T>();
				if (canModifyValues && collection.Count == collectionCountAfterChange)
				{
					collection.Remove(item);
					collection.Add(item);
				}
				break;
			}
			if (newChangeId)
			{
				InvokeOnChange(operation, item, asServer: false);
			}
		}
		if (newChangeId && num > 0)
		{
			InvokeOnChange(SyncHashSetOperation.Complete, default(T), asServer: false);
		}
	}

	private void InvokeOnChange(SyncHashSetOperation operation, T item, bool asServer)
	{
		if (asServer)
		{
			if (NetworkBehaviour.OnStartServerCalled)
			{
				this.OnChange?.Invoke(operation, item, asServer);
			}
			else
			{
				_serverOnChanges.Add(new CachedOnChange(operation, item));
			}
		}
		else if (NetworkBehaviour.OnStartClientCalled)
		{
			this.OnChange?.Invoke(operation, item, asServer);
		}
		else
		{
			_clientOnChanges.Add(new CachedOnChange(operation, item));
		}
	}

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
		foreach (T initialValue in _initialValues)
		{
			Collection.Add(initialValue);
		}
	}

	public bool Add(T item)
	{
		return Add(item, asServer: true);
	}

	private bool Add(T item, bool asServer)
	{
		if (!CanNetworkSetValues())
		{
			return false;
		}
		bool num = Collection.Add(item);
		if (num && asServer)
		{
			AddOperation(SyncHashSetOperation.Add, item, Collection.Count);
		}
		return num;
	}

	public void AddRange(IEnumerable<T> range)
	{
		foreach (T item in range)
		{
			Add(item, asServer: true);
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
				AddOperation(SyncHashSetOperation.Clear, default(T), Collection.Count);
			}
		}
	}

	public bool Contains(T item)
	{
		return Collection.Contains(item);
	}

	public bool Remove(T item)
	{
		return Remove(item, asServer: true);
	}

	private bool Remove(T item, bool asServer)
	{
		if (!CanNetworkSetValues())
		{
			return false;
		}
		bool num = Collection.Remove(item);
		if (num && asServer)
		{
			AddOperation(SyncHashSetOperation.Remove, item, Collection.Count);
		}
		return num;
	}

	public void DirtyAll()
	{
		if (base.IsInitialized && CanNetworkSetValues() && Dirty())
		{
			_sendAll = true;
		}
	}

	public void Dirty(T obj)
	{
		if (!base.IsInitialized || !CanNetworkSetValues())
		{
			return;
		}
		foreach (T item in Collection)
		{
			if (item.Equals(obj))
			{
				AddOperation(SyncHashSetOperation.Set, obj, Collection.Count);
				return;
			}
		}
		NetworkManager.LogError("Could not find object within SyncHashSet, dirty will not be set.");
	}

	public IEnumerator GetEnumerator()
	{
		return Collection.GetEnumerator();
	}

	[APIExclude]
	IEnumerator<T> IEnumerable<T>.GetEnumerator()
	{
		return Collection.GetEnumerator();
	}

	[APIExclude]
	IEnumerator IEnumerable.GetEnumerator()
	{
		return Collection.GetEnumerator();
	}

	public void ExceptWith(IEnumerable<T> other)
	{
		if (other == Collection)
		{
			Clear();
			return;
		}
		foreach (T item in other)
		{
			Remove(item);
		}
	}

	public void IntersectWith(IEnumerable<T> other)
	{
		ISet<T> other2 = ((!(other is ISet<T> set)) ? new HashSet<T>(other) : set);
		IntersectWith(other2);
	}

	private void IntersectWith(ISet<T> other)
	{
		_cache.AddRange(Collection);
		int count = _cache.Count;
		for (int i = 0; i < count; i++)
		{
			T item = _cache[i];
			if (!other.Contains(item))
			{
				Remove(item);
			}
		}
		_cache.Clear();
	}

	public bool IsProperSubsetOf(IEnumerable<T> other)
	{
		return Collection.IsProperSubsetOf(other);
	}

	public bool IsProperSupersetOf(IEnumerable<T> other)
	{
		return Collection.IsProperSupersetOf(other);
	}

	public bool IsSubsetOf(IEnumerable<T> other)
	{
		return Collection.IsSubsetOf(other);
	}

	public bool IsSupersetOf(IEnumerable<T> other)
	{
		return Collection.IsSupersetOf(other);
	}

	public bool Overlaps(IEnumerable<T> other)
	{
		return Collection.Overlaps(other);
	}

	public bool SetEquals(IEnumerable<T> other)
	{
		return Collection.SetEquals(other);
	}

	public void SymmetricExceptWith(IEnumerable<T> other)
	{
		if (other == Collection)
		{
			Clear();
			return;
		}
		foreach (T item in other)
		{
			Remove(item);
		}
	}

	public void UnionWith(IEnumerable<T> other)
	{
		if (other == Collection)
		{
			return;
		}
		foreach (T item in other)
		{
			Add(item);
		}
	}

	void ICollection<T>.Add(T item)
	{
		Add(item, asServer: true);
	}

	public void CopyTo(T[] array, int index)
	{
		Collection.CopyTo(array, index);
	}
}
