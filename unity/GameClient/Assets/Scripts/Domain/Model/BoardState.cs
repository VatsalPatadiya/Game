using System.Collections.Generic;

namespace GameDomain.Model
{
    public sealed class BoardState
    {
        public int LevelId;
        public Dictionary<string, TileCell> Cells = new Dictionary<string, TileCell>();
        public List<Move> MoveHistory = new List<Move>();

        public List<string> TrayTileIds = new List<string>();
        public int MaxTraySize = 4; // pair-match tray: holds up to 4 tiles; 4 distinct (no pair) = loss
        public bool IsGameOver = false;

        public int Score;
        public int ComboCount;

        public int HintsRemaining = 3;
        public int UndosRemaining = 3;
        public int ShufflesRemaining = 3;
    }
}
