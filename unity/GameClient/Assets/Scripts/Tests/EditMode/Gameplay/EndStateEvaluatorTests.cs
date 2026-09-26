using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Gameplay;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Gameplay
{
    public class EndStateEvaluatorTests
    {
        private static (BoardState, Dictionary<string, TileSlot>) MakeBoard(
            Dictionary<string, string> values, int movesRemaining, int shufflesRemaining)
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 4 });
            var slotsById = shape.ToDictionary(s => s.Id);
            var board = new BoardState
            {
                MovesRemaining = movesRemaining,
                ShufflesRemaining = shufflesRemaining,
                Cells = values.ToDictionary(kv => kv.Key, kv => new TileCell { Value = kv.Value })
            };
            return (board, slotsById);
        }

        [Test]
        public void Evaluate_AllCleared_ReturnsWon()
        {
            var (board, slots) = MakeBoard(
                new Dictionary<string, string> { ["L0_0"] = "a", ["L0_1"] = "b", ["L0_2"] = "b", ["L0_3"] = "a" },
                movesRemaining: -1, shufflesRemaining: 3);
            foreach (var c in board.Cells.Values) c.Cleared = true;

            Assert.That(EndStateEvaluator.Evaluate(board, slots), Is.EqualTo(EndState.Won));
        }

        [Test]
        public void Evaluate_MovesExhaustedWithTilesLeft_ReturnsLost()
        {
            var (board, slots) = MakeBoard(
                new Dictionary<string, string> { ["L0_0"] = "a", ["L0_1"] = "b", ["L0_2"] = "b", ["L0_3"] = "a" },
                movesRemaining: 0, shufflesRemaining: 3);

            Assert.That(EndStateEvaluator.Evaluate(board, slots), Is.EqualTo(EndState.Lost));
        }

        [Test]
        public void Evaluate_UnlimitedMovesWithPlayableBoard_ReturnsInProgress()
        {
            var (board, slots) = MakeBoard(
                new Dictionary<string, string> { ["L0_0"] = "a", ["L0_1"] = "b", ["L0_2"] = "b", ["L0_3"] = "a" },
                movesRemaining: -1, shufflesRemaining: 3);

            Assert.That(EndStateEvaluator.Evaluate(board, slots), Is.EqualTo(EndState.InProgress));
        }

        [Test]
        public void Evaluate_StuckButShufflesRemain_ReturnsInProgress()
        {
            // No two free tiles share a value => FindFreePair is null.
            var (board, slots) = MakeBoard(
                new Dictionary<string, string> { ["L0_0"] = "a", ["L0_1"] = "b", ["L0_2"] = "c", ["L0_3"] = "d" },
                movesRemaining: -1, shufflesRemaining: 1);

            Assert.That(EndStateEvaluator.Evaluate(board, slots), Is.EqualTo(EndState.InProgress));
        }

        [Test]
        public void Evaluate_StuckAndNoShuffles_ReturnsLost()
        {
            var (board, slots) = MakeBoard(
                new Dictionary<string, string> { ["L0_0"] = "a", ["L0_1"] = "b", ["L0_2"] = "c", ["L0_3"] = "d" },
                movesRemaining: -1, shufflesRemaining: 0);

            Assert.That(EndStateEvaluator.Evaluate(board, slots), Is.EqualTo(EndState.Lost));
        }
    }
}
