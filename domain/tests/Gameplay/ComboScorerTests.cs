using System;
using GameDomain.Gameplay;
using GameDomain.Model;

namespace GameDomain.Tests.Gameplay
{
    public class ComboScorerTests
    {
        [Test]
        public void RegisterMatch_FirstMatch_AwardsBasePointsAndComboOne()
        {
            var board = new BoardState();
            var scorer = new ComboScorer();
            var now = new DateTime(2026, 1, 1, 12, 0, 0);

            int points = scorer.RegisterMatch(board, now);

            Assert.That(points, Is.EqualTo(100));
            Assert.That(board.Score, Is.EqualTo(100));
            Assert.That(board.ComboCount, Is.EqualTo(1));
        }

        [Test]
        public void RegisterMatch_WithinComboWindow_MultipliesPoints()
        {
            var board = new BoardState();
            var scorer = new ComboScorer();
            var t0 = new DateTime(2026, 1, 1, 12, 0, 0);

            scorer.RegisterMatch(board, t0);
            int secondPoints = scorer.RegisterMatch(board, t0.AddSeconds(1));

            Assert.That(secondPoints, Is.EqualTo(200));
            Assert.That(board.Score, Is.EqualTo(300));
            Assert.That(board.ComboCount, Is.EqualTo(2));
        }

        [Test]
        public void RegisterMatch_AfterComboWindowExpires_ResetsComboToOne()
        {
            var board = new BoardState();
            var scorer = new ComboScorer();
            var t0 = new DateTime(2026, 1, 1, 12, 0, 0);

            scorer.RegisterMatch(board, t0);
            int secondPoints = scorer.RegisterMatch(board, t0.AddSeconds(10));

            Assert.That(secondPoints, Is.EqualTo(100));
            Assert.That(board.ComboCount, Is.EqualTo(1));
        }

        [Test]
        public void ResetIfIdle_PastComboWindow_ZeroesComboCount()
        {
            var board = new BoardState();
            var scorer = new ComboScorer();
            var t0 = new DateTime(2026, 1, 1, 12, 0, 0);

            scorer.RegisterMatch(board, t0);
            scorer.ResetIfIdle(board, t0.AddSeconds(10));

            Assert.That(board.ComboCount, Is.EqualTo(0));
        }
    }
}
