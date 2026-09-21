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

            // Bucket keys (see TileMatchRules): exact value for ordinary tiles,
            // shared FLOWER/SEASON label for wildcard tiles - so a free Flower
            // counts as a match for any Flower already in the tray, not just
            // an identical one.
            var trayBuckets = new HashSet<string>(board.TrayTileIds.Select(id => TileMatchRules.BucketKey(board.Cells[id].Value)));

            // 1. A free tile compatible with something already in the tray -> instant clear.
            foreach (var s in free)
                if (trayBuckets.Contains(TileMatchRules.BucketKey(board.Cells[s.Id].Value)))
                    return (s.Id, null);

            // 2. Two free board tiles that are compatible with each other.
            var seenByBucket = new Dictionary<string, string>();
            foreach (var s in free)
            {
                var bucket = TileMatchRules.BucketKey(board.Cells[s.Id].Value);
                if (seenByBucket.TryGetValue(bucket, out var partner))
                    return (partner, s.Id);
                seenByBucket[bucket] = s.Id;
            }

            return (null, null);
        }
    }
}
