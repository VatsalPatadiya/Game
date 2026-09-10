using System.Collections.Generic;
using GameDomain.Model;

namespace GameDomain.Generation
{
    // Measures how many valid matches are simultaneously available along a board's
    // known (guaranteed-solvable) removal order. At each step it counts how many of
    // the still-remaining removal groups have ALL their tiles currently free. High
    // early = easy to spot matches; low = the player must search. Group-structural,
    // so it ignores final palette values.
    public static class BranchingSimulator
    {
        public static List<int> Profile(Dictionary<string, TileSlot> slotsById, List<string[]> removalOrder)
        {
            var remaining = new HashSet<string>();
            foreach (var group in removalOrder)
                foreach (var id in group)
                    remaining.Add(id);

            var result = new List<int>(removalOrder.Count);

            for (int t = 0; t < removalOrder.Count; t++)
            {
                var freeList = FreedomRuleCalculator.ComputeFreeSlots(slotsById, remaining);
                var freeSet = new HashSet<string>();
                foreach (var slot in freeList) freeSet.Add(slot.Id);

                int available = 0;
                for (int k = t; k < removalOrder.Count; k++)
                {
                    bool allFree = true;
                    foreach (var id in removalOrder[k])
                    {
                        if (!freeSet.Contains(id)) { allFree = false; break; }
                    }
                    if (allFree) available++;
                }
                result.Add(available);

                foreach (var id in removalOrder[t]) remaining.Remove(id);
            }

            return result;
        }
    }
}
