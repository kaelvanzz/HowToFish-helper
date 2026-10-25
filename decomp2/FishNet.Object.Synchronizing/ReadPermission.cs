namespace FishNet.Object.Synchronizing;

public enum ReadPermission : byte
{
	Observers,
	OwnerOnly,
	ExcludeOwner
}
