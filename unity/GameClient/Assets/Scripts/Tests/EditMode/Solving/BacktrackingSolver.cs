using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;

namespace GameDomain.Tests.Solving
{
    public static class BacktrackingSolver
    {
        public static bool IsSolvable(List<TileSlot> shape, Dictionary<string, string> valuesBySlotId, int groupSize = 2)
        {
            var slotsById = shape.ToDictionary(s => s.Id);
            var remaining = new HashSet<string>(shape.Select(s => s.Id));
            var deadStates = new HashSet<string>();
            return TrySolve(slotsById, valuesBySlotId, remaining, deadStates, groupSize);
        }

        private static bool TrySolve(
            Dictionary<string, TileSlot> slotsById,
            Dictionary<string, string> valuesBySlotId,
            HashSet<string> remaining,
            HashSet<string> deadStates,
            int groupSize)
        {
            if (remaining.Count == 0)
                return true;

            string stateKey = string.Join(",", remaining.OrderBy(id => id));
            if (deadStates.Contains(stateKey))
                return false;

            var freeSlots = FreedomRuleCalculator.ComputeFreeSlots(slotsById, remaining);
            var byValue = freeSlots.GroupBy(s => valuesBySlotId[s.Id]);

            foreach (var group in byValue)
            {
                var candidates = group.ToList();
                if (candidates.Count < groupSize)
                    continue;

                foreach (var combo in Combinations(candidates, groupSize))
                {
                    foreach (var slot in combo) remaining.Remove(slot.Id);

                    if (TrySolve(slotsById, valuesBySlotId, remaining, deadStates, groupSize))
                        return true;

                    foreach (var slot in combo) remaining.Add(slot.Id);
                }
            }

            deadStates.Add(stateKey);
            return false;
        }

        private static IEnumerable<List<TileSlot>> Combinations(List<TileSlot> items, int k)
        {
            if (k == 0)
            {
                yield return new List<TileSlot>();
                yield break;
            }

            for (int i = 0; i <= items.Count - k; i++)
            {
                var head = items[i];
                var rest = items.GetRange(i + 1, items.Count - i - 1);
                foreach (var tail in Combinations(rest, k - 1))
                {
                    var combo = new List<TileSlot> { head };
                    combo.AddRange(tail);
                    yield return combo;
                }
            }
        }
    }
}
