using System;
using System.Collections.Generic;
using System.IO;
using Steamworks;
using UnityEngine;

public static class SaveSystem
{
	private static readonly string _saveRootFolder = Path.Combine(Application.persistentDataPath, "Saves");

	private static readonly string _localFileName = "local";

	private static readonly string _extension = ".txt";

	private static string _saveFolder;

	private static string _fullLocalSavePath;

	private static string _localBackupPath;

	private static string _localTempPath;

	public static void Init()
	{
		string path = SteamUser.GetSteamID().m_SteamID.ToString();
		_saveFolder = Path.Combine(_saveRootFolder, path);
		_fullLocalSavePath = Path.Combine(_saveFolder, _localFileName + _extension);
		_localBackupPath = _fullLocalSavePath + ".backup";
		_localTempPath = _fullLocalSavePath + ".tmp";
		Directory.CreateDirectory(_saveRootFolder);
		bool num = !Directory.Exists(_saveFolder) || !HasPrimaryOrBackupSave(_saveFolder);
		Directory.CreateDirectory(_saveFolder);
		if (!num)
		{
			return;
		}
		string[] files = Directory.GetFiles(_saveRootFolder, "*", SearchOption.TopDirectoryOnly);
		foreach (string text in files)
		{
			if (IsSaveFile(text))
			{
				string text2 = Path.Combine(_saveFolder, Path.GetFileName(text));
				try
				{
					File.Move(text, text2);
				}
				catch (Exception arg)
				{
					Debug.LogError($"Could not migrate legacy save '{text}' to '{text2}': {arg}");
				}
			}
		}
	}

	private static bool IsSaveFile(string path)
	{
		string extension = Path.GetExtension(path);
		if (!extension.Equals(_extension, StringComparison.OrdinalIgnoreCase))
		{
			return extension.Equals(".backup", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static bool HasPrimaryOrBackupSave(string folder)
	{
		string[] files = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly);
		for (int i = 0; i < files.Length; i++)
		{
			if (IsSaveFile(files[i]))
			{
				return true;
			}
		}
		return false;
	}

	public static void SaveServer(string name, string saveString)
	{
		if (!IsValidServerSave(saveString, out var error))
		{
			Debug.LogError("Server save '" + name + "' was not written because it is invalid: " + error);
			return;
		}
		string text = Path.Combine(_saveFolder, name + _extension);
		string text2 = text + ".backup";
		string text3 = text + ".tmp";
		try
		{
			File.WriteAllText(text3, saveString);
			if (!IsValidServerSave(File.ReadAllText(text3), out error))
			{
				throw new InvalidDataException("Temporary server save failed validation: " + error);
			}
			if (File.Exists(text))
			{
				string saveString2;
				string destinationBackupFileName = (((TryReadValidServerSave(text, out saveString2) ? saveString2 : null) != null) ? text2 : null);
				File.Replace(text3, text, destinationBackupFileName);
			}
			else
			{
				File.Move(text3, text);
			}
		}
		catch (Exception arg)
		{
			Debug.LogError($"Could not safely write server save '{name}': {arg}");
		}
		finally
		{
			try
			{
				if (File.Exists(text3))
				{
					File.Delete(text3);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("Could not remove temporary server save '" + text3 + "': " + ex.Message);
			}
		}
	}

	public static void DeleteServer(string name)
	{
		string text = Path.Combine(_saveFolder, name + _extension);
		if (File.Exists(text))
		{
			File.Delete(text);
		}
		string path = text + ".backup";
		if (File.Exists(path))
		{
			File.Delete(path);
		}
		string path2 = text + ".tmp";
		if (File.Exists(path2))
		{
			File.Delete(path2);
		}
	}

	public static List<string> LoadAllServers()
	{
		FileInfo[] files = new DirectoryInfo(_saveFolder).GetFiles("*" + _extension);
		List<string> list = new List<string>();
		FileInfo[] array = files;
		foreach (FileInfo fileInfo in array)
		{
			if (!(fileInfo.Name == _localFileName + _extension))
			{
				if (TryReadValidServerSave(fileInfo.FullName, out var saveString))
				{
					list.Add(saveString);
					continue;
				}
				if (!TryReadValidServerSave(fileInfo.FullName + ".backup", out saveString))
				{
					Debug.LogError("Neither server save '" + fileInfo.FullName + "' nor its backup could be loaded.");
					continue;
				}
				Debug.LogWarning("Server save '" + fileInfo.FullName + "' could not be loaded. Recovering its backup.");
				list.Add(saveString);
				SaveServer(Path.GetFileNameWithoutExtension(fileInfo.Name), saveString);
			}
		}
		return list;
	}

	public static void SaveLocal(string saveString)
	{
		if (!IsValidLocalSave(saveString, out var error))
		{
			Debug.LogError("Local save was not written because it is invalid: " + error);
			return;
		}
		try
		{
			File.WriteAllText(_localTempPath, saveString);
			if (!IsValidLocalSave(File.ReadAllText(_localTempPath), out error))
			{
				throw new InvalidDataException("Temporary local save failed validation: " + error);
			}
			if (File.Exists(_fullLocalSavePath))
			{
				string error2;
				string destinationBackupFileName = (IsValidLocalSave(File.ReadAllText(_fullLocalSavePath), out error2) ? _localBackupPath : null);
				File.Replace(_localTempPath, _fullLocalSavePath, destinationBackupFileName);
			}
			else
			{
				File.Move(_localTempPath, _fullLocalSavePath);
			}
		}
		catch (Exception arg)
		{
			Debug.LogError($"Could not safely write local save: {arg}");
		}
		finally
		{
			try
			{
				if (File.Exists(_localTempPath))
				{
					File.Delete(_localTempPath);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("Could not remove temporary local save: " + ex.Message);
			}
		}
	}

	public static string LoadLocal()
	{
		if (TryReadValidLocalSave(_fullLocalSavePath, out var saveString))
		{
			return saveString;
		}
		if (!File.Exists(_localBackupPath))
		{
			return null;
		}
		Debug.LogWarning("local.txt could not be loaded. Attempting to recover the local save backup.");
		if (TryReadValidLocalSave(_localBackupPath, out saveString))
		{
			SaveLocal(saveString);
			return saveString;
		}
		Debug.LogError("Neither local.txt nor its backup contains a valid local save. Using defaults.");
		return null;
	}

	private static bool TryReadValidLocalSave(string path, out string saveString)
	{
		saveString = null;
		if (!File.Exists(path))
		{
			return false;
		}
		try
		{
			saveString = File.ReadAllText(path);
			if (IsValidLocalSave(saveString, out var error))
			{
				return true;
			}
			Debug.LogWarning("Local save '" + path + "' is invalid: " + error);
		}
		catch (Exception arg)
		{
			Debug.LogWarning($"Could not read local save '{path}': {arg}");
		}
		saveString = null;
		return false;
	}

	private static bool IsValidLocalSave(string saveString, out string error)
	{
		if (string.IsNullOrWhiteSpace(saveString))
		{
			error = "The file is empty.";
			return false;
		}
		try
		{
			if (JsonUtility.FromJson<SaveManager.LocalSaveObject>(saveString) == null)
			{
				error = "JSON produced a null save object.";
				return false;
			}
			error = null;
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}

	private static bool TryReadValidServerSave(string path, out string saveString)
	{
		saveString = null;
		if (!File.Exists(path))
		{
			return false;
		}
		try
		{
			saveString = File.ReadAllText(path);
			if (IsValidServerSave(saveString, out var error))
			{
				return true;
			}
			Debug.LogWarning("Server save '" + path + "' is invalid: " + error);
		}
		catch (Exception arg)
		{
			Debug.LogWarning($"Could not read server save '{path}': {arg}");
		}
		saveString = null;
		return false;
	}

	private static bool IsValidServerSave(string saveString, out string error)
	{
		if (string.IsNullOrWhiteSpace(saveString))
		{
			error = "The file is empty.";
			return false;
		}
		try
		{
			SaveManager.ServerSaveObject serverSaveObject = JsonUtility.FromJson<SaveManager.ServerSaveObject>(saveString);
			if (serverSaveObject == null)
			{
				error = "JSON produced a null save object.";
				return false;
			}
			if (string.IsNullOrWhiteSpace(serverSaveObject.Name))
			{
				error = "The server name is missing.";
				return false;
			}
			if (serverSaveObject.LastSave <= 0)
			{
				error = "The last-save timestamp is missing or invalid.";
				return false;
			}
			error = null;
			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}
}
