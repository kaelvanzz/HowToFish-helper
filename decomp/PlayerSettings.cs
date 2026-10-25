using UnityEngine;

public class PlayerSettings : MonoBehaviour
{
	public static bool HoldToAttack { get; private set; }

	public static bool HoldToSprint { get; private set; }

	public static bool HoldToAim { get; private set; }

	public static void SetHoldToAttack(bool enable)
	{
		HoldToAttack = enable;
	}

	public static void SetHoldToSprint(bool enable)
	{
		HoldToSprint = enable;
	}

	public static void SetHoldToAim(bool enable)
	{
		HoldToAim = enable;
	}
}
