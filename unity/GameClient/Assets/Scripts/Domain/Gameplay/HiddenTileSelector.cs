using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Model;

namespace GameDomain.Gameplay
{
    // Marks a random subset of a generated board's tiles as face-down, on the
    // two hardest difficulty tiers only. See docs/superpowers/specs/
    // 2026-09-23-hidden-tile-reveal-design.md for the full design.
    public static class HiddenTileSelector
    {
        private const int MinDifficulty = 4;
        private const float Fraction = 0.35f; // within the approved 30-40% range

        public static void Apply(BoardState board, int difficulty, Random random)
        {
            if (difficulty < MinDifficulty) return;

            var ids = board.Cells.Keys.ToList();
            int count = (int)Math.Round(ids.Count * Fraction);
            Shuffle(ids, random);

            for (int i = 0; i < count && i < ids.Count; i++)
            {
                board.Cells[ids[i]].IsHiddenTile = true;
                board.Cells[ids[i]].Revealed = false;
            }
        }

        // Fisher-Yates, same pattern as PaletteSelector.Shuffle.
        private static void Shuffle<T>(IList<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
