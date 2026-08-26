using GameDomain.Model;

namespace GameDomain.Gameplay
{
    public static class UndoStack
    {
        public static bool TryUndo(BoardState board)
        {
            if (board.UndosRemaining <= 0)
                return false;

            if (board.MoveHistory.Count == 0)
                return false;

            var lastMove = board.MoveHistory[board.MoveHistory.Count - 1];
            board.MoveHistory.RemoveAt(board.MoveHistory.Count - 1);

            board.Cells[lastMove.SlotIdA].Cleared = false;
            board.Cells[lastMove.SlotIdB].Cleared = false;

            board.UndosRemaining -= 1;

            return true;
        }
    }
}
