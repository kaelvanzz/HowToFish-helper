using System;
using FishNet.Managing;
using GameKit.Dependencies.Utilities;

namespace FishNet.Serializing;

public struct SubStream : IResettable
{
	private PooledReader _reader;

	private int _startPosition;

	private PooledWriter _writer;

	private bool _disposed;

	public const int UNINITIALIZED_LENGTH = -1;

	public bool Initialized { get; private set; }

	public int Length
	{
		get
		{
			if (_writer != null)
			{
				return _writer.Length;
			}
			if (_reader != null)
			{
				return _reader.Length;
			}
			return -1;
		}
	}

	public int Remaining
	{
		get
		{
			if (_reader == null)
			{
				return -1;
			}
			return _reader.Remaining;
		}
	}

	public NetworkManager NetworkManager
	{
		get
		{
			if (_writer != null)
			{
				return _writer.NetworkManager;
			}
			if (_reader != null)
			{
				return _reader.NetworkManager;
			}
			return null;
		}
	}

	public static SubStream StartWriting(NetworkManager manager, out PooledWriter writer, int minimumLength = 0)
	{
		if (minimumLength == 0)
		{
			writer = WriterPool.Retrieve(manager);
		}
		else
		{
			writer = WriterPool.Retrieve(manager, minimumLength);
		}
		SubStream result = default(SubStream);
		result._writer = writer;
		result.Initialized = true;
		return result;
	}

	public bool StartReading(out Reader reader)
	{
		if (Initialized)
		{
			_reader.Position = _startPosition;
			reader = _reader;
			return true;
		}
		reader = null;
		return false;
	}

	public static SubStream CreateFromReader(Reader originalReader, int subStreamLength)
	{
		if (subStreamLength < 0)
		{
			NetworkManagerExtensions.LogError("SubStream length cannot be less than 0");
			return default(SubStream);
		}
		byte[] buffer = originalReader.GetBuffer();
		PooledReader pooledReader = ReaderPool.Retrieve(new ArraySegment<byte>(buffer, originalReader.Position, subStreamLength), originalReader.NetworkManager);
		originalReader.Skip(subStreamLength);
		SubStream result = default(SubStream);
		result._startPosition = pooledReader.Position;
		result._reader = pooledReader;
		result._writer = null;
		result._disposed = false;
		result.Initialized = true;
		return result;
	}

	public void ResetReaderToStartPosition()
	{
		if (_reader != null)
		{
			_reader.Position = _startPosition;
		}
		else
		{
			NetworkManager.LogError("SubStream was not initialized as reader!");
		}
	}

	internal PooledWriter GetWriter()
	{
		if (!Initialized)
		{
			NetworkManager.LogError("SubStream was not initialized, it has to be initialized properly either localy or remotely!");
		}
		else if (_writer == null)
		{
			NetworkManager.LogError("GetWriter() requires SubStream to be initialized as writer! You have to create SubStream with StartWriting()!");
		}
		return _writer;
	}

	internal PooledReader GetReader()
	{
		if (!Initialized)
		{
			NetworkManager.LogError("SubStream was not initialized, it has to be initialized properly either localy or remotely!");
		}
		if (_reader == null)
		{
			NetworkManager.LogError("GetReader() requires SubStream to be initialized as reader!");
		}
		return _reader;
	}

	internal static SubStream GetUninitialized()
	{
		SubStream result = default(SubStream);
		result.Initialized = false;
		return result;
	}

	public void ResetState()
	{
		if (!_disposed)
		{
			_disposed = true;
			if (_reader != null)
			{
				_reader.Store();
				_reader = null;
			}
		}
		if (_writer != null)
		{
			if (_writer.Length < 1000)
			{
				_writer.Store();
			}
			else
			{
				_writer.StoreLength();
			}
			_writer = null;
		}
	}

	public void InitializeState()
	{
	}
}
