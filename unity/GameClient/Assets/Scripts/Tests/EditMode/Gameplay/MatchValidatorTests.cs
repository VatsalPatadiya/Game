using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Gameplay;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Gameplay
{
    public class MatchValidatorTests
    {
        [Test]
        public void TryMatch_TwoFreeEqualTiles_ClearsBothAndReturnsTrue()
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 4 });
            var slotsById = shape.ToDictionary(s => s.Id);
            var board = new BoardState
            {
                Cells = new Dictionary<string, TileCell>
                {
                    ["L0_0"] = new TileCell { Value = "a" },
                    ["L0_1"] = new TileCell { Value = "b" },
                    ["L0_2"] = new TileCell { Value = "b" },
                    ["L0_3"] = new TileCell { Value = "a" }
                }
            };

            bool result = MatchValidator.TryMatch(board, slotsById, "L0_0", "L0_3");

            Assert.That(result, Is.True);
            Assert.That(board.Cells["L0_0"].Cleared, Is.True);
            Assert.That(board.Cells["L0_3"].Cleared, Is.True);
            Assert.That(board.MoveHistory.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryMatch_UnequalValues_ReturnsFalseAndLeavesBoardUnchanged()
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 4 });
            var slotsById = shape.ToDictionary(s => s.Id);
            var board = new BoardState
            {
                Cells = new Dictionary<string, TileCell>
                {
                    ["L0_0"] = new TileCell { Value = "a" },
                    ["L0_1"] = new TileCell { Value = "b" },
                    ["L0_2"] = new TileCell { Value = "b" },
                    ["L0_3"] = new TileCell { Value = "c" }
                }
            };

            bool result = MatchValidator.TryMatch(board, slotsById, "L0_0", "L0_3");

            Assert.That(result, Is.False);
            Assert.That(board.Cells["L0_0"].Cleared, Is.False);
            Assert.That(board.MoveHistory.Count, Is.EqualTo(0));
        }

        [Test]
        public void TryMatch_EqualValuesButNotFree_ReturnsFalse()
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 4 });
            var slotsById = shape.ToDictionary(s => s.Id);
            var board = new BoardState
            {
                Cells = new Dictionary<string, TileCell>
                {
                    ["L0_0"] = new TileCell { Value = "a" },
                    ["L0_1"] = new TileCell { Value = "x" },
                    ["L0_2"] = new TileCell { Value = "x" },
                    ["L0_3"] = new TileCell { Value = "b" }
                }
            };

            bool result = MatchValidator.TryMatch(board, slotsById, "L0_1", "L0_2");

            Assert.That(result, Is.False);
        }
    }
}
