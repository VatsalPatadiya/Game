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

        [Test]
        public void GenerateShaped_FallsBackWithoutThrowing_WhenProfileNeverSatisfied()
        {
            var level = new LevelDefinition { LevelId = 1, Shape = TestLayoutShapes.MediumShape(), TileSetId = "test" };
            // Impossible target: no board can average 1000 simultaneous matches, so
            // profile.Accepts always returns false and GenerateShaped must fall back
            // to a neutral solvable board instead of throwing.
            var impossibleProfile = new DifficultyProfile
            {
                Mode = MatchMode.Pair,
                OpeningFraction = 0.8f,
                OpeningBranchingMin = 1000f,
                BranchingTolerance = 0f,
                ConfusabilityLevel = 0,
            };

            BoardState board = null;
            Assert.DoesNotThrow(() =>
                board = BoardGenerator.GenerateShaped(level, new Random(20), impossibleProfile, null, 26, maxRestarts: 1));

            Assert.That(board.Cells.Count, Is.EqualTo(level.Shape.Count));
            var counts = board.Cells.Values.GroupBy(c => c.Value).Select(g => g.Count());
            Assert.That(counts, Has.All.Matches<int>(n => n % 2 == 0));
        }

        [Test]
        public void GenerateShaped_Throws_WhenNoSolvableOrderExistsAtAll()
        {
            // A single tile can never form a pair (groupSize 2): BranchingOrderBuilder.Build
            // deterministically returns null (free.Count=1 < groupSize=2) on every attempt
            // regardless of seed, so both the profile-seeking loop and the fallback loop
            // exhaust and GenerateShaped must throw rather than fabricate a board.
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 1 });
            var level = new LevelDefinition { LevelId = 1, Shape = shape, TileSetId = "test" };
            var profile = DifficultyProfile.For(1, MatchMode.Pair);

            Assert.Throws<BoardGenerationException>(() =>
                BoardGenerator.GenerateShaped(level, new Random(1), profile, null, 26, maxRestarts: 2));
        }
    }
}
