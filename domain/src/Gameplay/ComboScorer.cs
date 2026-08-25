using System;
using GameDomain.Model;

namespace GameDomain.Gameplay
{
    public sealed class ComboScorer
    {
        private const int BasePointsPerMatch = 100;
        private static readonly TimeSpan ComboWindow = TimeSpan.FromSeconds(3);

        private DateTime? _lastMatchTime;

        public int RegisterMatch(BoardState board, DateTime matchTime)
        {
            if (_lastMatchTime.HasValue && matchTime - _lastMatchTime.Value <= ComboWindow)
            {
                board.ComboCount += 1;
            }
            else
            {
                board.ComboCount = 1;
            }

            _lastMatchTime = matchTime;

            int points = BasePointsPerMatch * board.ComboCount;
            board.Score += points;
            return points;
        }

        public void ResetIfIdle(BoardState board, DateTime now)
        {
            if (_lastMatchTime.HasValue && now - _lastMatchTime.Value > ComboWindow)
            {
                _lastMatchTime = null;
                board.ComboCount = 0;
            }
        }
    }
}
