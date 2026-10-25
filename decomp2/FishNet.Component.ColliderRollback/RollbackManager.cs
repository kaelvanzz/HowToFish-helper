using System;
using FishNet.Managing;
using FishNet.Managing.Timing;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Component.ColliderRollback;

public class RollbackManager : MonoBehaviour
{
	private int? _boundingBoxLayerNumber;

	[Tooltip("Layer to use when creating and checking against bounding boxes. This should be different from any layer used.")]
	[SerializeField]
	private LayerMask _boundingBoxLayer = 0;

	[Tooltip("Maximum time in the past colliders can be rolled back to.")]
	[SerializeField]
	private float _maximumRollbackTime = 1.25f;

	[Tooltip("Interpolation value for the NetworkTransforms or objects being rolled back.")]
	[Range(0f, 250f)]
	[SerializeField]
	internal ushort Interpolation = 2;

	internal int? BoundingBoxLayerNumber
	{
		get
		{
			if (!_boundingBoxLayerNumber.HasValue)
			{
				for (int i = 0; i < 32; i++)
				{
					if (1 << i == BoundingBoxLayer.value)
					{
						_boundingBoxLayerNumber = i;
						break;
					}
				}
			}
			return _boundingBoxLayerNumber;
		}
	}

	internal LayerMask BoundingBoxLayer => _boundingBoxLayer;

	internal float MaximumRollbackTime => _maximumRollbackTime;

	internal void InitializeOnce_Internal(NetworkManager manager)
	{
	}

	[Obsolete("Use Rollback(Vector3, Vector3, float, PreciseTick, RollbackPhysicsType.Physics, bool) instead.")]
	public void Rollback(Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, bool asOwnerAndClientHost = false)
	{
	}

	[Obsolete("Use Rollback(Scene, Vector3, Vector3, float, PreciseTick, RollbackPhysicsType.Physics, bool) instead.")]
	public void Rollback(Scene scene, Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, bool asOwnerAndClientHost = false)
	{
	}

	[Obsolete("Use Rollback(int, Vector3, Vector3, float, PreciseTick, RollbackPhysicsType.Physics, bool) instead.")]
	public void Rollback(int sceneHandle, Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, bool asOwnerAndClientHost = false)
	{
	}

	[Obsolete("Use Rollback(Scene, Vector3, Vector3, float, PreciseTick, RollbackPhysicsType.Physics2D, bool) instead.")]
	public void Rollback(Scene scene, Vector2 origin, Vector2 normalizedDirection, float distance, PreciseTick pt, bool asOwnerAndClientHost = false)
	{
	}

	[Obsolete("Use Rollback(Vector3, Vector3, float, PreciseTick, RollbackPhysicsType.Physics2D, bool) instead.")]
	public void Rollback(Vector2 origin, Vector2 normalizedDirection, float distance, PreciseTick pt, bool asOwnerAndClientHost = false)
	{
	}

	public void Rollback(PreciseTick pt, RollbackPhysicsType physicsType, bool asOwnerAndClientHost = false)
	{
	}

	public void Rollback(Scene scene, PreciseTick pt, RollbackPhysicsType physicsType, bool asOwnerAndClientHost = false)
	{
	}

	public void Rollback(int sceneHandle, PreciseTick pt, RollbackPhysicsType physicsType, bool asOwnerAndClientHost = false)
	{
	}

	public void Rollback(Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, RollbackPhysicsType physicsType, bool asOwnerAndClientHost = false)
	{
	}

	public void Rollback(Scene scene, Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, RollbackPhysicsType physicsType, bool asOwnerAndClientHost = false)
	{
	}

	public void Rollback(int sceneHandle, Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, RollbackPhysicsType physicsType, bool asOwnerAndClientHost = false)
	{
	}

	public void Return()
	{
	}
}
