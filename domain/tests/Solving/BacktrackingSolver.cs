using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;

namespace GameDomain.Tests.Solving
{
    public static class BacktrackingSolver
    {
        public static bool IsSolvable(List<TileSlot> shape, Dictionary<string, string> valuesBySlotId)
        {
            var slotsById = shape.ToDictionary(s => s.Id);
            var remaining = new HashSet<string>(shape.Select(s => s.Id));
            var deadStates = new HashSet<string>();
            return TrySolve(slotsById, valuesBySlotId, remaining, deadStates);
        }

        private static bool TrySolve(
            Dictionary<string, TileSlot> slotsById,
            Dictionary<string, string> valuesBySlotId,
            HashSet<string> remaining,
            HashSet<string> deadStates)
        {
            if (remaining.Count == 0)
                return true;

            string stateKey = string.Join(",", remaining.OrderBy(id => id));
            if (deadStates.Contains(stateKey))
                return false;

            var freeSlots = FreedomRuleCalculator.ComputeFreeSlots(slotsById, remaining);

            for (int i = 0; i < freeSlots.Count; i++)
            {
                for (int j = i + 1; j < freeSlots.Count; j++)
                {
                    var a = freeSlots[i];
                    var b = freeSlots[j];
                    if (valuesBySlotId[a.Id] != valuesBySlotId[b.Id])
                        continue;

                    remaining.Remove(a.Id);
                    remaining.Remove(b.Id);

                    if (TrySolve(slotsById, valuesBySlotId, remaining, deadStates))
                        return true;

                    remaining.Add(a.Id);
                    remaining.Add(b.Id);
                }
            }

            deadStates.Add(stateKey);
            return false;
        }
    }
}
