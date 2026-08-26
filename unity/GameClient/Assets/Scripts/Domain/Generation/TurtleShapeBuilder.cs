using System.Collections.Generic;
using GameDomain.Model;

namespace GameDomain.Generation
{
    // A deep, tapered stack - each layer a centered rectangle narrower than
    // the one below, no hollow - matching a reference screenshot that showed
    // a dense pile of overlapping cards, not the earlier hollow "turtle"
    // silhouette. Held at exactly 31 pairs (62 tiles) - the same total this
    // project has already verified safe for BacktrackingSolver.IsSolvable
    // (a naive, non-memoized-on-success backtracking search whose runtime
    // grows steeply with pair count: 40 pairs alone was already 6-7x
    // slower than this size's known ~84s/200-boards, and 55 pairs didn't
    // finish in 40+ minutes). More visual depth comes from spreading the
    // same 62 tiles across 4 narrower layers instead of the old 3-layer
    // big-base-plus-small-cap split, not from adding tiles. This is an
    // original layout, not a tile-by-tile trace; it produces the same
    // TileSlot data (CoveredByIds/LeftNeighborId/RightNeighborId), so no
    // matching/freedom-rule domain logic needed to change.
    public static class TurtleShapeBuilder
    {
        public static List<TileSlot> Build()
        {
            var slots = new List<TileSlot>();
            var byPos = new Dictionary<(int x, int y, int l), TileSlot>();

            // Every layer is a strict, centered subset of the one below it,
            // so nothing ever floats over open space.
            for (int y = 0; y <= 3; y++) AddRow(slots, byPos, 0, 0, 8, y); // 9x4 = 36
            for (int y = 0; y <= 2; y++) AddRow(slots, byPos, 1, 1, 7, y); // 7x3 = 21
            AddRow(slots, byPos, 2, 3, 5, 0);                              // 3x1 = 3
            AddRow(slots, byPos, 3, 3, 4, 0);                              // 2x1 = 2
            // Total: 62 tiles (31 pairs) - same size already proven safe

            ComputeNeighborsAndCovering(slots, byPos);

            return slots;
        }

        private static void AddRow(
            List<TileSlot> slots, Dictionary<(int x, int y, int l), TileSlot> byPos,
            int layer, int startX, int endXInclusive, int y)
        {
            for (int x = startX; x <= endXInclusive; x++)
            {
                var slot = new TileSlot
                {
                    Id = "U_" + layer + "_" + x + "_" + y,
                    X = x,
                    Y = -y, // render downwards, matching the rest of the project's convention
                    Layer = layer,
                    CoveredByIds = new List<string>()
                };
                slots.Add(slot);
                byPos[(x, y, layer)] = slot;
            }
        }

        private static void ComputeNeighborsAndCovering(
            List<TileSlot> slots, Dictionary<(int x, int y, int l), TileSlot> byPos)
        {
            int maxLayer = 0;
            foreach (var slot in slots)
                if (slot.Layer > maxLayer) maxLayer = slot.Layer;

            foreach (var slot in slots)
            {
                int x = slot.X;
                int y = -slot.Y;
                int l = slot.Layer;

                if (byPos.TryGetValue((x - 1, y, l), out var left))
                    slot.LeftNeighborId = left.Id;
                if (byPos.TryGetValue((x + 1, y, l), out var right))
                    slot.RightNeighborId = right.Id;

                if (l < maxLayer && byPos.TryGetValue((x, y, l + 1), out var above))
                    slot.CoveredByIds.Add(above.Id);
            }
        }
    }
}
