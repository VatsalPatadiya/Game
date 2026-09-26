using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;

namespace GameDomain.Tests.Generation
{
    public class TurtleDifficultyTests
    {
        [Test]
        public void BuildForDifficulty_AllTiers_ProduceEvenTileCounts()
        {
            for (int d = 1; d <= 5; d++)
            {
                var shape = TurtleShapeBuilder.BuildForDifficulty(d);
                Assert.That(shape.Count % 2, Is.EqualTo(0), $"difficulty {d} must have an even (pairable) tile count");
                Assert.That(shape.Count, Is.GreaterThan(0));
            }
        }

        [Test]
        public void BuildForDifficulty_TileCount_IncreasesWithDifficulty()
        {
            int prev = 0;
            for (int d = 1; d <= 5; d++)
            {
                int count = TurtleShapeBuilder.BuildForDifficulty(d).Count;
                Assert.That(count, Is.GreaterThan(prev), $"difficulty {d} should have more tiles than {d - 1}");
                prev = count;
            }
        }

        [Test]
        public void BuildForDifficulty_ClampsOutOfRange()
        {
            Assert.That(TurtleShapeBuilder.BuildForDifficulty(0).Count,
                Is.EqualTo(TurtleShapeBuilder.BuildForDifficulty(1).Count));
            Assert.That(TurtleShapeBuilder.BuildForDifficulty(99).Count,
                Is.EqualTo(TurtleShapeBuilder.BuildForDifficulty(5).Count));
        }

        [Test]
        public void BuildForDifficulty_EveryTier_GeneratesAValidBoard()
        {
            for (int d = 1; d <= 5; d++)
            {
                var shape = TurtleShapeBuilder.BuildForDifficulty(d);
                var level = new LevelDefinition { LevelId = d, Shape = shape, TileSetId = "default" };
                BoardState board = null;
                Assert.DoesNotThrow(() => board = BoardGenerator.Generate(level, new Random(100 + d)),
                    $"difficulty {d} board must be generatable (a valid pair-removal order exists)");
                // Each value appears an even number of times (pairs).
                var counts = board.Cells.Values.GroupBy(c => c.Value).Select(g => g.Count());
                Assert.That(counts, Has.All.Matches<int>(n => n % 2 == 0));
            }
        }

        [Test]
        public void Build_ParameterlessClassicTurtle_StillEvenAndNonEmpty()
        {
            var shape = TurtleShapeBuilder.Build();
            Assert.That(shape.Count, Is.GreaterThan(0));
            Assert.That(shape.Count % 2, Is.EqualTo(0));
        }
    }
}
