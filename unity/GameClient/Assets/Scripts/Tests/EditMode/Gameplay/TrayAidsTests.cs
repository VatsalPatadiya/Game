using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Gameplay;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Gameplay
{
    public class TrayAidsTests
    {
        private static (BoardState, Dictionary<string, TileSlot>) MakeBoard(
            Dictionary<string, string> values, IEnumerable<string> trayIds = null,
            int undos = 3, int shuffles = 3)
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 4 });
            var slotsById = shape.ToDictionary(s => s.Id);
            var board = new BoardState
            {
                UndosRemaining = undos,
                ShufflesRemaining = shuffles,
                Cells = values.ToDictionary(kv => kv.Key, kv => new TileCell { Value = kv.Value })
            };
            if (trayIds != null) board.TrayTileIds = trayIds.ToList();
            return (board, slotsById);
        }

        [Test]
        public void TrayUndo_PopsLastTrayTile_AndDecrementsCharge()
        {
            var (board, _) = MakeBoard(
                new Dictionary<string, string> { ["L0_0"] = "a", ["L0_1"] = "b", ["L0_2"] = "c", ["L0_3"] = "d" },
                trayIds: new[] { "L0_0", "L0_1" });

            var popped = TrayUndo.TryUndo(board);

            Assert.That(popped, Is.EqualTo("L0_1"));
            Assert.That(board.TrayTileIds, Is.EqualTo(new[] { "L0_0" }));
            Assert.That(board.UndosRemaining, Is.EqualTo(2));
        }

        [Test]
        public void TrayUndo_EmptyTrayOrNoCharges_ReturnsNull()
        {
            var (empty, _) = MakeBoard(new Dictionary<string, string> { ["L0_0"] = "a" });
            Assert.That(TrayUndo.TryUndo(empty), Is.Null);

            var (noCharge, _) = MakeBoard(
                new Dictionary<string, string> { ["L0_0"] = "a" }, trayIds: new[] { "L0_0" }, undos: 0);
            Assert.That(TrayUndo.TryUndo(noCharge), Is.Null);
        }

        [Test]
        public void TrayShuffle_PreservesValueMultiset_ExcludesTray_DecrementsCharge()
        {
            var (board, _) = MakeBoard(
                new Dictionary<string, string> { ["L0_0"] = "a", ["L0_1"] = "b", ["L0_2"] = "a", ["L0_3"] = "b" },
                trayIds: new[] { "L0_3" });

            var ids = TrayShuffle.Shuffle(board, new Random(1));

            Assert.That(ids, Is.Not.Null);
            Assert.That(ids, Does.Not.Contain("L0_3"), "tray tile must be excluded");
            // value multiset over the shuffled (non-tray) tiles is preserved
            var boardValues = ids.Select(id => board.Cells[id].Value).OrderBy(v => v);
            Assert.That(boardValues, Is.EqualTo(new[] { "a", "a", "b" }));
            Assert.That(board.Cells["L0_3"].Value, Is.EqualTo("b"), "tray tile value untouched");
            Assert.That(board.ShufflesRemaining, Is.EqualTo(2));
        }

        [Test]
        public void TrayShuffle_NoCharges_ReturnsNull()
        {
            var (board, _) = MakeBoard(
                new Dictionary<string, string> { ["L0_0"] = "a" }, shuffles: 0);
            Assert.That(TrayShuffle.Shuffle(board, new Random(1)), Is.Null);
        }

        [Test]
        public void TrayHint_FreeTileMatchingTrayValue_ReturnedFirst()
        {
            // L0_0 value "a" is free; "a" is already in the tray (L0_3) -> instant clear.
            var (board, slots) = MakeBoard(
                new Dictionary<string, string> { ["L0_0"] = "a", ["L0_1"] = "b", ["L0_2"] = "c", ["L0_3"] = "a" },
                trayIds: new[] { "L0_3" });

            var (a, b) = TrayHintFinder.FindHint(board, slots);

            Assert.That(a, Is.EqualTo("L0_0"));
            Assert.That(b, Is.Null);
        }

        [Test]
        public void TrayHint_NoTrayMatch_ReturnsFreePair()
        {
            var (board, slots) = MakeBoard(
                new Dictionary<string, string> { ["L0_0"] = "a", ["L0_1"] = "b", ["L0_2"] = "b", ["L0_3"] = "a" });

            var (a, b) = TrayHintFinder.FindHint(board, slots);

            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            Assert.That(board.Cells[a].Value, Is.EqualTo(board.Cells[b].Value), "the pair shares a value");
        }
    }
}
