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
        // The one hidden tile (if any) currently showing its front face but
        // not yet collected. Null when nothing is peeked. See TileReveal.
        public string PeekedTileId;

        public int Score;
        public int ComboCount;

        public int MovesRemaining; // set from LevelDefinition.MovesBudget at generation; <= 0 (negative) means unlimited

        public int HintsRemaining = 3;
        public int UndosRemaining = 3;
        public int ShufflesRemaining = 3;
    }
}
