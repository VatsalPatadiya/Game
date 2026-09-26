using System.Collections.Generic;
using System.Linq;
using GameDomain.Model;

namespace GameDomain.Gameplay
{
    public enum EndState { InProgress, Won, Lost }

    public static class EndStateEvaluator
    {
        // Pure win/lose/stuck resolution so the rules stay EditMode-testable
        // (GameController is a MonoBehaviour and can't be unit-tested).
        public static EndState Evaluate(BoardState board, Dictionary<string, TileSlot> slotsById)
        {
            if (board.Cells.Values.All(c => c.Cleared))
                return EndState.Won;

            // MovesRemaining == 0 is a real exhausted budget; a negative value
            // means unlimited/untracked (BoardGenerator copies MovesBudget
            // verbatim, and an unlimited level carries -1 which never hits 0).
            if (board.MovesRemaining == 0)
                return EndState.Lost;

            bool anyLegalMove = HintFinder.FindFreePair(board, slotsById) != null;
            if (!anyLegalMove && board.ShufflesRemaining <= 0)
                return EndState.Lost;

            return EndState.InProgress;
        }
    }
}
