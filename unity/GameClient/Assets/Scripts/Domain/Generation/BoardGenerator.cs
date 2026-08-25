using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Model;

namespace GameDomain.Generation
{
    public static class BoardGenerator
    {
        public static BoardState Generate(LevelDefinition level, Random random, int maxRestarts = 50)
        {
            var slotsById = level.Shape.ToDictionary(s => s.Id);
            var allIds = new HashSet<string>(slotsById.Keys);

            for (int attempt = 0; attempt < maxRestarts; attempt++)
            {
                var removalOrder = ReverseConstructionSolver.TryBuildRemovalOrder(slotsById, allIds, random);
                if (removalOrder == null)
                    continue;

                var values = ReverseConstructionSolver.AssignValuesFromRemovalOrder(removalOrder, random);

                var board = new BoardState
                {
                    LevelId = level.LevelId,
                    Cells = new Dictionary<string, TileCell>()
                };

                foreach (var id in allIds)
                {
                    board.Cells[id] = new TileCell { Value = values[id], Cleared = false };
                }

                return board;
            }

            throw new BoardGenerationException(
                "Could not generate a solvable board for level " + level.LevelId + " after " + maxRestarts + " attempts.");
        }
    }
}
