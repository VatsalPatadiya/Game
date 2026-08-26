using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;

namespace GameDomain.Gameplay
{
    public static class ShuffleService
    {
        public static bool Shuffle(BoardState board, List<TileSlot> shape, Random random, int maxRestarts = 50)
        {
            if (board.ShufflesRemaining <= 0)
                return false;

            var slotsById = shape.ToDictionary(s => s.Id);
            var remainingIds = new HashSet<string>(
                board.Cells.Where(kv => !kv.Value.Cleared).Select(kv => kv.Key));

            for (int attempt = 0; attempt < maxRestarts; attempt++)
            {
                var removalOrder = ReverseConstructionSolver.TryBuildRemovalOrder(slotsById, remainingIds, random);
                if (removalOrder == null)
                    continue;

                var values = ReverseConstructionSolver.AssignValuesFromRemovalOrder(removalOrder, random);

                foreach (var id in remainingIds)
                {
                    board.Cells[id].Value = values[id];
                }

                board.ShufflesRemaining -= 1;
                return true;
            }

            throw new BoardGenerationException(
                "Could not find a solvable shuffle for level " + board.LevelId + " after " + maxRestarts + " attempts.");
        }
    }
}
