using System;
using System.Collections.Generic;
using System.IO;
using GameKit.Dependencies.Utilities;
using UnityEngine.SceneManagement;

namespace FishNet.Managing.Scened;

public class SceneLookupData : IEquatable<SceneLookupData>
{
	public int Handle;

	public string Name = string.Empty;

	private const string INVALID_SCENE = "One or more scene information entries contain invalid data and have been skipped.";

	public string NameOnly
	{
		get
		{
			if (string.IsNullOrEmpty(Name))
			{
				return string.Empty;
			}
			return RemoveUnityExtension(Path.GetFileName(Name));
		}
	}

	public bool IsValid
	{
		get
		{
			if (!(Name != string.Empty))
			{
				return Handle != 0;
			}
			return true;
		}
	}

	public SceneLookupData()
	{
	}

	public SceneLookupData(Scene scene)
	{
		Handle = scene.handle;
		Name = scene.name;
	}

	public SceneLookupData(string name)
	{
		Name = name;
	}

	public SceneLookupData(int handle)
	{
		Handle = handle;
	}

	public SceneLookupData(int handle, string name)
	{
		Handle = handle;
		Name = name;
	}

	public static bool operator ==(SceneLookupData sldA, SceneLookupData sldB)
	{
		if ((object)sldA == null != ((object)sldB == null))
		{
			return false;
		}
		return sldA?.Equals(sldB) ?? sldB?.Equals(sldA) ?? true;
	}

	public static bool operator !=(SceneLookupData sldA, SceneLookupData sldB)
	{
		if ((object)sldA == null != ((object)sldB == null))
		{
			return true;
		}
		if ((object)sldA != null)
		{
			return !sldA.Equals(sldB);
		}
		if ((object)sldB != null)
		{
			return !sldB.Equals(sldA);
		}
		return true;
	}

	public bool Equals(SceneLookupData sld)
	{
		if ((object)sld == null)
		{
			return false;
		}
		bool flag = Handle == 0 && sld.Handle == 0;
		if (!flag && sld.Handle == Handle)
		{
			return true;
		}
		if (flag && sld.Name == Name)
		{
			return true;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return (2053068273 * -1521134295 + Handle.GetHashCode()) * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
	}

	public override bool Equals(object obj)
	{
		return base.Equals(obj);
	}

	public override string ToString()
	{
		return $"Name {Name}, Handle {Handle}";
	}

	public static SceneLookupData CreateData(Scene scene)
	{
		return new SceneLookupData(scene);
	}

	public static SceneLookupData CreateData(string name)
	{
		return new SceneLookupData(name);
	}

	public static SceneLookupData CreateData(int handle)
	{
		return new SceneLookupData(handle);
	}

	public static SceneLookupData[] CreateData(List<Scene> scenes)
	{
		return CreateData(scenes.ToArray());
	}

	public static SceneLookupData[] CreateData(List<string> names)
	{
		return CreateData(names.ToArray());
	}

	public static SceneLookupData[] CreateData(List<int> handles)
	{
		return CreateData(handles.ToArray());
	}

	public static SceneLookupData[] CreateData(Scene[] scenes)
	{
		bool flag = false;
		List<SceneLookupData> list = new List<SceneLookupData>();
		for (int i = 0; i < scenes.Length; i++)
		{
			Scene scene = scenes[i];
			if (!scene.IsValid())
			{
				flag = true;
			}
			else
			{
				list.Add(CreateData(scene));
			}
		}
		if (flag)
		{
			NetworkManagerExtensions.LogWarning("One or more scene information entries contain invalid data and have been skipped.");
		}
		return list.ToArray();
	}

	public static SceneLookupData[] CreateData(string[] names)
	{
		SceneLookupData[] array = new SceneLookupData[names.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = new SceneLookupData(names[i]);
		}
		return ValidateData(array);
	}

	public static SceneLookupData[] ValidateData(SceneLookupData data)
	{
		return ValidateData(new SceneLookupData[1] { data });
	}

	public static SceneLookupData[] ValidateData(SceneLookupData[] datas)
	{
		bool flag = false;
		List<SceneLookupData> list = CollectionCaches<SceneLookupData>.RetrieveList();
		foreach (SceneLookupData sceneLookupData in datas)
		{
			if (sceneLookupData.IsValid)
			{
				int num = -1;
				for (int j = 0; j < list.Count; j++)
				{
					bool flag2 = list[j].Name == sceneLookupData.Name;
					bool flag3 = list[j].Handle == sceneLookupData.Handle;
					if (flag3)
					{
						if (sceneLookupData.Handle != 0)
						{
							num = j;
						}
					}
					else if (flag2 && flag3)
					{
						num = j;
					}
				}
				if (num != -1)
				{
					NetworkManagerExtensions.LogWarning("Data " + sceneLookupData.ToString() + " matches " + list[num].ToString() + " and has been removed from datas.");
				}
				else
				{
					list.Add(sceneLookupData);
				}
			}
			else
			{
				flag = true;
			}
		}
		SceneLookupData[] result;
		if (flag)
		{
			NetworkManagerExtensions.LogWarning("One or more scene information entries contain invalid data and have been skipped.");
			result = list.ToArray();
		}
		else
		{
			result = datas;
		}
		CollectionCaches<SceneLookupData>.Store(list);
		return result;
	}

	public static SceneLookupData[] CreateData(int[] handles)
	{
		bool flag = false;
		List<SceneLookupData> list = new List<SceneLookupData>();
		foreach (int num in handles)
		{
			if (num == 0)
			{
				flag = true;
			}
			else
			{
				list.Add(CreateData(num));
			}
		}
		if (flag)
		{
			NetworkManagerExtensions.LogWarning("One or more scene information entries contain invalid data and have been skipped.");
		}
		return list.ToArray();
	}

	private static string RemoveUnityExtension(string text)
	{
		string text2 = ".unity";
		int num = text.ToLower().IndexOf(text2);
		if (num != -1 && text.Length - num == text2.Length)
		{
			text = text.Substring(0, num);
		}
		return text;
	}

	public Scene GetScene(out bool foundByHandle, bool warnIfDuplicates = true)
	{
		foundByHandle = false;
		if (Handle == 0 && string.IsNullOrEmpty(NameOnly))
		{
			NetworkManagerExtensions.LogWarning("Scene handle and name is unset; scene cannot be returned.");
			return default(Scene);
		}
		Scene result = default(Scene);
		if (Handle != 0)
		{
			result = SceneManager.GetScene(Handle);
			if (result.handle != 0)
			{
				foundByHandle = true;
			}
		}
		if (!foundByHandle)
		{
			result = SceneManager.GetScene(NameOnly, null, warnIfDuplicates);
		}
		return result;
	}
}
