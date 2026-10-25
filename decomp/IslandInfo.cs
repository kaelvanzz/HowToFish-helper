using System;
using UnityEngine;

[Serializable]
public class IslandInfo
{
	[SerializeField]
	private GameObject _spawnPosition;

	[SerializeField]
	private GameObject _warning;

	[SerializeField]
	private Vector3 _archivedIslandPosition;

	public Vector3 IslandPosition => _spawnPosition.transform.position;

	public Vector3 ArchivedIslandPosition => _archivedIslandPosition;

	public void Toggle(bool to)
	{
		_spawnPosition.SetActive(to);
		_warning.SetActive(!to);
	}
}
