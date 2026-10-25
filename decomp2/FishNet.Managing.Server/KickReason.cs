namespace FishNet.Managing.Server;

public enum KickReason : short
{
	Unset,
	ExploitAttempt,
	MalformedData,
	ExploitExcessiveData,
	ExcessiveData,
	UnexpectedProblem,
	UnusualActivity
}
