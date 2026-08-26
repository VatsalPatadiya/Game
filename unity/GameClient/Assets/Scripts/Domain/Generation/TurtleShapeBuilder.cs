using System.Collections.Generic;
using GameDomain.Model;

namespace GameDomain.Generation
{
    // A classic mahjong-solitaire "turtle" silhouette: wide at the top,
    // narrowing into two separate pillars with a hollow gap through the
    // middle rows, widening again toward the bottom, with a small raised
    // deck+peak on the wide row just above the gap - never over the gap
    // itself, since a layer sitting there would visually fill the hollow
    // (see the comment at the layer-1/2 rows below). This is an original
    // layout inspired by the standard genre convention, not a trace of any
    // specific reference game's board; only the geometry differs from
    // PyramidShapeBuilder - it produces the same TileSlot data
    // (CoveredByIds/LeftNeighborId/RightNeighborId), so none of the
    // matching/freedom-rule domain logic needed to change.
    public static class TurtleShapeBuilder
    {
        public static List<TileSlot> Build()
        {
            var slots = new List<TileSlot>();
            var byPos = new Dictionary<(int x, int y, int l), TileSlot>();

            // Layer 0: wide top/bottom, hollow twin-pillar middle.
            AddRow(slots, byPos, 0, 1, 8, 0); // y=0: x 1..8  (8 tiles)
            AddRow(slots, byPos, 0, 0, 9, 1); // y=1: x 0..9  (10 tiles)
            AddRow(slots, byPos, 0, 0, 2, 2); // y=2: left pillar x 0..2
            AddRow(slots, byPos, 0, 7, 9, 2); // y=2: right pillar x 7..9
            AddRow(slots, byPos, 0, 0, 2, 3); // y=3: left pillar x 0..2
            AddRow(slots, byPos, 0, 7, 9, 3); // y=3: right pillar x 7..9
            AddRow(slots, byPos, 0, 0, 9, 4); // y=4: x 0..9  (10 tiles)
            AddRow(slots, byPos, 0, 1, 8, 5); // y=5: x 1..8  (8 tiles)

            // Layer 1/2: raised deck + peak sit on the wide y=1 transition
            // row, deliberately clear of the hollow's x=3..6 columns at
            // y=2/y=3. The per-layer render offset (TileView) is only a
            // few % of a tile - nowhere near enough to visually reveal a
            // gap underneath a tile that's actually there, so for the
            // hollow to read as hollow in this flat top-down view, no
            // layer can have a tile in that x/y range at all. See
            // docs/superpowers/specs/2026-08-26-pyramid-shape-and-tray-correction.md.
            AddRow(slots, byPos, 1, 3, 6, 1); // y=1 x 3..6 (4 tiles)
            AddRow(slots, byPos, 2, 4, 5, 1); // y=1 x 4..5 (2 tiles, peak)

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
