using UnityEngine;

namespace FishNet.Configuring.EditorCloning;

public static class CloneChecker
{
	public static bool IsMultiplayerClone(out EditorCloneType editorCloneType)
	{
		if (Application.dataPath.ToLower().Contains("library/vp/"))
		{
			editorCloneType = EditorCloneType.UnityMultiplayer;
			return true;
		}
		editorCloneType = EditorCloneType.None;
		return false;
	}
}
