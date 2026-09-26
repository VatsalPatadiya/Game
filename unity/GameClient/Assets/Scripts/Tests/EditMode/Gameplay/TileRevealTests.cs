using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Gameplay;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Gameplay
{
    public class TileRevealTests
    {
        // A flat row of 4: L0_0/L0_3 are free (row ends), L0_1/L0_2 are not
        // (same fixture/positions MatchValidatorTests already uses).
        private static (BoardState board, Dictionary<string, TileSlot> slotsById) FourTileRow()
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 4 });
            var slotsById = shape.ToDictionary(s => s.Id);
            var board = new BoardState
            {
                Cells = new Dictionary<string, TileCell>
                {
                    ["L0_0"] = new TileCell { Value = "a", Revealed = false, IsHiddenTile = true },
                    ["L0_1"] = new TileCell { Value = "b", Revealed = false, IsHiddenTile = true },
                    ["L0_2"] = new TileCell { Value = "b", Revealed = false, IsHiddenTile = true },
                    ["L0_3"] = new TileCell { Value = "a", Revealed = false, IsHiddenTile = true }
                }
            };
            return (board, slotsById);
        }

        [Test]
        public void TryReveal_FreeFaceDownTile_RevealsItAndBecomesPeeked()
        {
            var (board, slotsById) = FourTileRow();

            bool result = TileReveal.TryReveal(board, slotsById, "L0_0", out string reHidden);

            Assert.That(result, Is.True);
            Assert.That(reHidden, Is.Null);
            Assert.That(board.Cells["L0_0"].Revealed, Is.True);
            Assert.That(board.PeekedTileId, Is.EqualTo("L0_0"));
        }

        [Test]
        public void TryReveal_SecondDifferentFaceDownTile_ReHidesThePreviousPeek()
        {
            var (board, slotsById) = FourTileRow();
            TileReveal.TryReveal(board, slotsById, "L0_0", out _);

            bool result = TileReveal.TryReveal(board, slotsById, "L0_3", out string reHidden);

            Assert.That(result, Is.True);
            Assert.That(reHidden, Is.EqualTo("L0_0"));
            Assert.That(board.Cells["L0_0"].Revealed, Is.False);
            Assert.That(board.Cells["L0_3"].Revealed, Is.True);
            Assert.That(board.PeekedTileId, Is.EqualTo("L0_3"));
        }

        [Test]
        public void TryReveal_NotFreeTile_ReturnsFalseAndChangesNothing()
        {
            var (board, slotsById) = FourTileRow();

            bool result = TileReveal.TryReveal(board, slotsById, "L0_1", out string reHidden);

            Assert.That(result, Is.False);
            Assert.That(reHidden, Is.Null);
            Assert.That(board.Cells["L0_1"].Revealed, Is.False);
            Assert.That(board.PeekedTileId, Is.Null);
        }

        [Test]
        public void TryReveal_SameTileCalledTwice_StaysPeekedWithNoReHide()
        {
            var (board, slotsById) = FourTileRow();
            TileReveal.TryReveal(board, slotsById, "L0_0", out _);

            bool result = TileReveal.TryReveal(board, slotsById, "L0_0", out string reHidden);

            Assert.That(result, Is.True);
            Assert.That(reHidden, Is.Null);
            Assert.That(board.PeekedTileId, Is.EqualTo("L0_0"));
        }
    }
}
