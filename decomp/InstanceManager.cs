using System;
using System.Collections.Generic;
using UnityEngine;

public class InstanceManager : MonoBehaviour
{
	[Serializable]
	public class InstanceType
	{
		[Tooltip("Name of the type of instance")]
		public string Name;

		[Tooltip("Mesh to be used for gpu instancing")]
		public Mesh Mesh;

		[Tooltip("Material to be used for gpu instancing")]
		public Material Material;

		[Tooltip("Follow parent position and rotation while preserving instance scale; remove instances when their parent is missing")]
		public bool UseParents;

		public List<List<Matrix4x4>> Batches = new List<List<Matrix4x4>>();

		public List<List<Transform>> Parents = new List<List<Transform>>();
	}

	private static InstanceManager _instance;

	private static readonly Dictionary<string, InstanceType> _instanceTypeDic = new Dictionary<string, InstanceType>();

	private static readonly List<Matrix4x4> _worldMatrices = new List<Matrix4x4>(1000);

	[SerializeField]
	[NonReorderable]
	private InstanceType[] _instanceTypes;

	private void Awake()
	{
		Setter.SetSingleInstance(ref _instance, this);
		CreateDictionary();
	}

	private void LateUpdate()
	{
		RenderBatches();
	}

	public InstanceType GetInstanceType(string typeName)
	{
		InstanceType[] instanceTypes = _instanceTypes;
		foreach (InstanceType instanceType in instanceTypes)
		{
			if (instanceType.Name == typeName)
			{
				return instanceType;
			}
		}
		return null;
	}

	public static void AddInstance(string typeName, Vector3 pos, Quaternion rot, Vector3 scale, Transform parent = null)
	{
		InstanceType instanceType = _instanceTypeDic[typeName];
		if (instanceType.UseParents && (bool)parent)
		{
			pos = parent.InverseTransformPoint(pos);
			rot = Quaternion.Inverse(parent.rotation) * rot;
		}
		Matrix4x4 item = Matrix4x4.TRS(pos, rot, scale);
		if (instanceType.Batches.Count != 0)
		{
			List<List<Matrix4x4>> batches = instanceType.Batches;
			if (batches[batches.Count - 1].Count <= 999)
			{
				goto IL_0098;
			}
		}
		instanceType.Batches.Add(new List<Matrix4x4>());
		if (instanceType.UseParents)
		{
			instanceType.Parents.Add(new List<Transform>());
		}
		goto IL_0098;
		IL_0098:
		List<List<Matrix4x4>> batches2 = instanceType.Batches;
		batches2[batches2.Count - 1].Add(item);
		if (instanceType.UseParents)
		{
			List<List<Transform>> parents = instanceType.Parents;
			parents[parents.Count - 1].Add(parent);
		}
	}

	public static void RemoveInstancesWithinBox(BoxCollider box)
	{
		foreach (KeyValuePair<string, InstanceType> item in _instanceTypeDic)
		{
			InstanceType value = item.Value;
			if (value.UseParents)
			{
				RemoveInvalidParents(value);
			}
			for (int num = value.Batches.Count - 1; num >= 0; num--)
			{
				for (int num2 = value.Batches[num].Count - 1; num2 >= 0; num2--)
				{
					Matrix4x4 localMatrix = value.Batches[num][num2];
					if (value.UseParents)
					{
						localMatrix = GetWorldMatrix(value.Parents[num][num2], localMatrix);
					}
					Vector3 point = localMatrix.MultiplyPoint3x4(Vector3.zero);
					if (box.bounds.Contains(point))
					{
						value.Batches[num].RemoveAt(num2);
						if (value.UseParents)
						{
							value.Parents[num].RemoveAt(num2);
						}
					}
				}
			}
		}
	}

	public static void ReplaceBatches(string typeName, List<List<Matrix4x4>> NewBatches, List<List<Transform>> parents = null)
	{
		InstanceType instanceType = _instanceTypeDic[typeName];
		if (NewBatches == null)
		{
			throw new ArgumentNullException("NewBatches");
		}
		if (parents != null && parents.Count != NewBatches.Count)
		{
			throw new ArgumentException("Parent batches must match matrix batches.", "parents");
		}
		for (int i = 0; i < NewBatches.Count; i++)
		{
			if (NewBatches[i] == null)
			{
				throw new ArgumentException("Matrix batches cannot be null.", "NewBatches");
			}
			if (parents != null && (parents[i] == null || parents[i].Count != NewBatches[i].Count))
			{
				throw new ArgumentException("Each parent batch must match its matrix batch.", "parents");
			}
		}
		List<List<Transform>> list = new List<List<Transform>>();
		if (instanceType.UseParents)
		{
			if (parents != null)
			{
				list = parents;
			}
			else
			{
				foreach (List<Matrix4x4> NewBatch in NewBatches)
				{
					list.Add(new List<Transform>(new Transform[NewBatch.Count]));
				}
			}
		}
		instanceType.Batches = NewBatches;
		instanceType.Parents = list;
	}

	public static void Clear()
	{
		InstanceType[] instanceTypes = _instance._instanceTypes;
		foreach (InstanceType instanceType in instanceTypes)
		{
			instanceType.Batches.Clear();
			instanceType.Batches.Add(new List<Matrix4x4>());
			instanceType.Parents.Clear();
			if (instanceType.UseParents)
			{
				instanceType.Parents.Add(new List<Transform>());
			}
		}
	}

	private static void CreateDictionary()
	{
		_instanceTypeDic.Clear();
		InstanceType[] instanceTypes = _instance._instanceTypes;
		foreach (InstanceType instanceType in instanceTypes)
		{
			_instanceTypeDic.Add(instanceType.Name, instanceType);
		}
	}

	private static void RemoveInvalidParents(InstanceType type)
	{
		for (int num = type.Batches.Count - 1; num >= 0; num--)
		{
			for (int num2 = type.Batches[num].Count - 1; num2 >= 0; num2--)
			{
				if (!(type.Parents[num][num2] != null))
				{
					type.Batches[num].RemoveAt(num2);
					type.Parents[num].RemoveAt(num2);
				}
			}
		}
	}

	private static Matrix4x4 GetWorldMatrix(Transform parent, Matrix4x4 localMatrix)
	{
		Matrix4x4 result = Matrix4x4.Rotate(parent.rotation) * localMatrix;
		Vector3 vector = parent.TransformPoint(localMatrix.MultiplyPoint3x4(Vector3.zero));
		result.SetColumn(3, new Vector4(vector.x, vector.y, vector.z, 1f));
		return result;
	}

	private static void RenderBatches()
	{
		InstanceType[] instanceTypes = _instance._instanceTypes;
		foreach (InstanceType instanceType in instanceTypes)
		{
			if (instanceType.UseParents)
			{
				RemoveInvalidParents(instanceType);
			}
			for (int j = 0; j < instanceType.Batches.Count; j++)
			{
				List<Matrix4x4> list = instanceType.Batches[j];
				if (list.Count == 0)
				{
					continue;
				}
				if (!instanceType.UseParents)
				{
					Graphics.DrawMeshInstanced(instanceType.Mesh, 0, instanceType.Material, list);
					continue;
				}
				_worldMatrices.Clear();
				for (int k = 0; k < list.Count; k++)
				{
					Transform transform = instanceType.Parents[j][k];
					if (transform.gameObject.activeInHierarchy)
					{
						_worldMatrices.Add(GetWorldMatrix(transform, list[k]));
					}
				}
				if (_worldMatrices.Count > 0)
				{
					Graphics.DrawMeshInstanced(instanceType.Mesh, 0, instanceType.Material, _worldMatrices);
				}
			}
		}
	}
}
