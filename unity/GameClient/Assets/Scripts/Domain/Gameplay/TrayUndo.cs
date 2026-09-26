using GameDomain.Model;

namespace GameDomain.Gameplay
{
    // Undo for the tray mechanic: returns the most-recently-collected tile from
    // the tray back onto the board (it becomes tappable again). Consumes an undo.
    public static class TrayUndo
    {
        // Returns the popped slotId, or null if there is nothing to undo / no
        // charges remain.
        public static string TryUndo(BoardState board)
        {
            if (board.UndosRemaining <= 0) return null;
            if (board.TrayTileIds.Count == 0) return null;

            int last = board.TrayTileIds.Count - 1;
            string id = board.TrayTileIds[last];
            board.TrayTileIds.RemoveAt(last);
            board.UndosRemaining -= 1;
            board.IsGameOver = false; // freeing a slot can un-stick a full tray
            return id;
        }
    }
}
