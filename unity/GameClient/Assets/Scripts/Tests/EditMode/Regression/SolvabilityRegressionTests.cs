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
