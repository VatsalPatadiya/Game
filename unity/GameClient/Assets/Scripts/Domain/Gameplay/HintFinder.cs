using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;

namespace GameDomain.Gameplay
{
    public static class HintFinder
    {
        public static (string slotIdA, string slotIdB)? FindFreePair(BoardState board, Dictionary<string, TileSlot> slotsById)
        {
            var remaining = new HashSet<string>(
                board.Cells.Where(kv => !kv.Value.Cleared).Select(kv => kv.Key));

            var freeSlots = FreedomRuleCalculator.ComputeFreeSlots(slotsById, remaining);

            for (int i = 0; i < freeSlots.Count; i++)
            {
                for (int j = i + 1; j < freeSlots.Count; j++)
                {
                    var a = freeSlots[i];
                    var b = freeSlots[j];
                    if (board.Cells[a.Id].Value == board.Cells[b.Id].Value)
                        return (a.Id, b.Id);
                }
            }

            return null;
        }
    }
}
