using System;
using UnityEngine;

[Serializable]
public class SavedWorldItem
{
	public SavedItem Item;

	public Vector3 Position;

	public Quaternion Rotation;

	public bool IsDeadCreature;
}
