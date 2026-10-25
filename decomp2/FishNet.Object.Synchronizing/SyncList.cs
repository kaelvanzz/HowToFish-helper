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
public class SyncList<T> : SyncBase, IList<T>, ICollection<T>, IEnumerable<T>, IEnumerable, IReadOnlyList<T>, IReadOnlyCollection<T>
{
	private struct CachedOnChange
	{
		internal readonly SyncListOperation Operation;

		internal readonly int Index;

		internal readonly T Previous;

		internal readonly T Next;

		public CachedOnChange(SyncListOperation operation, int index, T previous, T next)
		{
			Operation = operation;
			Index = index;
			Previous = previous;
			Next = next;
		}
	}

	private struct ChangeData
	{
		internal readonly SyncListOperation Operation;

		internal readonly int EntryIndex;

		internal readonly T Item;

		internal readonly int CollectionCountAfterChange;

		public ChangeData(SyncListOperation operation, int entryIndex, T item, int collectionCountAfterChange)
		{
			Operation = operation;
			EntryIndex = entryIndex;
			Item = item;
			CollectionCountAfterChange = collectionCountAfterChange;
		}
	}

	[APIExclude]
	public delegate void SyncListChanged(SyncListOperation op, int index, T oldItem, T newItem, bool asServer);

	public List<T> Collection;

	private List<T> _initialValues;

	private readonly IEqualityComparer<T> _comparer;

	private List<ChangeData> _changed;

	private List<CachedOnChange> _serverOnChanges;

	private List<CachedOnChange> _clientOnChanges;

	private bool _valuesChanged;

	private bool _sendAll;

	[APIExclude]
	public bool IsReadOnly => false;

	public int Count => Collection.Count;

	public T this[int i]
	{
		get
		{
			return Collection[i];
		}
		set
		{
			Set(i, value, asServer: true, force: true);
		}
	}

	public event SyncListChanged OnChange;

	public SyncList(SyncTypeSettings settings = default(SyncTypeSettings))
		: this(CollectionCaches<T>.RetrieveList(), (IEqualityComparer<T>)EqualityComparer<T>.Default, settings)
	{
	}

	public SyncList(IEqualityComparer<T> comparer, SyncTypeSettings settings = default(SyncTypeSettings))
		: this(new List<T>(), (comparer == null) ? EqualityComparer<T>.Default : comparer, settings)
	{
	}

	public SyncList(List<T> collection, IEqualityComparer<T> comparer = null, SyncTypeSettings settings = default(SyncTypeSettings))
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
		Collection = ((collection == null) ? CollectionCaches<T>.RetrieveList() : collection);
		_initialValues = CollectionCaches<T>.RetrieveList();
		_changed = CollectionCaches<ChangeData>.RetrieveList();
		_serverOnChanges = CollectionCaches<CachedOnChange>.RetrieveList();
		_clientOnChanges = CollectionCaches<CachedOnChange>.RetrieveList();
	}

	~SyncList()
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

	public List<T> GetCollection(bool asServer)
	{
		return Collection;
	}

	private void AddOperation(SyncListOperation operation, int index, T prev, T next, int collectionCountAfterChange)
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
				ChangeData item = new ChangeData(operation, index, next, collectionCountAfterChange);
				_changed.Add(item);
			}
		}
		InvokeOnChange(operation, index, prev, next, flag);
	}

	protected internal override void OnStartCallback(bool asServer)
	{
		base.OnStartCallback(asServer);
		List<CachedOnChange> list = (asServer ? _serverOnChanges : _clientOnChanges);
		if (this.OnChange != null)
		{
			foreach (CachedOnChange item in list)
			{
				this.OnChange(item.Operation, item.Index, item.Previous, item.Next, asServer);
			}
		}
		list.Clear();
	}

	private void WriteOperationHeader(PooledWriter writer, SyncListOperation operation, int entryIndex, int collectionCountAfterChange)
	{
		writer.WriteUInt8Unpacked((byte)operation);
		writer.WriteInt32(entryIndex);
		writer.WriteInt32(collectionCountAfterChange);
	}

	private void ReadOperationHeader(PooledReader reader, out SyncListOperation operation, out int entryIndex, out int collectionCountAfterChange)
	{
		operation = (SyncListOperation)reader.ReadUInt8Unpacked();
		entryIndex = reader.ReadInt32();
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
			WriteOperationHeader(writer, changeData.Operation, changeData.EntryIndex, changeData.CollectionCountAfterChange);
			if (changeData.Operation == SyncListOperation.Add)
			{
				writer.Write(changeData.Item);
			}
			else if (changeData.Operation != SyncListOperation.RemoveAt && (changeData.Operation == SyncListOperation.Insert || changeData.Operation == SyncListOperation.Set))
			{
				writer.Write(changeData.Item);
			}
		}
		_changed.Clear();
	}

	protected internal override void WriteFull(PooledWriter writer)
	{
		if (_valuesChanged)
		{
			base.WriteHeader(writer, resetSyncTick: false);
			writer.WriteBoolean(value: true);
			int count = Collection.Count;
			writer.WriteInt32(count);
			for (int i = 0; i < count; i++)
			{
				WriteOperationHeader(writer, SyncListOperation.Add, i, i + 1);
				writer.Write(Collection[i]);
			}
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
		List<T> collection = Collection;
		bool flag = reader.ReadBoolean();
		if (canModifyValues && flag)
		{
			collection.Clear();
		}
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			ReadOperationHeader(reader, out var operation, out var entryIndex, out var collectionCountAfterChange);
			T prev = default(T);
			T val = default(T);
			switch (operation)
			{
			case SyncListOperation.Add:
			case SyncListOperation.Insert:
				val = reader.Read<T>();
				if (canModifyValues && collection.Count + 1 == collectionCountAfterChange && entryIndex <= collection.Count)
				{
					collection.Insert(entryIndex, val);
				}
				break;
			case SyncListOperation.Clear:
				if (canModifyValues)
				{
					collection.Clear();
				}
				break;
			case SyncListOperation.RemoveAt:
				if (canModifyValues && collection.Count - 1 == collectionCountAfterChange && entryIndex < collection.Count)
				{
					prev = collection[entryIndex];
					collection.RemoveAt(entryIndex);
				}
				break;
			case SyncListOperation.Set:
				val = reader.Read<T>();
				if (canModifyValues && collection.Count == collectionCountAfterChange && entryIndex < collection.Count)
				{
					prev = collection[entryIndex];
					collection[entryIndex] = val;
				}
				break;
			}
			if (newChangeId)
			{
				InvokeOnChange(operation, entryIndex, prev, val, asServer: false);
			}
		}
		if (newChangeId && num > 0)
		{
			InvokeOnChange(SyncListOperation.Complete, -1, default(T), default(T), asServer: false);
		}
	}

	private void InvokeOnChange(SyncListOperation operation, int index, T prev, T next, bool asServer)
	{
		if (asServer)
		{
			if (NetworkBehaviour.OnStartServerCalled)
			{
				this.OnChange?.Invoke(operation, index, prev, next, asServer);
			}
			else
			{
				_serverOnChanges.Add(new CachedOnChange(operation, index, prev, next));
			}
		}
		else if (NetworkBehaviour.OnStartClientCalled)
		{
			this.OnChange?.Invoke(operation, index, prev, next, asServer);
		}
		else
		{
			_clientOnChanges.Add(new CachedOnChange(operation, index, prev, next));
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

	public void Add(T item)
	{
		Add(item, asServer: true);
	}

	private void Add(T item, bool asServer)
	{
		if (CanNetworkSetValues())
		{
			Collection.Add(item);
			if (asServer)
			{
				int index = Collection.Count - 1;
				AddOperation(SyncListOperation.Add, index, default(T), item, Collection.Count);
			}
		}
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
				AddOperation(SyncListOperation.Clear, -1, default(T), default(T), Collection.Count);
			}
		}
	}

	public bool Contains(T item)
	{
		return IndexOf(item) >= 0;
	}

	public void CopyTo(T[] array, int index)
	{
		Collection.CopyTo(array, index);
	}

	public int IndexOf(T item)
	{
		for (int i = 0; i < Collection.Count; i++)
		{
			if (_comparer.Equals(item, Collection[i]))
			{
				return i;
			}
		}
		return -1;
	}

	public int FindIndex(Predicate<T> match)
	{
		for (int i = 0; i < Collection.Count; i++)
		{
			if (match(Collection[i]))
			{
				return i;
			}
		}
		return -1;
	}

	public T Find(Predicate<T> match)
	{
		int num = FindIndex(match);
		if (num == -1)
		{
			return default(T);
		}
		return Collection[num];
	}

	public List<T> FindAll(Predicate<T> match)
	{
		List<T> list = new List<T>();
		for (int i = 0; i < Collection.Count; i++)
		{
			if (match(Collection[i]))
			{
				list.Add(Collection[i]);
			}
		}
		return list;
	}

	public void Insert(int index, T item)
	{
		Insert(index, item, asServer: true);
	}

	private void Insert(int index, T item, bool asServer)
	{
		if (CanNetworkSetValues())
		{
			Collection.Insert(index, item);
			if (asServer)
			{
				AddOperation(SyncListOperation.Insert, index, default(T), item, Collection.Count);
			}
		}
	}

	public void InsertRange(int index, IEnumerable<T> range)
	{
		foreach (T item in range)
		{
			Insert(index, item);
			index++;
		}
	}

	public bool Remove(T item)
	{
		int num = IndexOf(item);
		bool num2 = num >= 0;
		if (num2)
		{
			RemoveAt(num);
		}
		return num2;
	}

	public void RemoveAt(int index)
	{
		RemoveAt(index, asServer: true);
	}

	private void RemoveAt(int index, bool asServer)
	{
		if (CanNetworkSetValues())
		{
			T prev = Collection[index];
			Collection.RemoveAt(index);
			if (asServer)
			{
				AddOperation(SyncListOperation.RemoveAt, index, prev, default(T), Collection.Count);
			}
		}
	}

	public int RemoveAll(Predicate<T> match)
	{
		List<T> list = new List<T>();
		for (int i = 0; i < Collection.Count; i++)
		{
			if (match(Collection[i]))
			{
				list.Add(Collection[i]);
			}
		}
		foreach (T item in list)
		{
			Remove(item);
		}
		return list.Count;
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
		int num = Collection.IndexOf(obj);
		if (num != -1)
		{
			Dirty(num);
		}
		else
		{
			NetworkManager.LogError("Could not find object within SyncList, dirty will not be set.");
		}
	}

	public void Dirty(int index)
	{
		if (CanNetworkSetValues())
		{
			T val = Collection[index];
			AddOperation(SyncListOperation.Set, index, val, val, Collection.Count);
		}
	}

	public void Set(int index, T value, bool force = true)
	{
		Set(index, value, asServer: true, force);
	}

	private void Set(int index, T value, bool asServer, bool force)
	{
		if (CanNetworkSetValues() && (force || !_comparer.Equals(Collection[index], value)))
		{
			T prev = Collection[index];
			Collection[index] = value;
			if (asServer)
			{
				AddOperation(SyncListOperation.Set, index, prev, value, Collection.Count);
			}
		}
	}

	public IEnumerator<T> GetEnumerator()
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
}
