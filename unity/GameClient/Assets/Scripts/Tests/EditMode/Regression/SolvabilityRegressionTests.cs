using NUnit.Framework;
// domain/tests/Regression/SolvabilityRegressionTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;
using GameDomain.Tests.Solving;

namespace GameDomain.Tests.Regression
{
    public class SolvabilityRegressionTests
    {
        private const int IterationsPerShape = 200;

        [Test]
        public void GeneratedBoards_AreAlwaysSolvable_ForSmallShape()
        {
            AssertAllGeneratedBoardsAreSolvable(TestLayoutShapes.SmallShape(), seedOffset: 1000);
        }

        [Test]
        public void GeneratedBoards_AreAlwaysSolvable_ForMediumShape()
        {
            AssertAllGeneratedBoardsAreSolvable(TestLayoutShapes.MediumShape(), seedOffset: 2000);
        }

        [Test]
        public void GeneratedBoards_AreAlwaysSolvable_ForLargeShape()
        {
            AssertAllGeneratedBoardsAreSolvable(TestLayoutShapes.LargeShape(), seedOffset: 3000);
        }

        // Explicit ~50-tile bucket, not just implicitly covered by LargeShape
        // (40 tiles) — a permanent regression guard that boards at this size
        // stay solvable as the game scales up.
        [Test]
        public void GeneratedBoards_AreAlwaysSolvable_ForExtraLargeShape()
        {
            AssertAllGeneratedBoardsAreSolvable(TestLayoutShapes.ExtraLargeShape(), seedOffset: 4000);
        }

        // The deep tapered pyramid (4 layers, 62 tiles) is a meaningfully
        // different topology from the simple rectangular-layered shapes
        // above, so solvability isn't assumed just because those passed -
        // it gets its own explicit, independently-verified bucket.
        [Test]
        public void GeneratedBoards_AreAlwaysSolvable_ForTurtleShape()
        {
            AssertAllGeneratedBoardsAreSolvable(TestLayoutShapes.TurtleShape(), seedOffset: 5000);
        }

        [Test]
        public void GenerateShaped_AllDifficulties_BothModes_AreSolvable()
        {
            foreach (var mode in new[] { MatchMode.Pair, MatchMode.Triple })
            {
                int tiles = mode == MatchMode.Triple ? 18 : 20;
                var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { tiles });

                for (int difficulty = 1; difficulty <= 5; difficulty++)
                {
                    var profile = DifficultyProfile.For(difficulty, mode);

                    for (int seed = 0; seed < 20; seed++)
                    {
                        var level = new LevelDefinition { LevelId = difficulty, Shape = shape, TileSetId = "test" };
                        var board = BoardGenerator.GenerateShaped(level, new Random(seed), profile, null, 26);

                        var values = board.Cells.ToDictionary(kv => kv.Key, kv => kv.Value.Value);
                        bool solvable = BacktrackingSolver.IsSolvable(shape, values, profile.GroupSize);

                        Assert.That(solvable, Is.True,
                            $"unsolvable: difficulty={difficulty} mode={mode} seed={seed}");
                    }
                }
            }
        }

        private static void AssertAllGeneratedBoardsAreSolvable(List<TileSlot> shape, int seedOffset)
        {
            for (int i = 0; i < IterationsPerShape; i++)
            {
                var level = new LevelDefinition { LevelId = i, Shape = shape, TileSetId = "test" };
                var board = BoardGenerator.Generate(level, new Random(seedOffset + i));

                var values = board.Cells.ToDictionary(kv => kv.Key, kv => kv.Value.Value);
                bool solvable = BacktrackingSolver.IsSolvable(shape, values);

                Assert.That(solvable, Is.True, "Board seed " + (seedOffset + i) + " for shape with " + shape.Count + " slots was not solvable.");
            }
        }
    }
}
