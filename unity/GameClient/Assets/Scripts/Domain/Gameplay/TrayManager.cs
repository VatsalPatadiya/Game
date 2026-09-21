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

        // Match size for a tray clear. 2 = pair match: two identical tiles in the
        // 4-slot tray clear together; a tray full of distinct tiles is a loss.
        public const int MatchSize = 2;

        private static void CheckForMatches(BoardState board)
        {
            // Group tray tiles by bucket key (exact value for ordinary tiles,
            // shared FLOWER/SEASON label for wildcard tiles - see
            // TileMatchRules) and clear the first group that reaches
            // MatchSize compatible tiles. The bucket key is only a grouping
            // key, never stored - matched tiles keep their own real per-tile
            // Value below, since ShuffleService/UndoStack need actual
            // reusable value strings, not a wildcard-group label.
            var bucketToSlotIds = new Dictionary<string, List<string>>();
            foreach (var id in board.TrayTileIds)
            {
                var bucket = TileMatchRules.BucketKey(board.Cells[id].Value);
                if (!bucketToSlotIds.TryGetValue(bucket, out var list))
                {
                    list = new List<string>();
                    bucketToSlotIds[bucket] = list;
                }
                list.Add(id);
            }

            foreach (var kv in bucketToSlotIds)
            {
                if (kv.Value.Count >= MatchSize)
                {
                    var matched = kv.Value.GetRange(0, MatchSize);
                    var valueA = board.Cells[matched[0]].Value;
                    var valueB = board.Cells[matched[1]].Value;

                    foreach (var id in matched)
                    {
                        board.TrayTileIds.Remove(id);
                        board.Cells[id].Cleared = true;
                    }

                    board.MoveHistory.Add(new Move
                    {
                        // First two ids/value keep the legacy pair fields valid for
                        // the pair-based ShuffleService/UndoStack (deferred powerups).
                        SlotIdA = matched[0],
                        SlotIdB = matched[1],
                        ValueA = valueA,
                        ValueB = valueB,
                        ClearedSlotIds = matched,
                        Value = valueA
                    });

                    board.Score += 100;
                    break; // Only process one match per push
                }
            }
        }
    }
}
