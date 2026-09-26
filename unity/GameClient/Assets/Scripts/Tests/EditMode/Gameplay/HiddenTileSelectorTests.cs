using System;
using System.Linq;
using NUnit.Framework;
using GameDomain.Gameplay;
using GameDomain.Model;

namespace GameDomain.Tests.Gameplay
{
    public class HiddenTileSelectorTests
    {
        private static BoardState BoardWithCells(int count)
        {
            var board = new BoardState();
            for (int i = 0; i < count; i++)
                board.Cells["s" + i] = new TileCell { Value = "0" };
            return board;
        }

        [Test]
        public void Apply_DifficultyBelow4_MarksNoCellsHidden()
        {
            var board = BoardWithCells(20);
            HiddenTileSelector.Apply(board, difficulty: 3, new Random(1));

            Assert.That(board.Cells.Values.Any(c => c.IsHiddenTile), Is.False);
            Assert.That(board.Cells.Values.All(c => c.Revealed), Is.True);
        }

        [Test]
        public void Apply_Difficulty4_MarksExactlyThirtyFivePercentHidden()
        {
            var board = BoardWithCells(100);
            HiddenTileSelector.Apply(board, difficulty: 4, new Random(1));

            int hiddenCount = board.Cells.Values.Count(c => c.IsHiddenTile);
            Assert.That(hiddenCount, Is.EqualTo(35));
        }

        [Test]
        public void Apply_HiddenCells_StartNotRevealed_OthersStayRevealed()
        {
            var board = BoardWithCells(20);
            HiddenTileSelector.Apply(board, difficulty: 5, new Random(2));

            foreach (var cell in board.Cells.Values)
                Assert.That(cell.Revealed, Is.EqualTo(!cell.IsHiddenTile));
        }

        [Test]
        public void Apply_Difficulty5_AlsoMarksCellsHidden()
        {
            var board = BoardWithCells(20);
            HiddenTileSelector.Apply(board, difficulty: 5, new Random(3));

            Assert.That(board.Cells.Values.Any(c => c.IsHiddenTile), Is.True);
        }
    }
}
