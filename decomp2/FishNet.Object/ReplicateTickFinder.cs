using FishNet.Object.Prediction;
using GameKit.Dependencies.Utilities.Types;

namespace FishNet.Object;

internal static class ReplicateTickFinder
{
	public enum DataPlacementResult
	{
		Error,
		Exact,
		InsertBeginning,
		InsertMiddle,
		InsertEnd
	}

	public static int GetReplicateHistoryIndex<T>(uint tick, RingBuffer<ReplicateDataContainer<T>> replicatesHistory, out DataPlacementResult findResult) where T : IReplicateData, new()
	{
		int replicatesCount = replicatesHistory.Count;
		if (replicatesCount == 0)
		{
			findResult = DataPlacementResult.InsertBeginning;
			return 0;
		}
		uint firstTick = replicatesHistory[0].Data.GetTick();
		int num = (int)(tick - firstTick);
		if (num >= replicatesCount)
		{
			return FindIndexBruteForce(out findResult);
		}
		if (num < 0)
		{
			findResult = DataPlacementResult.InsertBeginning;
			return 0;
		}
		if (replicatesHistory[num].Data.GetTick() != tick)
		{
			return FindIndexBruteForce(out findResult);
		}
		findResult = DataPlacementResult.Exact;
		return num;
		int FindIndexBruteForce(out DataPlacementResult result)
		{
			if (tick < firstTick)
			{
				result = DataPlacementResult.InsertBeginning;
				return 0;
			}
			if (tick > replicatesHistory[replicatesCount - 1].Data.GetTick())
			{
				result = DataPlacementResult.InsertEnd;
				return replicatesCount;
			}
			for (int i = 0; i < replicatesCount; i++)
			{
				uint tick2 = replicatesHistory[i].Data.GetTick();
				if (tick2 == tick)
				{
					result = DataPlacementResult.Exact;
					return i;
				}
				if (tick2 > tick)
				{
					result = DataPlacementResult.InsertMiddle;
					return i;
				}
			}
			result = DataPlacementResult.Error;
			return -1;
		}
	}
}
