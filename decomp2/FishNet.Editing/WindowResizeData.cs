using UnityEngine;

namespace FishNet.Editing;

internal struct WindowResizeData
{
	public readonly Vector2 CursorStartPosition;

	public readonly Vector2 WindowStartHeight;

	public readonly bool IsValid;

	public WindowResizeData(Vector2 cursorPosition, Vector2 windowHeight)
	{
		CursorStartPosition = cursorPosition;
		WindowStartHeight = windowHeight;
		IsValid = true;
	}
}
