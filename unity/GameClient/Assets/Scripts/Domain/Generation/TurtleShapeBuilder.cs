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
        // Hard project cap on board size. A board larger than this at the game's
        // fixed tile size is taller/wider than the play area and would overlap the
        // tray or the bottom buttons, so no level is allowed to exceed it. Set to 54
        // (a full 5-wide, 5-LAYER turtle) - the biggest that fits at the ~170px tile
        // size (6-wide overflows the screen width; this fills the play area vertically
        // as a proper tall pyramid).
        public const int MaxTiles = 54;

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

        // Centred turtle with an EXACT tile count (even, for pair-match). Picks the
        // most compact tapering base (width <= 8 so it fits the board band) whose
        // full taper is >= targetTiles, then trims the excess strictly TOP-DOWN
        // (highest layer first) so every remaining upper tile still rests on a full
        // layer below - no floating tiles, still solvable by construction.
        public static List<TileSlot> BuildWithTileCount(int targetTiles)
        {
            if (targetTiles > MaxTiles) targetTiles = MaxTiles; // enforce the hard cap
            if (targetTiles < 2) targetTiles = 2;
            if (targetTiles % 2 == 1) targetTiles++; // pairs need an even total

            List<(int x, int y, int l)> best = null;
            int bestExcess = int.MaxValue;
            for (int cols = 4; cols <= 8; cols++)
                for (int rows = 3; rows <= cols; rows++)
                {
                    var pos = TaperPositions(cols, rows);
                    int excess = pos.Count - targetTiles;
                    if (excess >= 0 && excess < bestExcess)
                    {
                        bestExcess = excess;
                        best = pos;
                    }
                }
            if (best == null) best = TaperPositions(8, 8); // fallback: plenty of tiles

            // Trim top-down (layer desc, then furthest y/x) to the exact target. Because
            // we remove whole upper layers before touching a lower one, nothing is left
            // floating (a removed tile never supports a still-present tile above it).
            best.Sort((a, b) =>
                a.l != b.l ? b.l - a.l : (a.y != b.y ? b.y - a.y : b.x - a.x));
            while (best.Count > targetTiles) best.RemoveAt(0);

            return BuildFromPositions(best);
        }

        // The centred tapering-turtle positions for a base footprint (no even-count
        // adjustment) - shared by Build(cols,rows) and BuildWithTileCount.
        private static List<(int x, int y, int l)> TaperPositions(int baseCols, int baseRows)
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
            return positions;
        }

        // Board footprint per difficulty tier (1-5), clamped. Produces roughly
        // 18 / 22 / 46 / 68 / 82 tiles - an increasing curve.
        public static List<TileSlot> BuildForDifficulty(int difficulty)
        {
            switch (difficulty <= 1 ? 1 : (difficulty >= 5 ? 5 : difficulty))
            {
                // <=5-wide and <=4 rows so every tier fits the play area at the larger
                // ~170px tile size. Counts: 12 / 20 / 26 / 30 / 40, strictly increasing.
                case 1: return Build(3, 3);   // 12
                case 2: return Build(4, 3);   // 20
                case 3: return Build(5, 3);   // 26
                case 4: return Build(4, 4);   // 30
                default: return BuildWithTileCount(MaxTiles); // 54 (5x5, tall)
            }
        }

        public static List<TileSlot> BuildClassic144()
        {
            var positions = new List<(int x, int y, int l)>();
            
            // To create a perfect "overlapping web" where EVERY layer straddles the one below it,
            // we must alternate parities by shrinking exactly 1 row and 1 col per layer.
            // This builds a perfect 7-layer square pyramid (140 tiles) plus 4 wing tiles (144 total).
            
            int centerX = 14;
            int centerY = 14;

            // L0 (Base): 7x7 square (49 tiles) - Even parity
            for (int x = centerX - 6; x <= centerX + 6; x += 2)
                for (int y = centerY - 6; y <= centerY + 6; y += 2)
                    positions.Add((x, y, 0));
                    
            // Wings on L0 to reach 144 tiles (4 tiles)
            positions.Add((centerX - 8, centerY, 0));
            positions.Add((centerX - 10, centerY, 0));
            positions.Add((centerX + 8, centerY, 0));
            positions.Add((centerX + 10, centerY, 0));

            // L1: 6x6 square (36 tiles) - Odd parity
            for (int x = centerX - 5; x <= centerX + 5; x += 2)
                for (int y = centerY - 5; y <= centerY + 5; y += 2)
                    positions.Add((x, y, 1));

            // L2: 5x5 square (25 tiles) - Even parity
            for (int x = centerX - 4; x <= centerX + 4; x += 2)
                for (int y = centerY - 4; y <= centerY + 4; y += 2)
                    positions.Add((x, y, 2));

            // L3: 4x4 square (16 tiles) - Odd parity
            for (int x = centerX - 3; x <= centerX + 3; x += 2)
                for (int y = centerY - 3; y <= centerY + 3; y += 2)
                    positions.Add((x, y, 3));

            // L4: 3x3 square (9 tiles) - Even parity
            for (int x = centerX - 2; x <= centerX + 2; x += 2)
                for (int y = centerY - 2; y <= centerY + 2; y += 2)
                    positions.Add((x, y, 4));

            // L5: 2x2 square (4 tiles) - Odd parity
            for (int x = centerX - 1; x <= centerX + 1; x += 2)
                for (int y = centerY - 1; y <= centerY + 1; y += 2)
                    positions.Add((x, y, 5));

            // L6: 1x1 cap (1 tile) - Even parity
            positions.Add((centerX, centerY, 6));

            return BuildFromPositions(positions);
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
