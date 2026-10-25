namespace FishNet.Managing.Scened;

internal static class SceneLookupDataExtensions
{
	public static string[] GetNames(this SceneLookupData[] datas)
	{
		string[] array = new string[datas.Length];
		for (int i = 0; i < datas.Length; i++)
		{
			array[i] = datas[i].Name;
		}
		return array;
	}

	public static string[] GetNamesOnly(this SceneLookupData[] datas)
	{
		string[] array = new string[datas.Length];
		for (int i = 0; i < datas.Length; i++)
		{
			array[i] = datas[i].NameOnly;
		}
		return array;
	}
}
