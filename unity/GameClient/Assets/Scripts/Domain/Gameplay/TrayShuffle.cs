using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Model;

namespace GameDomain.Gameplay
{
    // Shuffle for the tray mechanic: reshuffles the VALUES of the on-board tiles
    // (not the tray, not cleared) among themselves. The value multiset is
    // preserved, so pairs stay balanced. Consumes a shuffle.
    public static class TrayShuffle
    {
        // Returns the affected board slotIds (so the view can refresh their
        // faces), or null if no charges remain.
        public static List<string> Shuffle(BoardState board, Random random)
        {
            if (board.ShufflesRemaining <= 0) return null;

            var ids = board.Cells
                .Where(kv => !kv.Value.Cleared && !board.TrayTileIds.Contains(kv.Key))
                .Select(kv => kv.Key)
                .ToList();
            if (ids.Count == 0) return null;

            var values = ids.Select(id => board.Cells[id].Value).ToList();
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
            for (int i = 0; i < ids.Count; i++)
                board.Cells[ids[i]].Value = values[i];

            board.ShufflesRemaining -= 1;
            return ids;
        }
    }
}
