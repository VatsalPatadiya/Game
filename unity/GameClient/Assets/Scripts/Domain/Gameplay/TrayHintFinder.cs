using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;

namespace GameDomain.Gameplay
{
    // Hint for the tray-collection mechanic: prefer a free board tile whose value
    // is already sitting in the tray (tapping it clears a pair immediately);
    // otherwise a free tile that has a free same-value partner on the board.
    public static class TrayHintFinder
    {
        // Returns up to two board slotIds to highlight. b is null when the hint is
        // a single tile that completes a tray pair. Both null when stuck.
        public static (string a, string b) FindHint(BoardState board, Dictionary<string, TileSlot> slotsById)
        {
            var remaining = new HashSet<string>(
                board.Cells.Where(kv => !kv.Value.Cleared && !board.TrayTileIds.Contains(kv.Key)).Select(kv => kv.Key));
            var free = FreedomRuleCalculator.ComputeFreeSlots(slotsById, remaining);

            var trayValues = new HashSet<string>(board.TrayTileIds.Select(id => board.Cells[id].Value));

            // 1. A free tile whose value is already in the tray -> instant clear.
            foreach (var s in free)
                if (trayValues.Contains(board.Cells[s.Id].Value))
                    return (s.Id, null);

            // 2. Two free board tiles sharing a value.
            var seenByValue = new Dictionary<string, string>();
            foreach (var s in free)
            {
                var v = board.Cells[s.Id].Value;
                if (seenByValue.TryGetValue(v, out var partner))
                    return (partner, s.Id);
                seenByValue[v] = s.Id;
            }

            return (null, null);
        }
    }
}
