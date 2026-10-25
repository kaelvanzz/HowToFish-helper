using System;
using UnityEngine;

public class PlayerColDetector : MonoBehaviour
{
	public event Action<Collision> OnCollision;

	private void OnCollisionStay(Collision col)
	{
		this.OnCollision?.Invoke(col);
	}
}
