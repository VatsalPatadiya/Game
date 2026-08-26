using NUnit.Framework;
using GameDomain.Model;

namespace GameDomain.Tests.Model
{
    public class BoardStateTests
    {
        [Test]
        public void NewBoardState_StartsWithEmptyCellsAndHistory()
        {
            var board = new BoardState();

            Assert.That(board.Cells, Is.Empty);
            Assert.That(board.MoveHistory, Is.Empty);
            Assert.That(board.Score, Is.EqualTo(0));
            Assert.That(board.ComboCount, Is.EqualTo(0));
        }

        [Test]
        public void NewBoardState_DefaultsToThreeUsesOfEachControl()
        {
            var board = new BoardState();

            Assert.That(board.HintsRemaining, Is.EqualTo(3));
            Assert.That(board.UndosRemaining, Is.EqualTo(3));
            Assert.That(board.ShufflesRemaining, Is.EqualTo(3));
        }

        [Test]
        public void TileSlot_DefaultsToEmptyCoveredByList()
        {
            var slot = new TileSlot();

            Assert.That(slot.CoveredByIds, Is.Not.Null);
            Assert.That(slot.CoveredByIds, Is.Empty);
        }
    }
}
