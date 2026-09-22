using NUnit.Framework;
using System.Collections.Generic;
using GameDomain.Gameplay;
using GameDomain.Model;

namespace GameDomain.Tests.Gameplay
{
    public class TrayManagerPairTests
    {
        // Flat board (no covering / no neighbours) so every tile is free and
        // tappable - lets us drive the tray logic directly. MaxTraySize = 4 to
        // match the pair-match tray.
        private static (BoardState board, Dictionary<string, TileSlot> slots) MakeBoard(
            params (string id, string val)[] tiles)
        {
            var slots = new Dictionary<string, TileSlot>();
            var board = new BoardState
            {
                Cells = new Dictionary<string, TileCell>(),
                MaxTraySize = 4
            };
            foreach (var (id, val) in tiles)
            {
                slots[id] = new TileSlot { Id = id, X = 0, Y = 0, Layer = 0, CoveredByIds = new List<string>() };
                board.Cells[id] = new TileCell { Value = val, Cleared = false };
            }
            return (board, slots);
        }

        [Test]
        public void TwoOfAKind_ClearsBoth_AndEmptiesTheirSlots()
        {
            var (board, slots) = MakeBoard(("a", "5"), ("b", "5"));

            Assert.That(TrayManager.TryPushToTray(board, slots, "a"), Is.True);
            Assert.That(TrayManager.TryPushToTray(board, slots, "b"), Is.True);

            Assert.That(board.Cells["a"].Cleared, Is.True);
            Assert.That(board.Cells["b"].Cleared, Is.True);
            Assert.That(board.TrayTileIds, Is.Empty);
            Assert.That(board.IsGameOver, Is.False);
        }

        [Test]
        public void MatchClears_ButLeavesTheNonMatchingTileInTray()
        {
            var (board, slots) = MakeBoard(("a", "5"), ("b", "9"), ("c", "5"));

            TrayManager.TryPushToTray(board, slots, "a"); // tray: 5
            TrayManager.TryPushToTray(board, slots, "b"); // tray: 5, 9
            TrayManager.TryPushToTray(board, slots, "c"); // 5 matches -> a,c clear

            Assert.That(board.Cells["a"].Cleared, Is.True);
            Assert.That(board.Cells["c"].Cleared, Is.True);
            Assert.That(board.Cells["b"].Cleared, Is.False);
            Assert.That(board.TrayTileIds, Is.EquivalentTo(new[] { "b" }));
        }

        [Test]
        public void FourDistinctTiles_FillTray_AndLose()
        {
            var (board, slots) = MakeBoard(("a", "1"), ("b", "2"), ("c", "3"), ("d", "4"));

            TrayManager.TryPushToTray(board, slots, "a");
            TrayManager.TryPushToTray(board, slots, "b");
            TrayManager.TryPushToTray(board, slots, "c");
            TrayManager.TryPushToTray(board, slots, "d");

            Assert.That(board.TrayTileIds.Count, Is.EqualTo(4));
            Assert.That(board.IsGameOver, Is.True);
        }

        [Test]
        public void PushRejected_WhenTileAlreadyInTray()
        {
            var (board, slots) = MakeBoard(("a", "5"), ("b", "6"));

            Assert.That(TrayManager.TryPushToTray(board, slots, "a"), Is.True);
            Assert.That(TrayManager.TryPushToTray(board, slots, "a"), Is.False); // already collected
        }
    }
}
