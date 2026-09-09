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
    // themselves, the rendered overlap == the domain coverage.
    //
    // A "base cols x base rows" turtle tapers each layer inward by one column and
    // one row (staying centred), so every upper tile rests on a full 2x2 of the
    // layer below and nothing floats. Board sizes scale with the base dimensions,
    // which sub-project #7 (content) uses to make levels differ by difficulty.
    public static class TurtleShapeBuilder
    {
        // The classic 48-ish turtle (kept for back-compat / regression tests):
        // a 6x4 base tapering 6x4 -> 5x3 -> 4x2 -> 1 cap.
        private static readonly int[][] LayerXs =
        {
            new[] { 1, 3, 5, 7, 9, 11 },
            new[] { 2, 4, 6, 8, 10 },
            new[] { 3, 5, 7, 9 },
            new[] { 6 },
        };
        private static readonly int[][] LayerYs =
        {
            new[] { 1, 3, 5, 7 },
            new[] { 2, 4, 6 },
            new[] { 3, 5 },
            new[] { 4 },
        };

        public static List<TileSlot> Build()
        {
            var positions = new List<(int x, int y, int l)>();
            for (int l = 0; l < LayerXs.Length; l++)
                foreach (var y in LayerYs[l])
                    foreach (var x in LayerXs[l])
                        positions.Add((x, y, l));
            return BuildFromPositions(positions);
        }

        // A centred turtle of the given base footprint. Tapers one col/row per
        // layer until a layer would be smaller than 2x2. Guarantees an EVEN tile
        // count (pairs) by dropping the topmost cap when odd.
        public static List<TileSlot> Build(int baseCols, int baseRows)
        {
            var positions = new List<(int x, int y, int l)>();
            for (int l = 0; baseCols - l >= 2 && baseRows - l >= 2; l++)
            {
                int cols = baseCols - l;
                int rows = baseRows - l;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                        positions.Add(((1 + l) + 2 * c, (1 + l) + 2 * r, l));
            }

            if (positions.Count % 2 == 1)
            {
                // Drop the topmost / furthest cap so the board stays even and
                // still solvable (removing a cap only frees tiles below it).
                positions.Sort((a, b) =>
                    a.l != b.l ? b.l - a.l : (a.y != b.y ? b.y - a.y : b.x - a.x));
                positions.RemoveAt(0);
            }

            return BuildFromPositions(positions);
        }

        // Board footprint per difficulty tier (1-5), clamped. Produces roughly
        // 18 / 22 / 46 / 68 / 82 tiles - an increasing curve.
        public static List<TileSlot> BuildForDifficulty(int difficulty)
        {
            switch (difficulty <= 1 ? 1 : (difficulty >= 5 ? 5 : difficulty))
            {
                case 1: return Build(4, 3);
                case 2: return Build(5, 3);
                case 3: return Build(6, 4);
                case 4: return Build(6, 5);
                default: return Build(7, 5);
            }
        }

        private static List<TileSlot> BuildFromPositions(List<(int x, int y, int l)> positions)
        {
            var slots = new List<TileSlot>();
            var byPos = new Dictionary<(int x, int y, int l), TileSlot>();

            foreach (var (x, y, l) in positions)
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

                if (byPos.TryGetValue((x - 2, y, l), out var left))
                    slot.LeftNeighborId = left.Id;
                if (byPos.TryGetValue((x + 2, y, l), out var right))
                    slot.RightNeighborId = right.Id;

                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        if (byPos.TryGetValue((x + dx, y + dy, l + 1), out var above))
                            slot.CoveredByIds.Add(above.Id);
            }
        }
    }
}
