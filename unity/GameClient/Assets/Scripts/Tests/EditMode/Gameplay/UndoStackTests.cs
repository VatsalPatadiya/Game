using NUnit.Framework;
using System.Collections.Generic;
using GameDomain.Gameplay;
using GameDomain.Model;

namespace GameDomain.Tests.Gameplay
{
    public class UndoStackTests
    {
        [Test]
        public void TryUndo_WithPriorMove_RestoresBothTilesAndReturnsTrue()
        {
            var board = new BoardState
            {
                Cells = new Dictionary<string, TileCell>
                {
                    ["L0_0"] = new TileCell { Value = "a", Cleared = true },
                    ["L0_3"] = new TileCell { Value = "a", Cleared = true }
                },
                MoveHistory = new List<Move>
                {
                    new Move { SlotIdA = "L0_0", SlotIdB = "L0_3", ValueA = "a", ValueB = "a" }
                }
            };

            bool result = UndoStack.TryUndo(board);

            Assert.That(result, Is.True);
            Assert.That(board.Cells["L0_0"].Cleared, Is.False);
            Assert.That(board.Cells["L0_3"].Cleared, Is.False);
            Assert.That(board.MoveHistory.Count, Is.EqualTo(0));
        }

        [Test]
        public void TryUndo_NoMoveHistory_ReturnsFalse()
        {
            var board = new BoardState();

            bool result = UndoStack.TryUndo(board);

            Assert.That(result, Is.False);
        }
    }
}
