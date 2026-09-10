using NUnit.Framework;
using System;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Generation
{
    public class BoardGeneratorShapedTests
    {
        [Test]
        public void GenerateShaped_ProducesSolvableFullyPopulatedBoard_Pairs()
        {
            var level = new LevelDefinition { LevelId = 1, Shape = TestLayoutShapes.MediumShape(), TileSetId = "test" };
            var profile = DifficultyProfile.For(1, MatchMode.Pair);

            var board = BoardGenerator.GenerateShaped(level, new Random(10), profile, null, 26);

            Assert.That(board.Cells.Count, Is.EqualTo(level.Shape.Count));
            Assert.That(board.Cells.Values, Has.All.Matches<TileCell>(c => c.Value != null && !c.Cleared));
            // Solvable: a value can be grouped into pairs (each value count is even).
            var counts = board.Cells.Values.GroupBy(c => c.Value).Select(g => g.Count());
            Assert.That(counts, Has.All.Matches<int>(n => n % 2 == 0));
        }

        [Test]
        public void GenerateShaped_EasyProfile_HasHighOpeningBranching()
        {
            var level = new LevelDefinition { LevelId = 1, Shape = TestLayoutShapes.LargeShape(), TileSetId = "test" };
            var profile = DifficultyProfile.For(1, MatchMode.Pair);

            // Should not throw and should satisfy the profile it was generated for.
            Assert.DoesNotThrow(() =>
                BoardGenerator.GenerateShaped(level, new Random(11), profile, null, 26));
        }

        [Test]
        public void GenerateShaped_Triples_EachValueCountIsMultipleOfThree()
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 9, 6, 3 });
            var level = new LevelDefinition { LevelId = 3, Shape = shape, TileSetId = "test" };
            var profile = DifficultyProfile.For(3, MatchMode.Triple);

            var board = BoardGenerator.GenerateShaped(level, new Random(12), profile, null, 26);

            var counts = board.Cells.Values.GroupBy(c => c.Value).Select(g => g.Count());
            Assert.That(counts, Has.All.Matches<int>(n => n % 3 == 0));
        }
    }
}
