using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;

namespace GameDomain.Gameplay
{
    public static class MatchValidator
    {
        public static bool TryMatch(BoardState board, Dictionary<string, TileSlot> slotsById, string slotIdA, string slotIdB)
        {
            if (slotIdA == slotIdB)
                return false;

            if (!board.Cells.TryGetValue(slotIdA, out var cellA) || cellA.Cleared)
                return false;
            if (!board.Cells.TryGetValue(slotIdB, out var cellB) || cellB.Cleared)
                return false;

            if (cellA.Value != cellB.Value)
                return false;

            var remaining = new HashSet<string>(
                board.Cells.Where(kv => !kv.Value.Cleared).Select(kv => kv.Key));

            if (!FreedomRuleCalculator.IsFree(slotsById[slotIdA], remaining))
                return false;
            if (!FreedomRuleCalculator.IsFree(slotsById[slotIdB], remaining))
                return false;

            cellA.Cleared = true;
            cellB.Cleared = true;
            board.MoveHistory.Add(new Move
            {
                SlotIdA = slotIdA,
                SlotIdB = slotIdB,
                ValueA = cellA.Value,
                ValueB = cellB.Value
            });

            return true;
        }
    }
}
