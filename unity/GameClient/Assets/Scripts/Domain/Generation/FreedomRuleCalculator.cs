using System.Collections.Generic;
using GameDomain.Model;

namespace GameDomain.Generation
{
    public static class FreedomRuleCalculator
    {
        public static bool IsFree(TileSlot slot, HashSet<string> remainingSlotIds)
        {
            foreach (var coveredById in slot.CoveredByIds)
            {
                if (remainingSlotIds.Contains(coveredById))
                    return false;
            }

            bool leftOpen = slot.LeftNeighborId == null || !remainingSlotIds.Contains(slot.LeftNeighborId);
            bool rightOpen = slot.RightNeighborId == null || !remainingSlotIds.Contains(slot.RightNeighborId);

            return leftOpen || rightOpen;
        }

        public static List<TileSlot> ComputeFreeSlots(Dictionary<string, TileSlot> slotsById, HashSet<string> remainingSlotIds)
        {
            var free = new List<TileSlot>();
            foreach (var id in remainingSlotIds)
            {
                var slot = slotsById[id];
                if (IsFree(slot, remainingSlotIds))
                    free.Add(slot);
            }
            return free;
        }
    }
}
