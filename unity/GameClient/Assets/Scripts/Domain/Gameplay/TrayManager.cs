using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;

namespace GameDomain.Gameplay
{
    public static class TrayManager
    {
        public static bool TryPushToTray(BoardState board, Dictionary<string, TileSlot> slotsById, string slotId)
        {
            if (board.IsGameOver) return false;

            if (!board.Cells.TryGetValue(slotId, out var cell) || cell.Cleared)
                return false;

            // Check if it's already in the tray
            if (board.TrayTileIds.Contains(slotId))
                return false;

            // Check if tray is full
            if (board.TrayTileIds.Count >= board.MaxTraySize)
                return false;

            // Compute remaining tiles (not cleared and not in tray)
            var remaining = new HashSet<string>(
                board.Cells.Where(kv => !kv.Value.Cleared && !board.TrayTileIds.Contains(kv.Key)).Select(kv => kv.Key));

            if (!FreedomRuleCalculator.IsFree(slotsById[slotId], remaining))
                return false;

            // Push to tray
            board.TrayTileIds.Add(slotId);

            // Check for match
            CheckForMatches(board);

            // Check for game over
            if (board.TrayTileIds.Count >= board.MaxTraySize)
            {
                board.IsGameOver = true;
            }

            return true;
        }

        private static void CheckForMatches(BoardState board)
        {
            // We look for any 2 tiles in the tray with the same value
            var valueToSlotIds = new Dictionary<string, List<string>>();
            foreach (var id in board.TrayTileIds)
            {
                var val = board.Cells[id].Value;
                if (!valueToSlotIds.TryGetValue(val, out var list))
                {
                    list = new List<string>();
                    valueToSlotIds[val] = list;
                }
                list.Add(id);
            }

            foreach (var kv in valueToSlotIds)
            {
                if (kv.Value.Count >= 2)
                {
                    // Found a match!
                    string id1 = kv.Value[0];
                    string id2 = kv.Value[1];

                    board.TrayTileIds.Remove(id1);
                    board.TrayTileIds.Remove(id2);
                    board.Cells[id1].Cleared = true;
                    board.Cells[id2].Cleared = true;

                    board.MoveHistory.Add(new Move
                    {
                        SlotIdA = id1,
                        SlotIdB = id2,
                        ValueA = kv.Key,
                        ValueB = kv.Key
                    });

                    // Add score points (simple calculation)
                    board.Score += 100;
                    break; // Only process one match per push
                }
            }
        }
    }
}
