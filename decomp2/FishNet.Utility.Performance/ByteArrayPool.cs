using System;
using System.Collections.Generic;

namespace FishNet.Utility.Performance;

public static class ByteArrayPool
{
	private static Queue<byte[]> _byteArrays = new Queue<byte[]>();

	public static byte[] Retrieve(int minimumLength)
	{
		byte[] array = null;
		if (_byteArrays.Count > 0)
		{
			array = _byteArrays.Dequeue();
		}
		int num = minimumLength * 2;
		if (array == null)
		{
			array = new byte[num];
		}
		else if (array.Length < minimumLength)
		{
			Array.Resize(ref array, num);
		}
		return array;
	}

	public static void Store(byte[] buffer)
	{
		if (_byteArrays.Count <= 300)
		{
			_byteArrays.Enqueue(buffer);
		}
	}
}
