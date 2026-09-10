using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Model;

namespace GameDomain.Generation
{
    // Builds a solvable removal order in groups of `groupSize`, biased so the opening
    // (first `openingFraction` of steps) maximizes newly-exposed tiles (many matches
    // available early = easy) and the remainder minimizes exposure (matches scarce =
    // hard). Because every emitted group was simultaneously free, following the order
    // clears the board without overflowing the tray -> guaranteed solvable/tray-safe.
    public static class BranchingOrderBuilder
    {
        public static List<string[]> Build(
            Dictionary<string, TileSlot> slotsById, HashSet<string> slotIds,
            Random random, int groupSize, float openingFraction)
        {
            var remaining = new HashSet<string>(slotIds);
            var order = new List<string[]>();
            int totalGroups = slotIds.Count / groupSize;
            int openingGroups = (int)Math.Ceiling(totalGroups * openingFraction);

            while (remaining.Count > 0)
            {
                var free = FreedomRuleCalculator.ComputeFreeSlots(slotsById, remaining);
                if (free.Count < groupSize) return null;

                bool maximize = order.Count < openingGroups;
                var group = PickGroup(slotsById, remaining, free, groupSize, maximize, random);

                order.Add(group);
                foreach (var id in group) remaining.Remove(id);
            }

            return order;
        }

        // Greedily pick `groupSize` free tiles whose removal exposes the most (maximize)
        // or fewest (minimize) new tiles. Randomized tie-breaking keeps boards varied.
        private static string[] PickGroup(
            Dictionary<string, TileSlot> slotsById, HashSet<string> remaining,
            List<TileSlot> free, int groupSize, bool maximize, Random random)
        {
            // Shuffle the free list so equal-scoring candidates vary by seed.
            for (int i = free.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (free[i], free[j]) = (free[j], free[i]);
            }

            var freeIds = free.Select(s => s.Id).ToList();

            // Enumerate candidate groups as the first `groupSize` after sorting free
            // tiles by individual exposure gain (cheap, avoids O(n^choose) blowup).
            var scored = freeIds
                .Select(id => (id, gain: ExposureGain(slotsById, remaining, id)))
                .OrderBy(t => maximize ? -t.gain : t.gain)
                .ThenBy(_ => random.Next())
                .Select(t => t.id)
                .Take(groupSize)
                .ToArray();

            return scored;
        }

        // How many currently-blocked tiles become free if `id` is removed.
        private static int ExposureGain(
            Dictionary<string, TileSlot> slotsById, HashSet<string> remaining, string id)
        {
            var after = new HashSet<string>(remaining);
            after.Remove(id);

            int gain = 0;
            foreach (var otherId in remaining)
            {
                if (otherId == id) continue;
                var slot = slotsById[otherId];
                bool wasFree = FreedomRuleCalculator.IsFree(slot, remaining);
                bool nowFree = FreedomRuleCalculator.IsFree(slot, after);
                if (!wasFree && nowFree) gain++;
            }
            return gain;
        }
    }
}
