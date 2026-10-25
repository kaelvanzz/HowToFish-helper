using System;

namespace FishNet.Object.Prediction;

public static class ReplicateStateExtensions
{
	public static bool IsValid(this ReplicateState value)
	{
		return value != ReplicateState.Invalid;
	}

	public static bool ContainsTicked(this ReplicateState value)
	{
		return value.FastContains(ReplicateState.Ticked);
	}

	public static bool ContainsCreated(this ReplicateState value)
	{
		return value.FastContains(ReplicateState.Created);
	}

	public static bool ContainsReplayed(this ReplicateState value)
	{
		return value.FastContains(ReplicateState.Replayed);
	}

	[Obsolete("Use ContainsReplayed.")]
	public static bool IsReplayed(this ReplicateState value)
	{
		return value.ContainsReplayed();
	}

	public static bool IsTickedCreated(this ReplicateState value)
	{
		return value == (ReplicateState.Ticked | ReplicateState.Created);
	}

	public static bool IsTickedNonCreated(this ReplicateState value)
	{
		return value == ReplicateState.Ticked;
	}

	public static bool IsReplayedCreated(this ReplicateState value)
	{
		return value == (ReplicateState.Replayed | ReplicateState.Created);
	}

	public static bool IsFuture(this ReplicateState value)
	{
		return value == ReplicateState.Replayed;
	}

	[Obsolete("Use ContainsCreated.")]
	public static bool IsCreated(this ReplicateState value)
	{
		return value.ContainsCreated();
	}

	public static bool FastContains(this ReplicateState whole, ReplicateState part)
	{
		return (whole & part) == part;
	}
}
