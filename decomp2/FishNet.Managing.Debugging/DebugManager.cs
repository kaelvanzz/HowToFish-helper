using UnityEngine;

namespace FishNet.Managing.Debugging;

[DisallowMultipleComponent]
[AddComponentMenu("FishNet/Manager/DebugManager")]
public class DebugManager : MonoBehaviour
{
	[Tooltip("True to write additional information about scene objects being sent in spawn messages. This is primarily used to resolve sceneId not found errors.")]
	public bool WriteSceneObjectDetails;

	[Tooltip("True to validate written versus read length of Rpcs. Errors will be thrown if read length is not equal to written length.")]
	public bool ValidateRpcLengths;

	[Tooltip("True to disable RpcLinks for Observer RPCs.")]
	public bool DisableObserversRpcLinks;

	[Tooltip("True to disable RpcLinks for Target RPCs.")]
	public bool DisableTargetRpcLinks;

	[Tooltip("True to disable RpcLinks for Server RPCs.")]
	public bool DisableServerRpcLinks;

	[Tooltip("True to disable RpcLinks for Replicate RPCs.")]
	public bool DisableReplicateRpcLinks;

	[Tooltip("True to disable RpcLinks for Reconcile RPCs.")]
	public bool DisableReconcileRpcLinks;
}
