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
                    MovesRemaining = level.MovesBudget,
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

        // Triple-match variant: values come in threes and the board is guaranteed
        // beatable (built from a valid triple removal order). Requires the shape's
        // tile count to be a multiple of 3.
        public static BoardState GenerateTriples(LevelDefinition level, Random random, int maxRestarts = 50)
        {
            var slotsById = level.Shape.ToDictionary(s => s.Id);
            var allIds = new HashSet<string>(slotsById.Keys);

            for (int attempt = 0; attempt < maxRestarts; attempt++)
            {
                var removalOrder = ReverseConstructionSolver.TryBuildRemovalOrderTriples(slotsById, allIds, random);
                if (removalOrder == null)
                    continue;

                var values = ReverseConstructionSolver.AssignValuesFromRemovalOrderTriples(removalOrder, random);

                var board = new BoardState
                {
                    LevelId = level.LevelId,
                    Cells = new Dictionary<string, TileCell>()
                };

                foreach (var id in allIds)
                    board.Cells[id] = new TileCell { Value = values[id], Cleared = false };

                return board;
            }

            throw new BoardGenerationException(
                "Could not generate a solvable triple board for level " + level.LevelId + " after " + maxRestarts + " attempts.");
        }

        // Difficulty-shaped variant: builds a front-loaded removal order (see
        // BranchingOrderBuilder), measures its branching curve (BranchingSimulator),
        // and reseeds until the DifficultyProfile accepts the curve (soft target).
        // On exhaustion falls back to any solvable order without the branching
        // requirement -- solvability is never sacrificed for difficulty shape.
        public static BoardState GenerateShaped(
            LevelDefinition level, Random random, DifficultyProfile profile,
            int[] clusterIdByModel, int modelCount, int maxRestarts = 200)
        {
            var slotsById = level.Shape.ToDictionary(s => s.Id);
            var allIds = new HashSet<string>(slotsById.Keys);
            int groupSize = profile.GroupSize;

            List<string[]> chosenOrder = null;

            for (int attempt = 0; attempt < maxRestarts; attempt++)
            {
                var order = BranchingOrderBuilder.Build(
                    slotsById, new HashSet<string>(allIds), random, groupSize, profile.OpeningFraction);
                if (order == null) continue;

                var curve = BranchingSimulator.Profile(slotsById, order);
                if (profile.Accepts(curve)) { chosenOrder = order; break; }
            }

            // Fallback: a neutral (still front-loaded but unverified) solvable order. Solvability
            // is preserved because BranchingOrderBuilder only ever groups co-free tiles.
            if (chosenOrder == null)
            {
                for (int attempt = 0; attempt < maxRestarts && chosenOrder == null; attempt++)
                    chosenOrder = BranchingOrderBuilder.Build(
                        slotsById, new HashSet<string>(allIds), random, groupSize, profile.OpeningFraction);

                if (chosenOrder == null)
                    throw new BoardGenerationException(
                        "Could not generate a solvable board for level " + level.LevelId +
                        " after " + maxRestarts + " attempts.");

                System.Diagnostics.Debug.WriteLine(
                    "Difficulty profile not met for level " + level.LevelId + "; used fallback board.");
            }

            var values = PaletteSelector.AssignValues(
                chosenOrder, clusterIdByModel, modelCount, profile.ConfusabilityLevel, random);

            var board = new BoardState
            {
                LevelId = level.LevelId,
                MovesRemaining = level.MovesBudget,
                Cells = new Dictionary<string, TileCell>()
            };
            foreach (var id in allIds)
                board.Cells[id] = new TileCell { Value = values[id], Cleared = false };

            return board;
        }
    }
}
