using System;
using UnityEngine;

namespace FishNet.Object.Synchronizing;

[Obsolete("This no longer functions. See console errors and Break Solutions in the documentation for resolution.")]
[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public class SyncObjectAttribute : PropertyAttribute
{
	public float SendRate = 0.1f;

	public ReadPermission ReadPermissions;

	public WritePermission WritePermissions;

	public bool RequireReadOnly = true;
}
