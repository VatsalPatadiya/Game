using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Gameplay;
using GameDomain.Generation;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Gameplay
{
    public class ShuffleServiceTests
    {
        [Test]
        public void Shuffle_KeepsPositionsFixedAndReassignsValuesInPairs()
        {
            var shape = TestLayoutShapes.MediumShape();
            var level = new LevelDefinition { LevelId = 1, Shape = shape, TileSetId = "test" };
            var board = BoardGenerator.Generate(level, new Random(11));
            var idsBefore = board.Cells.Keys.OrderBy(id => id).ToList();

            ShuffleService.Shuffle(board, shape, new Random(99));

            var idsAfter = board.Cells.Keys.OrderBy(id => id).ToList();
            Assert.That(idsAfter, Is.EqualTo(idsBefore));

            var valueCounts = board.Cells.Values.GroupBy(c => c.Value).ToDictionary(g => g.Key, g => g.Count());
            Assert.That(valueCounts.Values, Has.All.EqualTo(2));
        }

        [Test]
        public void Shuffle_OnlyReassignsUnclearedTiles()
        {
            var shape = TestLayoutShapes.SmallShape();
            var slotsById = shape.ToDictionary(s => s.Id);
            var level = new LevelDefinition { LevelId = 2, Shape = shape, TileSetId = "test" };
            var board = BoardGenerator.Generate(level, new Random(5));

            var hint = HintFinder.FindFreePair(board, slotsById);
            Assert.That(hint, Is.Not.Null);
            MatchValidator.TryMatch(board, slotsById, hint.Value.slotIdA, hint.Value.slotIdB);

            ShuffleService.Shuffle(board, shape, new Random(3));

            Assert.That(board.Cells[hint.Value.slotIdA].Cleared, Is.True);
            Assert.That(board.Cells[hint.Value.slotIdB].Cleared, Is.True);
        }

        [Test]
        public void ShuffleThenUndo_PreservesExactlyTwoUnclearedCellsPerValue()
        {
            var shape = TestLayoutShapes.SmallShape();
            var slotsById = shape.ToDictionary(s => s.Id);
            var level = new LevelDefinition { LevelId = 3, Shape = shape, TileSetId = "test" };
            var board = BoardGenerator.Generate(level, new Random(5));

            var hint = HintFinder.FindFreePair(board, slotsById);
            Assert.That(hint, Is.Not.Null);
            MatchValidator.TryMatch(board, slotsById, hint.Value.slotIdA, hint.Value.slotIdB);

            ShuffleService.Shuffle(board, shape, new Random(3));

            var undone = UndoStack.TryUndo(board);
            Assert.That(undone, Is.True);

            var unclearedValueCounts = board.Cells
                .Where(kv => !kv.Value.Cleared)
                .GroupBy(kv => kv.Value.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            Assert.That(unclearedValueCounts.Values, Has.All.EqualTo(2),
                "Every value among uncleared cells must appear on exactly 2 uncleared cells after shuffle+undo.");
        }
    }
}
