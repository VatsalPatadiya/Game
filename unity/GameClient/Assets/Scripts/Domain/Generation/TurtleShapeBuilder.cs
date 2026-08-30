using System.Collections.Generic;
using GameDomain.Model;

namespace GameDomain.Generation
{
    // Classic mahjong "turtle": a stacked pyramid where each layer is offset by
    // HALF a tile from the one below. Coordinates are in HALF-tile units, so
    // same-layer tiles step by 2 and each higher layer sits at the opposite
    // parity - i.e. shifted half a tile diagonally. An upper tile therefore
    // STRADDLES and covers up to a 2x2 block of the tiles beneath it, exactly
    // like real mahjong. Because the half-tile offset lives in the coordinates
    // themselves, the rendered overlap == the domain coverage: no fake per-layer
    // render offset is needed, and a tile that looks tucked under another really
    // is covered (and non-free). 40 tiles (20 pairs) - the solver-verified size.
    //
    // Produces the same TileSlot data shape (CoveredByIds / LeftNeighborId /
    // RightNeighborId) the rest of the domain already consumes, so no
    // matching/freedom-rule logic changes.
    public static class TurtleShapeBuilder
    {
        // Centre positions per layer, in half-tile units. Layers alternate parity
        // (odd, even, odd, even) so every layer is offset half a tile from the one
        // below. Each is a centred rectangle narrower than the layer under it, so
        // the pile tapers to a cap and nothing floats over open space.
        // 48 tiles = 16 triples (must be a multiple of 3 for triple-match). A 6x4
        // base tapering 6x4 -> 5x3 -> 4x2 -> 1 cap; every upper tile rests on a
        // full 2x2 of the layer below, so nothing floats.
        private static readonly int[][] LayerXs =
        {
            new[] { 1, 3, 5, 7, 9, 11 }, // layer 0: 6 columns
            new[] { 2, 4, 6, 8, 10 },    // layer 1: 5 columns, half-tile inset
            new[] { 3, 5, 7, 9 },        // layer 2: 4 columns
            new[] { 6 },                 // layer 3: 1-tile cap
        };
        private static readonly int[][] LayerYs =
        {
            new[] { 1, 3, 5, 7 },        // layer 0: 4 rows  -> 6x4 = 24
            new[] { 2, 4, 6 },           // layer 1: 3 rows  -> 5x3 = 15
            new[] { 3, 5 },              // layer 2: 2 rows  -> 4x2 = 8
            new[] { 4 },                 // layer 3: 1 row   -> 1x1 = 1
        };

        public static List<TileSlot> Build()
        {
            var slots = new List<TileSlot>();
            var byPos = new Dictionary<(int x, int y, int l), TileSlot>();

            for (int l = 0; l < LayerXs.Length; l++)
                foreach (var y in LayerYs[l])
                    foreach (var x in LayerXs[l])
                    {
                        var slot = new TileSlot
                        {
                            Id = "U_" + l + "_" + x + "_" + y,
                            X = x,
                            Y = -y, // render downwards, matching the rest of the project's convention
                            Layer = l,
                            CoveredByIds = new List<string>()
                        };
                        slots.Add(slot);
                        byPos[(x, y, l)] = slot;
                    }

            ComputeNeighborsAndCovering(slots, byPos);

            return slots;
        }

        private static void ComputeNeighborsAndCovering(
            List<TileSlot> slots, Dictionary<(int x, int y, int l), TileSlot> byPos)
        {
            foreach (var slot in slots)
            {
                int x = slot.X;
                int y = -slot.Y;
                int l = slot.Layer;

                // A whole tile is 2 half-units wide, so same-layer horizontal
                // neighbours sit at x +/- 2 on the same row.
                if (byPos.TryGetValue((x - 2, y, l), out var left))
                    slot.LeftNeighborId = left.Id;
                if (byPos.TryGetValue((x + 2, y, l), out var right))
                    slot.RightNeighborId = right.Id;

                // Covered by any tile one layer up whose 2x2 footprint overlaps this
                // tile's - i.e. whose centre is within one half-unit on each axis.
                // With alternating parity only the four diagonal (+/-1, +/-1)
                // positions can exist, giving the real 2x2-straddle coverage.
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        if (byPos.TryGetValue((x + dx, y + dy, l + 1), out var above))
                            slot.CoveredByIds.Add(above.Id);
            }
        }
    }
}
