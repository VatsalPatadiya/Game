using System;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Generation
{
    public class BoardGeneratorTests
    {
        [Test]
        public void Generate_ProducesFullyPopulatedBoard_ForSmallShape()
        {
            var level = new LevelDefinition { LevelId = 1, Shape = TestLayoutShapes.SmallShape(), TileSetId = "test" };

            var board = BoardGenerator.Generate(level, new Random(42));

            Assert.That(board.Cells.Count, Is.EqualTo(level.Shape.Count));
            Assert.That(board.Cells.Values, Has.All.Matches<TileCell>(cell => cell.Value != null && !cell.Cleared));
        }

        [Test]
        public void Generate_EachValueAppearsExactlyTwice_ForMediumShape()
        {
            var level = new LevelDefinition { LevelId = 2, Shape = TestLayoutShapes.MediumShape(), TileSetId = "test" };

            var board = BoardGenerator.Generate(level, new Random(7));

            var valueCounts = board.Cells.Values.GroupBy(c => c.Value).ToDictionary(g => g.Key, g => g.Count());
            Assert.That(valueCounts.Values, Has.All.EqualTo(2));
        }

        [Test]
        public void Generate_DoesNotThrow_ForLargeShape()
        {
            var level = new LevelDefinition { LevelId = 3, Shape = TestLayoutShapes.LargeShape(), TileSetId = "test" };

            Assert.DoesNotThrow(() => BoardGenerator.Generate(level, new Random(123)));
        }
    }
}
